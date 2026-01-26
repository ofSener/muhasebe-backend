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
            new AnkaraExcelParser(),
            new QuickExcelParser(),
            new HepiyiExcelParser(),
            new NeovaExcelParser(),
            new UnicoExcelParser(),
            new SompoExcelParser()
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
            // Excel verilerini oku
            var rows = await ReadExcelFileAsync(fileStream, fileName);

            if (rows.Count == 0)
            {
                return new ExcelImportPreviewDto
                {
                    TotalRows = 0,
                    ValidRows = 0,
                    InvalidRows = 0,
                    Rows = new List<ExcelImportRowDto>()
                };
            }

            // Parser seç
            var headers = rows.First().Keys.ToList();
            IExcelParser? parser = null;

            if (sigortaSirketiId.HasValue)
            {
                // Kullanıcının seçtiği şirkete göre parser bul
                parser = _parsers.FirstOrDefault(p => p.SigortaSirketiId == sigortaSirketiId.Value);
            }

            if (parser == null)
            {
                // Otomatik tespit
                parser = _parsers.FirstOrDefault(p => p.CanParse(fileName, headers));
            }

            if (parser == null)
            {
                // Default parser (ilk uygun olanı kullan)
                parser = _parsers.First();
                _logger.LogWarning("Parser otomatik tespit edilemedi, default parser kullanılıyor: {Parser}", parser.SirketAdi);
            }

            _logger.LogInformation("Parser seçildi: {Parser}", parser.SirketAdi);

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

                // Müşteri kontrolü ve oluşturma
                var musteriId = row.MusteriId;
                if (!musteriId.HasValue && !string.IsNullOrEmpty(row.TcVkn))
                {
                    // Müşteriyi bul veya oluştur
                    var musteri = await FindOrCreateMusteriAsync(row);
                    musteriId = musteri.Id;
                    if (musteri.Id == 0) // Yeni oluşturuldu
                    {
                        _context.Musteriler.Add(musteri);
                        await _context.SaveChangesAsync();
                        musteriId = musteri.Id;
                        newCustomersCreated++;
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
                    Vergi = row.Vergi ?? 0,
                    Komisyon = row.Komisyon ?? 0,
                    BransId = row.BransId ?? 0,
                    MusteriId = musteriId ?? 0,
                    SigortaEttirenId = musteriId ?? 0,
                    IsOrtagiFirmaId = row.IsOrtagiFirmaId ?? _currentUserService.FirmaId ?? 0,
                    IsOrtagiSubeId = row.IsOrtagiSubeId ?? _currentUserService.SubeId ?? 0,
                    IsOrtagiUyeId = row.IsOrtagiUyeId ?? 0,
                    EklenmeTarihi = _dateTimeService.Now,
                    KayitDurumu = 1,
                    DisPolice = 0,
                    PoliceTespitKaynakId = 3, // Excel import
                    Sube = row.Sube,
                    PoliceKesenPersonel = row.PoliceKesenPersonel
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

        // Değişiklikleri kaydet
        await _context.SaveChangesAsync();

        // Cache'den sil
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
            // DB'den şirket bilgisini al
            var sirket = await _context.SigortaSirketleri
                .FirstOrDefaultAsync(s => s.Ad.Contains(parser.SirketAdi) ||
                                         parser.FileNamePatterns.Any(p => s.Ad.ToLower().Contains(p)));

            formats.Add(new SupportedFormatDto
            {
                SigortaSirketiId = sirket?.Id ?? parser.SigortaSirketiId,
                SigortaSirketiAdi = sirket?.Ad ?? parser.SirketAdi,
                FormatDescription = $"{parser.SirketAdi} Excel formatı",
                RequiredColumns = new List<string> { "Poliçe No", "Brüt Prim", "Başlangıç Tarihi", "Bitiş Tarihi" },
                Notes = parser.SirketAdi == "Sompo Sigorta"
                    ? "Header 2. satırda, ilk satır firma bilgisi"
                    : null
            });
        }

        return new SupportedFormatsResponseDto { Formats = formats };
    }

    public Task<ImportHistoryListDto> GetImportHistoryAsync(int? firmaId, int page = 1, int pageSize = 20)
    {
        // TODO: Import history için ayrı bir tablo oluşturulabilir
        // Şimdilik boş liste döndür
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

    private async Task<List<IDictionary<string, object?>>> ReadExcelFileAsync(Stream fileStream, string fileName)
    {
        var rows = new List<IDictionary<string, object?>>();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (extension == ".xlsx")
        {
            // EPPlus kullan (.xlsx için)
            using var package = new ExcelPackage(fileStream);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();

            if (worksheet == null || worksheet.Dimension == null)
                return rows;

            // Header'ları al (ilk satır)
            var headers = new List<string>();
            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
            {
                var header = worksheet.Cells[1, col].Value?.ToString()?.Trim();
                headers.Add(string.IsNullOrEmpty(header) ? $"Column_{col}" : header);
            }

            // Veri satırlarını oku
            for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
            {
                var rowData = new Dictionary<string, object?>();
                bool hasData = false;

                for (int col = 1; col <= headers.Count; col++)
                {
                    var cellValue = worksheet.Cells[row, col].Value;
                    rowData[headers[col - 1]] = cellValue;
                    if (cellValue != null && !string.IsNullOrWhiteSpace(cellValue.ToString()))
                        hasData = true;
                }

                if (hasData)
                    rows.Add(rowData);
            }
        }
        else if (extension == ".xls")
        {
            // ExcelDataReader kullan (.xls için)
            using var reader = ExcelReaderFactory.CreateReader(fileStream);
            var result = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
            });

            if (result.Tables.Count == 0)
                return rows;

            var table = result.Tables[0];

            // Header'ları al
            var headers = new List<string>();
            for (int col = 0; col < table.Columns.Count; col++)
            {
                var header = table.Columns[col].ColumnName;
                headers.Add(string.IsNullOrEmpty(header) ? $"Column_{col}" : header);
            }

            // Veri satırlarını oku
            foreach (System.Data.DataRow dataRow in table.Rows)
            {
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

                if (hasData)
                    rows.Add(rowData);
            }
        }
        else if (extension == ".csv")
        {
            // CSV okuma
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

        return rows;
    }

    private async Task EnrichRowsWithLookupDataAsync(List<ExcelImportRowDto> rows)
    {
        // Tüm TC/VKN'leri topla
        var tcVknList = rows
            .Where(r => !string.IsNullOrEmpty(r.TcVkn))
            .Select(r => r.TcVkn!)
            .Distinct()
            .ToList();

        // Mevcut müşterileri sorgula
        var existingCustomers = await _context.Musteriler
            .Where(m => tcVknList.Contains(m.TcKimlikNo!) ||
                       tcVknList.Contains(m.VergiNo!) ||
                       tcVknList.Contains(m.TcVergiNo!))
            .ToListAsync();

        // Branşları al
        var branslar = await _context.Branslar.ToListAsync();
        var policeTurleri = await _context.PoliceTurleri.ToListAsync();

        // Her satır için eşleştirme yap
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            // Müşteri eşleştirme
            if (!string.IsNullOrEmpty(row.TcVkn))
            {
                var musteri = existingCustomers.FirstOrDefault(m =>
                    m.TcKimlikNo == row.TcVkn ||
                    m.VergiNo == row.TcVkn ||
                    m.TcVergiNo == row.TcVkn);

                if (musteri != null)
                {
                    rows[i] = row with { MusteriId = musteri.Id };
                }
            }

            // Branş eşleştirme
            if (!string.IsNullOrEmpty(row.UrunAdi))
            {
                var urunAdiUpper = row.UrunAdi.ToUpperInvariant();

                // Önce PoliceTuru'ndan ara
                var policeTuru = policeTurleri.FirstOrDefault(p =>
                    p.Turu != null && urunAdiUpper.Contains(p.Turu.ToUpperInvariant()));

                if (policeTuru != null)
                {
                    rows[i] = rows[i] with { BransId = policeTuru.Id };
                }
                else
                {
                    // Sonra Brans'tan ara
                    var brans = branslar.FirstOrDefault(b =>
                        urunAdiUpper.Contains(b.Ad.ToUpperInvariant()) ||
                        b.Ad.ToUpperInvariant().Contains(urunAdiUpper));

                    if (brans != null)
                    {
                        rows[i] = rows[i] with { BransId = brans.Id };
                    }
                }
            }
        }
    }

    private async Task<Musteri> FindOrCreateMusteriAsync(ExcelImportRowDto row)
    {
        // Mevcut müşteriyi ara
        var existingMusteri = await _context.Musteriler
            .FirstOrDefaultAsync(m =>
                m.TcKimlikNo == row.TcVkn ||
                m.VergiNo == row.TcVkn ||
                m.TcVergiNo == row.TcVkn);

        if (existingMusteri != null)
            return existingMusteri;

        // Yeni müşteri oluştur
        var isIndividual = row.TcVkn?.Length == 11;
        var nameParts = (row.SigortaliAdi ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return new Musteri
        {
            SahipTuru = (sbyte)(isIndividual ? 1 : 2), // 1: Bireysel, 2: Kurumsal
            TcKimlikNo = isIndividual ? row.TcVkn : null,
            VergiNo = !isIndividual ? row.TcVkn : null,
            TcVergiNo = row.TcVkn,
            Adi = nameParts.Length > 0 ? string.Join(" ", nameParts.Take(nameParts.Length - 1)) : row.SigortaliAdi,
            Soyadi = nameParts.Length > 1 ? nameParts.Last() : null,
            EkleyenFirmaId = _currentUserService.FirmaId,
            EkleyenSubeId = _currentUserService.SubeId,
            EklenmeZamani = _dateTimeService.Now
        };
    }

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

/// <summary>
/// Import session verisi (cache için)
/// </summary>
internal class ImportSessionData
{
    public string SessionId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int SigortaSirketiId { get; set; }
    public List<ExcelImportRowDto> Rows { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}
