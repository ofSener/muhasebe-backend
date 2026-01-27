using System.Text;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.ExcelImport.Dtos;
using IhsanAI.Domain.Entities;
using IhsanAI.Infrastructure.Services.Parsers;

namespace IhsanAI.Infrastructure.Services;

public class ExcelImportService : IExcelImportService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ExcelImportService> _logger;
    private readonly List<IExcelParser> _parsers;

    public ExcelImportService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService,
        IMemoryCache cache,
        ILogger<ExcelImportService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
        _cache = cache;
        _logger = logger;

        // Parser'ları kaydet
        _parsers = new List<IExcelParser>
        {
            new SompoExcelParser(),
            new AnkaraExcelParser(),
            new QuickExcelParser(),
            new HepiyiExcelParser(),
            new NeovaExcelParser(),
            new UnicoExcelParser()
        };

        // EPPlus lisans ayarı (non-commercial)
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        // ExcelDataReader için encoding kaydı
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<ExcelImportPreviewDto> ParseExcelAsync(Stream fileStream, string fileName, int? sigortaSirketiId = null)
    {
        _logger.LogInformation("Excel parsing başlatılıyor: {FileName}", fileName);

        try
        {
            // Parser seç (dosya adından)
            IExcelParser? parser = null;

            if (sigortaSirketiId.HasValue)
            {
                parser = _parsers.FirstOrDefault(p => p.SigortaSirketiId == sigortaSirketiId.Value);
            }

            if (parser == null)
            {
                // Dosya adından otomatik tespit
                var fileNameLower = fileName.ToLowerInvariant();
                parser = _parsers.FirstOrDefault(p =>
                    p.FileNamePatterns.Any(pattern => fileNameLower.Contains(pattern.ToLowerInvariant())));
            }

            if (parser == null)
            {
                // Default: Ankara parser
                parser = _parsers.First(p => p.SirketAdi.Contains("Ankara"));
                _logger.LogWarning("Parser otomatik tespit edilemedi, default parser kullanılıyor: {Parser}", parser.SirketAdi);
            }

            _logger.LogInformation("Parser seçildi: {Parser}", parser.SirketAdi);

            // Excel verilerini oku (parser'a özel)
            var rows = await ReadExcelFileAsync(fileStream, fileName, parser);

            if (rows.Count == 0)
            {
                return new ExcelImportPreviewDto
                {
                    TotalRows = 0,
                    ValidRows = 0,
                    InvalidRows = 0,
                    Rows = new List<ExcelImportRowDto>(),
                    DetectedFormat = parser.SirketAdi
                };
            }

            // Parse et
            var parsedRows = parser.Parse(rows);

            // Müşteri ve branş eşleştirmelerini yap
            await EnrichRowsWithLookupDataAsync(parsedRows);

            // Session oluştur
            var sessionId = Guid.NewGuid().ToString();
            var cacheEntry = new ImportSessionData
            {
                SessionId = sessionId,
                FileName = fileName,
                SigortaSirketiId = sigortaSirketiId ?? parser.SigortaSirketiId,
                Rows = parsedRows,
                CreatedAt = _dateTimeService.Now
            };

            // Cache'e kaydet (30 dakika geçerli)
            _cache.Set(sessionId, cacheEntry, TimeSpan.FromMinutes(30));

            // Sigorta şirketi adını al
            var sirket = await _context.SigortaSirketleri
                .FirstOrDefaultAsync(s => s.Id == cacheEntry.SigortaSirketiId);

            return new ExcelImportPreviewDto
            {
                TotalRows = parsedRows.Count,
                ValidRows = parsedRows.Count(r => r.IsValid),
                InvalidRows = parsedRows.Count(r => !r.IsValid),
                Rows = parsedRows.Take(100).ToList(), // İlk 100 satırı önizleme olarak döndür
                ImportSessionId = sessionId,
                FileName = fileName,
                SigortaSirketiId = cacheEntry.SigortaSirketiId,
                SigortaSirketiAdi = sirket?.Ad ?? parser.SirketAdi,
                DetectedFormat = parser.SirketAdi
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excel parsing hatası: {FileName}", fileName);
            throw;
        }
    }

    public async Task<ExcelImportResultDto> ConfirmImportAsync(string sessionId)
    {
        _logger.LogInformation("Import onaylanıyor: {SessionId}", sessionId);

        if (!_cache.TryGetValue<ImportSessionData>(sessionId, out var session) || session == null)
        {
            return new ExcelImportResultDto
            {
                Success = false,
                ErrorMessage = "Oturum bulunamadı veya süresi dolmuş. Lütfen dosyayı tekrar yükleyin."
            };
        }

        var errors = new List<ExcelImportErrorDto>();
        var successCount = 0;
        var duplicateCount = 0;
        var newCustomersCreated = 0;

        // Sadece geçerli satırları al
        var validRows = session.Rows.Where(r => r.IsValid).ToList();

        foreach (var row in validRows)
        {
            try
            {
                // Duplicate kontrolü
                var existingPolice = await _context.PoliceHavuzlari
                    .FirstOrDefaultAsync(p =>
                        p.PoliceNo == row.PoliceNo &&
                        p.SigortaSirketiId == session.SigortaSirketiId &&
                        p.ZeyilNo == GetZeyilNoAsSbyte(row.ZeyilNo));

                if (existingPolice != null)
                {
                    duplicateCount++;
                    errors.Add(new ExcelImportErrorDto
                    {
                        RowNumber = row.RowNumber,
                        PoliceNo = row.PoliceNo,
                        ErrorMessage = "Bu poliçe zaten mevcut"
                    });
                    continue;
                }

                // Müşteri kontrolü - TC/VKN olmadan isim ile arama yapılabilir
                var musteriId = row.MusteriId;
                if (!musteriId.HasValue && !string.IsNullOrEmpty(row.SigortaliAdi))
                {
                    // İsim ile mevcut müşteri ara
                    var sigortaliAdi = row.SigortaliAdi.Trim().ToUpperInvariant();
                    var existingMusteri = await _context.Musteriler
                        .FirstOrDefaultAsync(m =>
                            (m.Adi + " " + m.Soyadi).ToUpper() == sigortaliAdi ||
                            m.Adi!.ToUpper() == sigortaliAdi);

                    if (existingMusteri != null)
                    {
                        musteriId = existingMusteri.Id;
                    }
                }

                // PoliceHavuz kaydı oluştur
                var policeHavuz = new PoliceHavuz
                {
                    PoliceTipi = row.PoliceTipi ?? "TAHAKKUK",
                    PoliceNo = row.PoliceNo ?? string.Empty,
                    Plaka = row.Plaka ?? string.Empty,
                    ZeyilNo = GetZeyilNoAsSbyte(row.ZeyilNo),
                    YenilemeNo = GetYenilemeNoAsSbyte(row.YenilemeNo),
                    SigortaSirketiId = session.SigortaSirketiId,
                    TanzimTarihi = row.TanzimTarihi ?? row.BaslangicTarihi ?? _dateTimeService.Now,
                    BaslangicTarihi = row.BaslangicTarihi ?? _dateTimeService.Now,
                    BitisTarihi = row.BitisTarihi ?? row.BaslangicTarihi?.AddYears(1) ?? _dateTimeService.Now.AddYears(1),
                    BrutPrim = row.BrutPrim ?? 0,
                    NetPrim = row.NetPrim ?? row.BrutPrim ?? 0,
                    Vergi = 0,  // Vergi kaldırıldı
                    Komisyon = row.Komisyon ?? 0,
                    BransId = row.BransId ?? 0,
                    MusteriId = musteriId ?? 0,
                    SigortaEttirenId = musteriId ?? 0,
                    IsOrtagiFirmaId = _currentUserService.FirmaId ?? 0,
                    IsOrtagiSubeId = _currentUserService.SubeId ?? 0,
                    IsOrtagiUyeId = 0,
                    EklenmeTarihi = _dateTimeService.Now,
                    KayitDurumu = 1,
                    DisPolice = 0,
                    PoliceTespitKaynakId = 3,
                    Sube = null,
                    PoliceKesenPersonel = null
                };

                _context.PoliceHavuzlari.Add(policeHavuz);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Satır import hatası: {RowNumber}", row.RowNumber);
                errors.Add(new ExcelImportErrorDto
                {
                    RowNumber = row.RowNumber,
                    PoliceNo = row.PoliceNo,
                    ErrorMessage = ex.Message
                });
            }
        }

        await _context.SaveChangesAsync();
        _cache.Remove(sessionId);

        _logger.LogInformation(
            "Import tamamlandı: {SuccessCount} başarılı, {FailedCount} hatalı, {DuplicateCount} duplicate",
            successCount, errors.Count, duplicateCount);

        return new ExcelImportResultDto
        {
            Success = true,
            TotalProcessed = validRows.Count,
            SuccessCount = successCount,
            FailedCount = errors.Count,
            DuplicateCount = duplicateCount,
            NewCustomersCreated = newCustomersCreated,
            Errors = errors
        };
    }

    public async Task<SupportedFormatsResponseDto> GetSupportedFormatsAsync()
    {
        var formats = new List<SupportedFormatDto>();

        foreach (var parser in _parsers)
        {
            var searchName = parser.SirketAdi.ToLower().Split(' ')[0];
            var sirket = await _context.SigortaSirketleri
                .FirstOrDefaultAsync(s => s.Ad.ToLower().Contains(searchName));

            formats.Add(new SupportedFormatDto
            {
                SigortaSirketiId = sirket?.Id ?? parser.SigortaSirketiId,
                SigortaSirketiAdi = sirket?.Ad ?? parser.SirketAdi,
                FormatDescription = $"{parser.SirketAdi} Excel formatı",
                RequiredColumns = new List<string> { "Poliçe No", "Brüt Prim", "Başlangıç Tarihi" },
                Notes = parser.SirketAdi.Contains("Sompo")
                    ? "Header 2. satırda, ilk satır firma bilgisi"
                    : null
            });
        }

        return new SupportedFormatsResponseDto { Formats = formats };
    }

    public Task<ImportHistoryListDto> GetImportHistoryAsync(int? firmaId, int page = 1, int pageSize = 20)
    {
        return Task.FromResult(new ImportHistoryListDto
        {
            Items = new List<ImportHistoryDto>(),
            TotalCount = 0
        });
    }

    public int? DetectSigortaSirketiFromFileName(string fileName)
    {
        var fileNameLower = fileName.ToLowerInvariant();

        foreach (var parser in _parsers)
        {
            if (parser.FileNamePatterns.Any(p => fileNameLower.Contains(p.ToLowerInvariant())))
            {
                return parser.SigortaSirketiId;
            }
        }

        return null;
    }

    #region Private Methods

    private async Task<List<IDictionary<string, object?>>> ReadExcelFileAsync(Stream fileStream, string fileName, IExcelParser parser)
    {
        var rows = new List<IDictionary<string, object?>>();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        // SOMPO formatı için özel işlem: header 3. satırda (EPPlus 1-indexed)
        // Row 1 = Firma adı, Row 2 = Tarih aralığı, Row 3 = Headers
        var isSompoFormat = parser.SirketAdi.Contains("Sompo");
        var headerRowIndex = isSompoFormat ? 3 : 1; // EPPlus 1-indexed

        if (extension == ".xlsx")
        {
            using var package = new ExcelPackage(fileStream);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();

            if (worksheet == null || worksheet.Dimension == null)
                return rows;

            // Header satırını bul
            var headers = new List<string>();
            var actualHeaderRow = headerRowIndex;

            // SOMPO formatında gerçek header'ları bul
            if (isSompoFormat)
            {
                // 3. satırdaki (EPPlus row 3) header'ları al
                for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                {
                    var cellValue = worksheet.Cells[3, col].Value?.ToString()?.Trim();
                    headers.Add(string.IsNullOrEmpty(cellValue) ? $"Column_{col}" : cellValue);
                }
                actualHeaderRow = 3;
            }
            else
            {
                // Normal format: ilk satır header
                for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                {
                    var header = worksheet.Cells[1, col].Value?.ToString()?.Trim();
                    headers.Add(string.IsNullOrEmpty(header) ? $"Column_{col}" : header);
                }
                actualHeaderRow = 1;
            }

            _logger.LogInformation("Headers found: {Headers}", string.Join(", ", headers));

            // Veri satırlarını oku
            for (int row = actualHeaderRow + 1; row <= worksheet.Dimension.End.Row; row++)
            {
                var rowData = new Dictionary<string, object?>();
                bool hasData = false;

                for (int col = 1; col <= headers.Count; col++)
                {
                    var cellValue = worksheet.Cells[row, col].Value;
                    var headerName = headers[col - 1];
                    rowData[headerName] = cellValue;

                    if (cellValue != null && !string.IsNullOrWhiteSpace(cellValue.ToString()))
                        hasData = true;
                }

                // Boş satırları veya özet satırlarını atla
                if (hasData && !IsSkippableRow(rowData, isSompoFormat))
                {
                    rows.Add(rowData);
                }
            }
        }
        else if (extension == ".xls")
        {
            using var reader = ExcelReaderFactory.CreateReader(fileStream);
            var result = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
            });

            if (result.Tables.Count == 0)
                return rows;

            var table = result.Tables[0];

            if (table.Rows.Count < headerRowIndex)
                return rows;

            // Header satırını al
            var headers = new List<string>();
            var headerRow = table.Rows[headerRowIndex - 1]; // 0-indexed

            for (int col = 0; col < table.Columns.Count; col++)
            {
                var header = headerRow[col]?.ToString()?.Trim();
                headers.Add(string.IsNullOrEmpty(header) ? $"Column_{col}" : header);
            }

            // Veri satırlarını oku
            for (int rowIdx = headerRowIndex; rowIdx < table.Rows.Count; rowIdx++)
            {
                var dataRow = table.Rows[rowIdx];
                var rowData = new Dictionary<string, object?>();
                bool hasData = false;

                for (int col = 0; col < headers.Count; col++)
                {
                    var cellValue = dataRow[col];
                    if (cellValue == DBNull.Value) cellValue = null;
                    rowData[headers[col]] = cellValue;
                    if (cellValue != null && !string.IsNullOrWhiteSpace(cellValue.ToString()))
                        hasData = true;
                }

                if (hasData && !IsSkippableRow(rowData, isSompoFormat))
                    rows.Add(rowData);
            }
        }
        else if (extension == ".csv")
        {
            fileStream.Position = 0;
            using var reader = new StreamReader(fileStream, Encoding.UTF8);
            var headerLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(headerLine))
                return rows;

            var headers = headerLine.Split(new[] { ',', ';', '\t' }, StringSplitOptions.None)
                .Select(h => h.Trim().Trim('"'))
                .ToList();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = line.Split(new[] { ',', ';', '\t' }, StringSplitOptions.None)
                    .Select(v => v.Trim().Trim('"'))
                    .ToList();

                var rowData = new Dictionary<string, object?>();
                bool hasData = false;

                for (int i = 0; i < headers.Count && i < values.Count; i++)
                {
                    rowData[headers[i]] = values[i];
                    if (!string.IsNullOrWhiteSpace(values[i]))
                        hasData = true;
                }

                if (hasData)
                    rows.Add(rowData);
            }
        }

        _logger.LogInformation("Total rows read: {Count}", rows.Count);
        return rows;
    }

    private bool IsSkippableRow(IDictionary<string, object?> row, bool isSompoFormat)
    {
        // Özet satırlarını atla (Tahakkuk, İptal, Net, Brüt Prim gibi)
        var firstValue = row.Values.FirstOrDefault()?.ToString()?.ToUpperInvariant();

        if (string.IsNullOrEmpty(firstValue))
            return true;

        var skipKeywords = new[] { "TAHAKKUK", "İPTAL", "IPTAL", "NET", "BRÜT", "BRUT", "TOPLAM", "NET PRİM", "BRÜT PRİM" };

        if (skipKeywords.Any(k => firstValue.Contains(k)))
            return true;

        // SOMPO formatında tarih aralığı satırını atla
        if (isSompoFormat && firstValue.Contains("/") && firstValue.Contains("-"))
            return true;

        return false;
    }

    private async Task EnrichRowsWithLookupDataAsync(List<ExcelImportRowDto> rows)
    {
        var branslar = await _context.Branslar.ToListAsync();
        var policeTurleri = await _context.PoliceTurleri.ToListAsync();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            // Branş eşleştirmesi
            if (!string.IsNullOrEmpty(row.Brans))
            {
                var bransUpper = row.Brans.ToUpperInvariant();

                var policeTuru = policeTurleri.FirstOrDefault(p =>
                    p.Turu != null && bransUpper.Contains(p.Turu.ToUpperInvariant()));

                if (policeTuru != null)
                {
                    rows[i] = rows[i] with { BransId = policeTuru.Id };
                }
                else
                {
                    var brans = branslar.FirstOrDefault(b =>
                        bransUpper.Contains(b.Ad.ToUpperInvariant()) ||
                        b.Ad.ToUpperInvariant().Contains(bransUpper));

                    if (brans != null)
                    {
                        rows[i] = rows[i] with { BransId = brans.Id };
                    }
                }
            }
        }
    }

    // FindOrCreateMusteriAsync kaldırıldı - TC/VKN olmadan müşteri oluşturma devre dışı
    // İleride SigortaliAdi ile müşteri eşleştirme/oluşturma eklenebilir

    private static sbyte GetZeyilNoAsSbyte(string? value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        if (sbyte.TryParse(value, out var result))
            return result;
        return 0;
    }

    private static sbyte? GetYenilemeNoAsSbyte(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (sbyte.TryParse(value, out var result))
            return result;
        return null;
    }

    #endregion
}

internal class ImportSessionData
{
    public string SessionId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int SigortaSirketiId { get; set; }
    public List<ExcelImportRowDto> Rows { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
