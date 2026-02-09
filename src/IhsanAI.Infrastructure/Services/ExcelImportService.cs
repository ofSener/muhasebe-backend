using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.ExcelImport.Dtos;
using IhsanAI.Domain.Entities;
using IhsanAI.Infrastructure.Common;
using IhsanAI.Infrastructure.Services.Parsers;

namespace IhsanAI.Infrastructure.Services;

public class ExcelImportService : IExcelImportService
{
    private static readonly string TempDirectory = Path.Combine(Path.GetTempPath(), "IhsanAI_Import");

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ExcelImportService> _logger;
    private readonly List<IExcelParser> _parsers;
    private readonly QuickXmlParser _quickXmlParser;
    private readonly UnicoXmlParser _unicoXmlParser;

    static ExcelImportService()
    {
        // Startup: eski orphan temp dosyalarını temizle (1 saatten eski)
        CleanupOldTempFiles();
    }

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

        // Parser'ları kaydet - SIRA ÖNEMLİ: Spesifik signature'lar önce, genel olanlar sona
        // Bidirectional Contains matching yüzünden genel kolonlar ("Ürün No", "Sigortalı Ünvanı" vb.)
        // yanlış eşleşme yapabilir. Daha spesifik/benzersiz signature'a sahip parser'lar önce kontrol edilmeli.
        _parsers = new List<IExcelParser>
        {
            // --- En spesifik signature'lar (benzersiz kolon adları) ---
            new NeovaExcelParser(),     // 4 benzersiz sig: "KOD", "G/T", "MÜŞTERİ TCKN / VKN", "P/T/R"
            new CorpusExcelParser(),    // 3 benzersiz sig: "ACENTA POL NO", "ORTAKLIK_BEDELI", "SİGORTALI ÜNVANI"
            new QuickExcelParser(),     // 2 benzersiz sig: "UrunAd", "AcenteKomisyon" (camelCase)
            new HepiyiExcelParser(),    // 3 sig: "Police Tür Kod" benzersiz
            new UnicoExcelParser(),     // 3 sig: "Tarife Adı" benzersiz
            new AnkaraExcelParser(),    // 2 sig: "Partaj Adı" benzersiz
            new AkExcelParser(),        // 3 sig: "BAS/YUK" benzersiz

            // --- Ortak kolonlu grup (İpt/Kay, Vade Başlangıç) - spesifikten genele ---
            new KoruExcelParser(),      // 4 sig: Doğa + "Sepet Id" (Koru'ya özgü) → Doğa'dan önce
            new DogaExcelParser(),      // 4 sig: "Sbm Havuz" (HDI'da yok) → HDI'dan önce
            new HdiExcelParser(),       // 3 sig: "Tecdit No" benzersiz ama İpt/Kay+Vade ortak

            // --- En genel signature (yaygın kolon adları) - en sona ---
            new SompoExcelParser()      // 3 sig: "Ürün No", "Sigortalı Ünvanı", "Döviz Cinsi" (çok genel, birçok Excel'de bulunur)
        };

        // XML Parsers
        _quickXmlParser = new QuickXmlParser();
        _unicoXmlParser = new UnicoXmlParser();

        // EPPlus lisans ayarı (non-commercial)
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        // ExcelDataReader için encoding kaydı
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<ExcelImportPreviewDto> ParseExcelAsync(Stream fileStream, string fileName, int? sigortaSirketiId = null)
    {
        _logger.LogInformation("Dosya parsing başlatılıyor: {FileName}", fileName);

        try
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            // XML dosyası ise XML parser kullan
            if (extension == ".xml")
            {
                return await ParseXmlFileAsync(fileStream, fileName, sigortaSirketiId);
            }

            // Parser seç
            IExcelParser? parser = null;

            // 1. Explicit sigorta şirketi ID'si verilmişse onu kullan
            if (sigortaSirketiId.HasValue)
            {
                // Önce parser ID ile dene
                parser = _parsers.FirstOrDefault(p => p.SigortaSirketiId == sigortaSirketiId.Value);

                // Bulunamadıysa database'den şirket adını bul ve parser eşleştir
                if (parser == null)
                {
                    var dbSirket = await _context.SigortaSirketleri
                        .FirstOrDefaultAsync(s => s.Id == sigortaSirketiId.Value);

                    if (dbSirket != null)
                    {
                        var sirketAdLower = dbSirket.Ad.ToLower();
                        parser = _parsers.FirstOrDefault(p =>
                            sirketAdLower.Contains(p.SirketAdi.ToLower().Split(' ')[0]) ||
                            p.SirketAdi.ToLower().Contains(sirketAdLower.Split(' ')[0]));

                        if (parser != null)
                        {
                            _logger.LogInformation("Parser database şirket adından bulundu: {SirketAd} -> {Parser}",
                                dbSirket.Ad, parser.SirketAdi);
                        }
                    }
                }
            }

            // 2. Dosya adından otomatik tespit (Türkçe karakter normalizasyonu ile)
            if (parser == null)
            {
                var normalizedFileName = TurkishStringHelper.Normalize(fileName);
                parser = _parsers.FirstOrDefault(p =>
                    p.FileNamePatterns.Any(pattern => normalizedFileName.Contains(TurkishStringHelper.Normalize(pattern))));
            }

            // 3. İçerik bazlı tespit - header kolonlarına bakarak parser seç
            if (parser == null)
            {
                _logger.LogInformation("Dosya adından parser tespit edilemedi, içerik bazlı tespit deneniyor...");
                parser = await DetectParserFromContentAsync(fileStream, fileName);
                fileStream.Position = 0; // Stream'i başa sar
            }

            // 4. Hala bulunamadıysa hata döndür
            if (parser == null)
            {
                _logger.LogWarning("Parser otomatik tespit edilemedi: {FileName}", fileName);
                return new ExcelImportPreviewDto
                {
                    TotalRows = 0,
                    ValidRows = 0,
                    InvalidRows = 0,
                    Rows = new List<ExcelImportRowDto>(),
                    DetectedFormat = "Bilinmeyen Format - Lütfen sigorta şirketini manuel seçin"
                };
            }

            _logger.LogInformation("Parser seçildi: {Parser}", parser.SirketAdi);

            // Excel verilerini oku (parser'a özel)
            fileStream.Position = 0; // Stream'in başında olduğundan emin ol
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

            // Ek sayfaları oku (varsa)
            var additionalSheets = new Dictionary<string, List<IDictionary<string, object?>>>();
            if (parser.AdditionalSheetNames != null && parser.AdditionalSheetNames.Length > 0)
            {
                fileStream.Position = 0;
                additionalSheets = await ReadAdditionalSheetsAsync(fileStream, fileName, parser);
            }

            // Parse et - ek sayfa varsa onu kullan
            List<ExcelImportRowDto> parsedRows;
            if (additionalSheets.Count > 0)
            {
                parsedRows = parser.ParseWithAdditionalSheets(rows, additionalSheets);
            }
            else
            {
                parsedRows = parser.Parse(rows);
            }

            // Müşteri ve branş eşleştirmelerini yap
            await EnrichRowsWithLookupDataAsync(parsedRows);

            // Session oluştur - satırları temp dosyaya yaz (bellek tasarrufu)
            var sessionId = Guid.NewGuid().ToString();
            var tempFilePath = SaveRowsToTempFile(parsedRows);
            var cacheEntry = new ImportSessionData
            {
                SessionId = sessionId,
                UserId = _currentUserService.UserId ?? string.Empty,
                FileName = fileName,
                SigortaSirketiId = sigortaSirketiId ?? parser.SigortaSirketiId,
                TempFilePath = tempFilePath,
                TotalRowCount = parsedRows.Count,
                ValidRowCount = parsedRows.Count(r => r.IsValid),
                CreatedAt = _dateTimeService.Now
            };

            // Cache'e kaydet (30 dakika, PostEviction ile temp dosya temizlenir)
            _cache.Set(sessionId, cacheEntry, CreateCacheOptions());

            // Sigorta şirketi adını al
            var sirket = await _context.SigortaSirketleri
                .FirstOrDefaultAsync(s => s.Id == cacheEntry.SigortaSirketiId);

            return new ExcelImportPreviewDto
            {
                TotalRows = parsedRows.Count,
                ValidRows = parsedRows.Count(r => r.IsValid),
                InvalidRows = parsedRows.Count(r => !r.IsValid),
                Rows = parsedRows, // Tüm satırları döndür, frontend sayfalama yapacak
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

        // Session sahiplik kontrolü
        if (!string.IsNullOrEmpty(session.UserId) && session.UserId != _currentUserService.UserId)
        {
            _logger.LogWarning("Session sahiplik hatası: SessionUserId={SessionUserId}, CurrentUserId={CurrentUserId}",
                session.UserId, _currentUserService.UserId);
            return new ExcelImportResultDto
            {
                Success = false,
                ErrorMessage = "Bu oturum size ait değil."
            };
        }

        var errors = new List<ExcelImportErrorDto>();
        var successCount = 0;
        var duplicateCount = 0;
        var newCustomersCreated = 0;

        // Satırları al (temp dosyadan veya cache'ten)
        var allRows = GetSessionRows(session);
        var validRows = allRows.Where(r => r.IsValid).ToList();
        _logger.LogInformation("Import: {TotalCount} satır, {ValidCount} geçerli", allRows.Count, validRows.Count);

        if (validRows.Count == 0)
        {
            return new ExcelImportResultDto
            {
                Success = false,
                ErrorMessage = "İçe aktarılacak geçerli satır bulunamadı."
            };
        }

        // --- PERFORMANS: Tüm sorguları döngü dışında yap ---

        // Duplicate kontrolü için mevcut poliçeleri toplu çek
        var allPoliceNos = validRows
            .Where(r => !string.IsNullOrEmpty(r.PoliceNo))
            .Select(r => r.PoliceNo!)
            .Distinct()
            .ToList();

        var existingPolicies = await _context.PoliceHavuzlari
            .Where(p => allPoliceNos.Contains(p.PoliceNo) && p.SigortaSirketiId == session.SigortaSirketiId)
            .Select(p => new { p.PoliceNo, p.ZeyilNo })
            .ToListAsync();

        var existingPolicyKeys = existingPolicies
            .Select(p => $"{p.PoliceNo}_{p.ZeyilNo}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Müşteri lookup: isim ile toplu sorgula
        var customerNames = validRows
            .Where(r => !r.MusteriId.HasValue && !string.IsNullOrEmpty(r.SigortaliAdi))
            .Select(r => r.SigortaliAdi!.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        var customerLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (customerNames.Count > 0)
        {
            var musteriler = await _context.Musteriler
                .Where(m => m.Adi != null)
                .Select(m => new { m.Id, m.Adi, m.Soyadi })
                .ToListAsync();

            foreach (var m in musteriler)
            {
                var fullName = ((m.Adi ?? "") + " " + (m.Soyadi ?? "")).Trim().ToUpperInvariant();
                var firstName = (m.Adi ?? "").Trim().ToUpperInvariant();
                if (!string.IsNullOrEmpty(fullName) && !customerLookup.ContainsKey(fullName))
                    customerLookup[fullName] = m.Id;
                if (!string.IsNullOrEmpty(firstName) && !customerLookup.ContainsKey(firstName))
                    customerLookup[firstName] = m.Id;
            }
        }

        // Entity'leri hazırla
        foreach (var row in validRows)
        {
            try
            {
                // Duplicate kontrolü (memory'den)
                var policyKey = $"{row.PoliceNo}_{GetZeyilNoAsInt(row.ZeyilNo)}";
                if (existingPolicyKeys.Contains(policyKey))
                {
                    duplicateCount++;
                    continue;
                }
                existingPolicyKeys.Add(policyKey);

                // Müşteri kontrolü (memory'den)
                var musteriId = row.MusteriId;
                if (!musteriId.HasValue && !string.IsNullOrEmpty(row.SigortaliAdi))
                {
                    var sigortaliAdi = row.SigortaliAdi.Trim().ToUpperInvariant();
                    if (customerLookup.TryGetValue(sigortaliAdi, out var foundMusteriId))
                    {
                        musteriId = foundMusteriId;
                    }
                }

                // PoliceHavuz kaydı oluştur
                var policeHavuz = new PoliceHavuz
                {
                    PoliceTipi = row.PoliceTipi ?? "TAHAKKUK",
                    PoliceNo = row.PoliceNo ?? string.Empty,
                    Plaka = row.Plaka ?? string.Empty,
                    ZeyilNo = GetZeyilNoAsInt(row.ZeyilNo),
                    YenilemeNo = GetYenilemeNoAsSbyte(row.YenilemeNo),
                    SigortaSirketiId = session.SigortaSirketiId,
                    TanzimTarihi = row.ZeyilOnayTarihi ?? row.TanzimTarihi ?? row.BaslangicTarihi ?? _dateTimeService.Now,
                    BaslangicTarihi = row.BaslangicTarihi ?? _dateTimeService.Now,
                    BitisTarihi = row.BitisTarihi ?? row.BaslangicTarihi?.AddYears(1) ?? _dateTimeService.Now.AddYears(1),
                    BrutPrim = row.BrutPrim ?? 0,
                    NetPrim = row.NetPrim ?? row.BrutPrim ?? 0,
                    Vergi = 0,
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

        // ExecutionStrategy ile transaction (MySqlRetryingExecutionStrategy uyumlu)
        _logger.LogInformation("SaveChanges başlatılıyor: {Count} kayıt eklenecek", successCount);
        var strategy = _context.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            });
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            _logger.LogError(ex, "Import kaydetme hatası. Detay: {Detail}", innerMsg);
            return new ExcelImportResultDto
            {
                Success = false,
                ErrorMessage = $"Veritabanına kaydetme hatası: {innerMsg}"
            };
        }

        // Session ve temp dosyayı temizle
        DeleteTempFile(session.TempFilePath);
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
            Errors = errors,
            TotalValidRows = validRows.Count,
            ProcessedSoFar = validRows.Count,
            IsCompleted = true,
            HasMoreBatches = false
        };
    }

    public async Task<ExcelImportResultDto> ConfirmImportBatchAsync(string sessionId, int skip, int take)
    {
        _logger.LogInformation("Batch import: SessionId={SessionId}, Skip={Skip}, Take={Take}", sessionId, skip, take);

        if (!_cache.TryGetValue<ImportSessionData>(sessionId, out var session) || session == null)
        {
            return new ExcelImportResultDto
            {
                Success = false,
                ErrorMessage = "Oturum bulunamadı veya süresi dolmuş. Lütfen dosyayı tekrar yükleyin."
            };
        }

        // Session sahiplik kontrolü
        if (!string.IsNullOrEmpty(session.UserId) && session.UserId != _currentUserService.UserId)
        {
            _logger.LogWarning("Batch session sahiplik hatası: SessionUserId={SessionUserId}, CurrentUserId={CurrentUserId}",
                session.UserId, _currentUserService.UserId);
            return new ExcelImportResultDto
            {
                Success = false,
                ErrorMessage = "Bu oturum size ait değil."
            };
        }

        var errors = new List<ExcelImportErrorDto>();
        var successCount = 0;
        var duplicateCount = 0;

        // Satırları al (ilk batch: temp dosyadan, sonraki batch'ler: hafızadan)
        var allRows = GetSessionRows(session);
        var allValidRows = allRows.Where(r => r.IsValid).ToList();
        var totalValidRows = allValidRows.Count;

        // Batch'i al
        var batchRows = allValidRows.Skip(skip).Take(take).ToList();
        var processedSoFar = Math.Min(skip + take, totalValidRows);
        var hasMoreBatches = processedSoFar < totalValidRows;

        // Batch boşsa (işlem bitti)
        if (batchRows.Count == 0)
        {
            // Son batch ise session ve temp dosyayı temizle
            if (!hasMoreBatches)
            {
                DeleteTempFile(session.TempFilePath);
                _cache.Remove(sessionId);
            }

            return new ExcelImportResultDto
            {
                Success = true,
                TotalProcessed = 0,
                SuccessCount = 0,
                FailedCount = 0,
                DuplicateCount = 0,
                Errors = errors,
                TotalValidRows = totalValidRows,
                ProcessedSoFar = processedSoFar,
                IsCompleted = !hasMoreBatches,
                HasMoreBatches = hasMoreBatches
            };
        }

        // ===== DB LOOKUP: İlk batch'te yükle, sonrakiler cache'ten kullansın =====
        if (session.CachedPolicyKeys == null)
        {
            // Tüm geçerli poliçe numaralarını topla (tüm batch'ler için)
            var allPoliceNos = allValidRows
                .Where(r => !string.IsNullOrEmpty(r.PoliceNo))
                .Select(r => r.PoliceNo!)
                .Distinct()
                .ToList();

            var existingPolicies = await _context.PoliceHavuzlari
                .Where(p => allPoliceNos.Contains(p.PoliceNo) && p.SigortaSirketiId == session.SigortaSirketiId)
                .Select(p => new { p.PoliceNo, p.ZeyilNo })
                .ToListAsync();

            session.CachedPolicyKeys = existingPolicies
                .Select(p => $"{p.PoliceNo}_{p.ZeyilNo}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        if (session.CachedCustomerLookup == null)
        {
            session.CachedCustomerLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var musteriler = await _context.Musteriler
                .Where(m => m.Adi != null)
                .Select(m => new { m.Id, m.Adi, m.Soyadi })
                .ToListAsync();

            foreach (var m in musteriler)
            {
                var fullName = ((m.Adi ?? "") + " " + (m.Soyadi ?? "")).Trim().ToUpperInvariant();
                var firstName = (m.Adi ?? "").Trim().ToUpperInvariant();
                if (!string.IsNullOrEmpty(fullName) && !session.CachedCustomerLookup.ContainsKey(fullName))
                    session.CachedCustomerLookup[fullName] = m.Id;
                if (!string.IsNullOrEmpty(firstName) && !session.CachedCustomerLookup.ContainsKey(firstName))
                    session.CachedCustomerLookup[firstName] = m.Id;
            }
        }

        var existingPolicyKeys = session.CachedPolicyKeys;
        var customerLookup = session.CachedCustomerLookup;
        // ===== LOOKUP SONU =====

        // Entity'leri hazırla
        foreach (var row in batchRows)
        {
            try
            {
                // Duplicate kontrolü (memory'den)
                var policyKey = $"{row.PoliceNo}_{GetZeyilNoAsInt(row.ZeyilNo)}";
                if (existingPolicyKeys.Contains(policyKey))
                {
                    duplicateCount++;
                    continue;
                }

                // Yeni eklenen poliçeyi de set'e ekle (aynı batch içinde duplicate önleme)
                existingPolicyKeys.Add(policyKey);

                // Müşteri kontrolü (memory'den)
                var musteriId = row.MusteriId;
                if (!musteriId.HasValue && !string.IsNullOrEmpty(row.SigortaliAdi))
                {
                    var sigortaliAdi = row.SigortaliAdi.Trim().ToUpperInvariant();
                    if (customerLookup.TryGetValue(sigortaliAdi, out var foundMusteriId))
                    {
                        musteriId = foundMusteriId;
                    }
                }

                // PoliceHavuz kaydı oluştur
                var policeHavuz = new PoliceHavuz
                {
                    PoliceTipi = row.PoliceTipi ?? "TAHAKKUK",
                    PoliceNo = row.PoliceNo ?? string.Empty,
                    Plaka = row.Plaka ?? string.Empty,
                    ZeyilNo = GetZeyilNoAsInt(row.ZeyilNo),
                    YenilemeNo = GetYenilemeNoAsSbyte(row.YenilemeNo),
                    SigortaSirketiId = session.SigortaSirketiId,
                    TanzimTarihi = row.ZeyilOnayTarihi ?? row.TanzimTarihi ?? row.BaslangicTarihi ?? _dateTimeService.Now,
                    BaslangicTarihi = row.BaslangicTarihi ?? _dateTimeService.Now,
                    BitisTarihi = row.BitisTarihi ?? row.BaslangicTarihi?.AddYears(1) ?? _dateTimeService.Now.AddYears(1),
                    BrutPrim = row.BrutPrim ?? 0,
                    NetPrim = row.NetPrim ?? row.BrutPrim ?? 0,
                    Vergi = 0,
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
                _logger.LogError(ex, "Batch satır import hatası: {RowNumber}", row.RowNumber);
                errors.Add(new ExcelImportErrorDto
                {
                    RowNumber = row.RowNumber,
                    PoliceNo = row.PoliceNo,
                    ErrorMessage = ex.Message
                });
            }
        }

        // SaveChangesAsync zaten implicit transaction kullanır (tek SaveChanges = atomik)
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            _logger.LogError(ex, "Batch kaydetme hatası. Detay: {Detail}", innerMsg);
            return new ExcelImportResultDto
            {
                Success = false,
                ErrorMessage = $"Veritabanına kaydetme hatası: {innerMsg}"
            };
        }

        // Son batch ise session ve temp dosyayı temizle, değilse timeout'u uzat
        if (!hasMoreBatches)
        {
            DeleteTempFile(session.TempFilePath);
            _cache.Remove(sessionId);
        }
        else
        {
            // Devam eden batch'ler için session süresini uzat
            _cache.Set(sessionId, session, CreateCacheOptions());
        }

        _logger.LogInformation(
            "Batch tamamlandı: {SuccessCount} başarılı, {FailedCount} hatalı, {DuplicateCount} duplicate, Progress: {ProcessedSoFar}/{TotalValidRows}",
            successCount, errors.Count, duplicateCount, processedSoFar, totalValidRows);

        return new ExcelImportResultDto
        {
            Success = true,
            TotalProcessed = batchRows.Count,
            SuccessCount = successCount,
            FailedCount = errors.Count,
            DuplicateCount = duplicateCount,
            NewCustomersCreated = 0,
            Errors = errors,
            TotalValidRows = totalValidRows,
            ProcessedSoFar = processedSoFar,
            IsCompleted = !hasMoreBatches,
            HasMoreBatches = hasMoreBatches
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

            // Parser'a göre not belirle
            string? notes = null;
            if (parser.SirketAdi.Contains("Sompo"))
            {
                notes = "Header 2. satırda, ilk satır firma bilgisi";
            }
            else if (parser.SirketAdi.Contains("Quick"))
            {
                notes = "Excel (.xlsx, .xls) ve XML formatları desteklenir";
            }

            formats.Add(new SupportedFormatDto
            {
                SigortaSirketiId = sirket?.Id ?? parser.SigortaSirketiId,
                SigortaSirketiAdi = sirket?.Ad ?? parser.SirketAdi,
                FormatDescription = $"{parser.SirketAdi} Excel formatı",
                RequiredColumns = new List<string> { "Poliçe No", "Brüt Prim", "Başlangıç Tarihi" },
                Notes = notes
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
        var normalizedFileName = TurkishStringHelper.Normalize(fileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        _logger.LogDebug("Dosya adından şirket algılama: {FileName} → normalized: {Normalized}", fileName, normalizedFileName);

        // XML dosyası için XML parser'ları kontrol et
        if (extension == ".xml")
        {
            if (_unicoXmlParser.CanParse(fileName, Enumerable.Empty<string>()))
            {
                _logger.LogDebug("XML algılama: Unico eşleşti (ID: {Id})", _unicoXmlParser.SigortaSirketiId);
                return _unicoXmlParser.SigortaSirketiId;
            }
            if (_quickXmlParser.CanParseXml(fileName))
            {
                _logger.LogDebug("XML algılama: Quick eşleşti (ID: {Id})", _quickXmlParser.SigortaSirketiId);
                return _quickXmlParser.SigortaSirketiId;
            }
        }

        // Excel parser'ları için Türkçe karakter normalizasyonu ile pattern eşleştirme
        foreach (var parser in _parsers)
        {
            foreach (var pattern in parser.FileNamePatterns)
            {
                var normalizedPattern = TurkishStringHelper.Normalize(pattern);
                if (normalizedFileName.Contains(normalizedPattern))
                {
                    _logger.LogDebug("Excel algılama: {Parser} eşleşti (pattern: {Pattern}, ID: {Id})",
                        parser.SirketAdi, pattern, parser.SigortaSirketiId);
                    return parser.SigortaSirketiId;
                }
            }
        }

        _logger.LogDebug("Dosya adından şirket algılanamadı: {FileName}", fileName);
        return null;
    }

    public async Task<DetectFormatResultDto> DetectFormatFromHeadersAsync(string fileName, List<string> headers)
    {
        _logger.LogInformation("Format tespit ediliyor: FileName={FileName}, Headers={Headers}",
            fileName, string.Join(", ", headers.Take(10)));

        // 1. Önce dosya adından tespit dene
        var normalizedFileName = TurkishStringHelper.Normalize(fileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        // XML dosyası için XML parser'ları kontrol et
        if (extension == ".xml")
        {
            if (_unicoXmlParser.CanParse(fileName, Enumerable.Empty<string>()))
            {
                var (dbId, dbName) = await GetDatabaseSirketInfo(_unicoXmlParser.SigortaSirketiId, _unicoXmlParser.SirketAdi);
                return new DetectFormatResultDto
                {
                    Detected = true,
                    SigortaSirketiId = dbId,
                    SigortaSirketiAdi = dbName,
                    DetectionMethod = "filename"
                };
            }
            if (_quickXmlParser.CanParseXml(fileName))
            {
                var (dbId, dbName) = await GetDatabaseSirketInfo(_quickXmlParser.SigortaSirketiId, _quickXmlParser.SirketAdi);
                return new DetectFormatResultDto
                {
                    Detected = true,
                    SigortaSirketiId = dbId,
                    SigortaSirketiAdi = dbName,
                    DetectionMethod = "filename"
                };
            }
        }

        // Dosya adından parser ara
        foreach (var parser in _parsers)
        {
            if (parser.FileNamePatterns.Any(p => normalizedFileName.Contains(TurkishStringHelper.Normalize(p))))
            {
                _logger.LogInformation("Dosya adından tespit edildi: {Parser}", parser.SirketAdi);
                var (dbId, dbName) = await GetDatabaseSirketInfo(parser);
                return new DetectFormatResultDto
                {
                    Detected = true,
                    SigortaSirketiId = dbId,
                    SigortaSirketiAdi = dbName,
                    DetectionMethod = "filename"
                };
            }
        }

        // 2. Header'lardan tespit dene (CanParse metodu ile)
        if (headers.Count > 0)
        {
            foreach (var parser in _parsers)
            {
                if (parser.CanParse(fileName, headers))
                {
                    _logger.LogInformation("Header'lardan tespit edildi: {Parser}", parser.SirketAdi);
                    var (dbId, dbName) = await GetDatabaseSirketInfo(parser);
                    return new DetectFormatResultDto
                    {
                        Detected = true,
                        SigortaSirketiId = dbId,
                        SigortaSirketiAdi = dbName,
                        DetectionMethod = "headers"
                    };
                }
            }
        }

        // Tespit edilemedi
        _logger.LogWarning("Format tespit edilemedi: {FileName}", fileName);
        return new DetectFormatResultDto
        {
            Detected = false,
            Message = "Dosya formatı otomatik tespit edilemedi. Lütfen sigorta şirketini manuel olarak seçin."
        };
    }

    /// <summary>
    /// Parser için database'deki şirket ID ve adını bulur (GetSupportedFormatsAsync ile tutarlılık için)
    /// </summary>
    private async Task<(int Id, string Name)> GetDatabaseSirketInfo(IExcelParser parser)
    {
        return await GetDatabaseSirketInfo(parser.SigortaSirketiId, parser.SirketAdi);
    }

    /// <summary>
    /// Şirket adından database ID ve adını bulur
    /// </summary>
    private async Task<(int Id, string Name)> GetDatabaseSirketInfo(int parserId, string parserSirketAdi)
    {
        var searchName = parserSirketAdi.ToLower().Split(' ')[0];
        var sirket = await _context.SigortaSirketleri
            .FirstOrDefaultAsync(s => s.Ad.ToLower().Contains(searchName));

        return (sirket?.Id ?? parserId, sirket?.Ad ?? parserSirketAdi);
    }

    /// <summary>
    /// İçerik bazlı parser tespiti - header kolonlarına bakarak en uygun parser'ı bulur
    /// </summary>
    private Task<IExcelParser?> DetectParserFromContentAsync(Stream fileStream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var headers = new List<string>();

        try
        {
            if (extension == ".xlsx")
            {
                using var package = new ExcelPackage(fileStream);
                var worksheet = package.Workbook.Worksheets.FirstOrDefault();

                if (worksheet == null || worksheet.Dimension == null)
                    return Task.FromResult<IExcelParser?>(null);

                // Her parser için header satırını dene ve kolonları kontrol et
                foreach (var parser in _parsers)
                {
                    var headerRow = DetectHeaderRow(worksheet, parser);
                    headers.Clear();

                    for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                    {
                        var cellValue = worksheet.Cells[headerRow, col].Value?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(cellValue))
                            headers.Add(cellValue);
                    }

                    if (headers.Count > 0 && parser.CanParse(fileName, headers))
                    {
                        _logger.LogInformation("İçerik bazlı tespit: {Parser} parser'ı seçildi (header row: {Row})",
                            parser.SirketAdi, headerRow);
                        return Task.FromResult<IExcelParser?>(parser);
                    }
                }
            }
            else if (extension == ".xls")
            {
                // LeaveOpen = true ile stream'in kapatılmasını önle
                using var reader = ExcelReaderFactory.CreateReader(fileStream, new ExcelReaderConfiguration
                {
                    LeaveOpen = true
                });
                using var result = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
                });

                if (result.Tables.Count == 0)
                    return Task.FromResult<IExcelParser?>(null);

                var table = result.Tables[0];

                // Her parser için header satırını dene
                foreach (var parser in _parsers)
                {
                    var headerRowIdx = DetectHeaderRowFromDataTable(table, parser);
                    if (headerRowIdx > table.Rows.Count)
                        continue;

                    headers.Clear();
                    var headerRow = table.Rows[headerRowIdx - 1];

                    for (int col = 0; col < table.Columns.Count; col++)
                    {
                        var cellValue = headerRow[col]?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(cellValue) && cellValue != "Unnamed")
                            headers.Add(cellValue);
                    }

                    if (headers.Count > 0 && parser.CanParse(fileName, headers))
                    {
                        _logger.LogInformation("İçerik bazlı tespit (xls): {Parser} parser'ı seçildi (header row: {Row})",
                            parser.SirketAdi, headerRowIdx);
                        return Task.FromResult<IExcelParser?>(parser);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "İçerik bazlı parser tespiti sırasında hata oluştu");
        }

        return Task.FromResult<IExcelParser?>(null);
    }

    #region Private Methods

    /// <summary>
    /// XML dosyasını parse eder (Quick Sigorta ve Unico Sigorta XML formatları)
    /// Önce içerik bazlı algılama (root element), sonra dosya adı bazlı algılama yapar.
    /// </summary>
    private async Task<ExcelImportPreviewDto> ParseXmlFileAsync(Stream fileStream, string fileName, int? sigortaSirketiId)
    {
        _logger.LogInformation("XML parsing başlatılıyor: {FileName}", fileName);

        try
        {
            List<ExcelImportRowDto>? parsedRows = null;
            int detectedSirketId = 0;
            string detectedFormat = "Bilinmeyen XML Formatı";

            // 1. Önce içerik bazlı algılama dene (en güvenilir)
            var contentDetectedParser = DetectXmlParserFromContent(fileStream);
            fileStream.Position = 0; // Stream'i başa sar

            if (contentDetectedParser == "quick")
            {
                parsedRows = _quickXmlParser.ParseXml(fileStream);
                detectedSirketId = _quickXmlParser.SigortaSirketiId;
                detectedFormat = _quickXmlParser.SirketAdi;
                _logger.LogInformation("Quick XML parser kullanılıyor (içerik bazlı algılama)");
            }
            else if (contentDetectedParser == "unico")
            {
                parsedRows = _unicoXmlParser.ParseXml(fileStream);
                detectedSirketId = _unicoXmlParser.SigortaSirketiId;
                detectedFormat = _unicoXmlParser.SirketAdi;
                _logger.LogInformation("Unico XML parser kullanılıyor (içerik bazlı algılama)");
            }
            // 2. İçerik algılama başarısızsa, dosya adı bazlı algılama dene
            else if (_unicoXmlParser.CanParse(fileName, Enumerable.Empty<string>()) || sigortaSirketiId == _unicoXmlParser.SigortaSirketiId)
            {
                parsedRows = _unicoXmlParser.ParseXml(fileStream);
                detectedSirketId = _unicoXmlParser.SigortaSirketiId;
                detectedFormat = _unicoXmlParser.SirketAdi;
                _logger.LogInformation("Unico XML parser kullanılıyor (dosya adı bazlı)");
            }
            else if (_quickXmlParser.CanParseXml(fileName) || sigortaSirketiId == _quickXmlParser.SigortaSirketiId)
            {
                parsedRows = _quickXmlParser.ParseXml(fileStream);
                detectedSirketId = _quickXmlParser.SigortaSirketiId;
                detectedFormat = _quickXmlParser.SirketAdi;
                _logger.LogInformation("Quick XML parser kullanılıyor (dosya adı bazlı)");
            }

            // Parser bulunduysa sonuçları işle
            if (parsedRows != null)
            {
                if (parsedRows.Count == 0)
                {
                    return new ExcelImportPreviewDto
                    {
                        TotalRows = 0,
                        ValidRows = 0,
                        InvalidRows = 0,
                        Rows = new List<ExcelImportRowDto>(),
                        DetectedFormat = detectedFormat
                    };
                }

                // Müşteri ve branş eşleştirmelerini yap
                await EnrichRowsWithLookupDataAsync(parsedRows);

                // Session oluştur - satırları temp dosyaya yaz (bellek tasarrufu)
                var sessionId = Guid.NewGuid().ToString();
                var tempFilePath = SaveRowsToTempFile(parsedRows);
                var cacheEntry = new ImportSessionData
                {
                    SessionId = sessionId,
                    UserId = _currentUserService.UserId ?? string.Empty,
                    FileName = fileName,
                    SigortaSirketiId = sigortaSirketiId ?? detectedSirketId,
                    TempFilePath = tempFilePath,
                    TotalRowCount = parsedRows.Count,
                    ValidRowCount = parsedRows.Count(r => r.IsValid),
                    CreatedAt = _dateTimeService.Now
                };

                // Cache'e kaydet (30 dakika, PostEviction ile temp dosya temizlenir)
                _cache.Set(sessionId, cacheEntry, CreateCacheOptions());

                // Sigorta şirketi adını al
                var sirket = await _context.SigortaSirketleri
                    .FirstOrDefaultAsync(s => s.Id == cacheEntry.SigortaSirketiId);

                _logger.LogInformation("XML parsing tamamlandı: {Count} satır", parsedRows.Count);

                return new ExcelImportPreviewDto
                {
                    TotalRows = parsedRows.Count,
                    ValidRows = parsedRows.Count(r => r.IsValid),
                    InvalidRows = parsedRows.Count(r => !r.IsValid),
                    Rows = parsedRows,
                    ImportSessionId = sessionId,
                    FileName = fileName,
                    SigortaSirketiId = cacheEntry.SigortaSirketiId,
                    SigortaSirketiAdi = sirket?.Ad ?? detectedFormat,
                    DetectedFormat = detectedFormat
                };
            }

            // Desteklenmeyen XML formatı
            _logger.LogWarning("Desteklenmeyen XML formatı: {FileName}", fileName);
            return new ExcelImportPreviewDto
            {
                TotalRows = 0,
                ValidRows = 0,
                InvalidRows = 0,
                Rows = new List<ExcelImportRowDto>(),
                DetectedFormat = "Bilinmeyen XML Formatı"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "XML parsing hatası: {FileName}", fileName);
            throw;
        }
    }

    /// <summary>
    /// XML içeriğinden parser türünü algılar (root element ve yapı kontrolü).
    /// Quick: PoliceTransferDto root veya Policeler/AcenteBilgiler elementleri
    /// Unico: Policy elementleri veya UnicoXML root
    /// </summary>
    private string? DetectXmlParserFromContent(Stream xmlStream)
    {
        try
        {
            var position = xmlStream.Position;
            var xmlSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using var xmlReader = XmlReader.Create(xmlStream, xmlSettings);
            var doc = XDocument.Load(xmlReader);
            xmlStream.Position = position;

            var root = doc.Root;
            if (root == null)
                return null;

            // Quick Sigorta XML yapısı kontrolü
            // Root: PoliceTransferDto veya Policeler/AcenteBilgiler elementleri
            if (root.Name.LocalName == "PoliceTransferDto" ||
                root.Element("Policeler") != null ||
                root.Element("AcenteBilgiler") != null ||
                root.Descendants("PoliceNo").Any() && root.Descendants("AcenteKomisyon").Any())
            {
                _logger.LogDebug("XML içerik algılama: Quick formatı tespit edildi (root: {Root})", root.Name.LocalName);
                return "quick";
            }

            // Unico Sigorta XML yapısı kontrolü
            // Root: UnicoXML veya Policy elementleri ile ProductNo/GrossPremium
            if (root.Name.LocalName == "UnicoXML" ||
                root.Descendants("Policy").Any() && root.Descendants("ProductNo").Any() ||
                root.Descendants("Policy").Any() && root.Descendants("GrossPremium").Any())
            {
                _logger.LogDebug("XML içerik algılama: Unico formatı tespit edildi (root: {Root})", root.Name.LocalName);
                return "unico";
            }

            _logger.LogDebug("XML içerik algılama: Tanınmayan format (root: {Root})", root.Name.LocalName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "XML içerik algılama hatası");
            return null;
        }
    }

    private Task<Dictionary<string, List<IDictionary<string, object?>>>> ReadAdditionalSheetsAsync(
        Stream fileStream, string fileName, IExcelParser parser)
    {
        var result = new Dictionary<string, List<IDictionary<string, object?>>>();

        if (parser.AdditionalSheetNames == null || parser.AdditionalSheetNames.Length == 0)
            return Task.FromResult(result);

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (extension == ".xlsx")
        {
            using var package = new ExcelPackage(fileStream);

            foreach (var sheetName in parser.AdditionalSheetNames)
            {
                var worksheet = package.Workbook.Worksheets.FirstOrDefault(w =>
                    w.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase));

                if (worksheet == null || worksheet.Dimension == null)
                {
                    _logger.LogWarning("Ek sayfa bulunamadı: {SheetName}", sheetName);
                    continue;
                }

                var rows = new List<IDictionary<string, object?>>();

                // Header'ları oku (ilk satır)
                var headers = new List<string>();
                for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                {
                    var cellValue = worksheet.Cells[1, col].Value?.ToString()?.Trim();
                    headers.Add(string.IsNullOrEmpty(cellValue) ? $"Column_{col}" : cellValue);
                }

                // Veri satırlarını oku
                for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
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

                    if (hasData)
                        rows.Add(rowData);
                }

                result[sheetName] = rows;
                _logger.LogInformation("Ek sayfa okundu: {SheetName}, {RowCount} satır", sheetName, rows.Count);
            }
        }
        else if (extension == ".xls")
        {
            using var reader = ExcelReaderFactory.CreateReader(fileStream, new ExcelReaderConfiguration
            {
                LeaveOpen = true
            });
            using var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
            });

            foreach (var sheetName in parser.AdditionalSheetNames)
            {
                var table = dataSet.Tables.Cast<System.Data.DataTable>()
                    .FirstOrDefault(t => t.TableName.Equals(sheetName, StringComparison.OrdinalIgnoreCase));

                if (table == null || table.Rows.Count < 2)
                {
                    _logger.LogWarning("Ek sayfa bulunamadı (xls): {SheetName}", sheetName);
                    continue;
                }

                var rows = new List<IDictionary<string, object?>>();

                // Header'ları oku (ilk satır)
                var headers = new List<string>();
                var headerRow = table.Rows[0];
                for (int col = 0; col < table.Columns.Count; col++)
                {
                    var header = headerRow[col]?.ToString()?.Trim();
                    headers.Add(string.IsNullOrEmpty(header) ? $"Column_{col}" : header);
                }

                // Veri satırlarını oku
                for (int rowIdx = 1; rowIdx < table.Rows.Count; rowIdx++)
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

                    if (hasData)
                        rows.Add(rowData);
                }

                result[sheetName] = rows;
                _logger.LogInformation("Ek sayfa okundu (xls): {SheetName}, {RowCount} satır", sheetName, rows.Count);
            }
        }

        return Task.FromResult(result);
    }

    private async Task<List<IDictionary<string, object?>>> ReadExcelFileAsync(Stream fileStream, string fileName, IExcelParser parser)
    {
        var rows = new List<IDictionary<string, object?>>();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (extension == ".xlsx")
        {
            using var package = new ExcelPackage(fileStream);

            // Parser'da MainSheetName varsa onu kullan, yoksa ilk sayfayı al
            ExcelWorksheet? worksheet;
            if (!string.IsNullOrEmpty(parser.MainSheetName))
            {
                worksheet = package.Workbook.Worksheets.FirstOrDefault(w =>
                    w.Name.Equals(parser.MainSheetName, StringComparison.OrdinalIgnoreCase));
                if (worksheet == null)
                {
                    _logger.LogWarning("Ana sayfa bulunamadı: {SheetName}, ilk sayfa kullanılıyor", parser.MainSheetName);
                    worksheet = package.Workbook.Worksheets.FirstOrDefault();
                }
            }
            else
            {
                worksheet = package.Workbook.Worksheets.FirstOrDefault();
            }

            if (worksheet == null || worksheet.Dimension == null)
                return rows;

            // Header satırını dinamik olarak tespit et
            var headerRowIndex = DetectHeaderRow(worksheet, parser);
            _logger.LogInformation("Detected header row: {HeaderRow} for parser: {Parser}", headerRowIndex, parser.SirketAdi);

            // Header'ları oku
            var headers = new List<string>();
            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
            {
                var cellValue = worksheet.Cells[headerRowIndex, col].Value?.ToString()?.Trim();
                headers.Add(string.IsNullOrEmpty(cellValue) ? $"Column_{col}" : cellValue);
            }

            _logger.LogInformation("Headers found: {Headers}", string.Join(", ", headers));

            // Veri satırlarını oku
            for (int row = headerRowIndex + 1; row <= worksheet.Dimension.End.Row; row++)
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
                if (hasData && !IsSkippableRow(rowData))
                {
                    rows.Add(rowData);
                }
            }
        }
        else if (extension == ".xls")
        {
            // .xls için önce tüm veriyi oku, sonra header satırını tespit et
            using var reader = ExcelReaderFactory.CreateReader(fileStream, new ExcelReaderConfiguration
            {
                LeaveOpen = true
            });
            using var result = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
            });

            if (result.Tables.Count == 0)
                return rows;

            var table = result.Tables[0];

            // Header satırını dinamik olarak tespit et
            var headerRowIndex = DetectHeaderRowFromDataTable(table, parser);
            _logger.LogInformation("Detected header row (xls): {HeaderRow} for parser: {Parser}", headerRowIndex, parser.SirketAdi);

            if (table.Rows.Count < headerRowIndex)
                return rows;

            // Header satırını al (0-indexed)
            var headers = new List<string>();
            var headerRow = table.Rows[headerRowIndex - 1];

            for (int col = 0; col < table.Columns.Count; col++)
            {
                var header = headerRow[col]?.ToString()?.Trim();
                headers.Add(string.IsNullOrEmpty(header) ? $"Column_{col}" : header);
            }

            _logger.LogInformation("Headers found (xls): {Headers}", string.Join(", ", headers));

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

                if (hasData && !IsSkippableRow(rowData))
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

    private bool IsSkippableRow(IDictionary<string, object?> row)
    {
        // Özet satırlarını atla (Tahakkuk, İptal, Net, Brüt Prim gibi)
        var firstValue = row.Values.FirstOrDefault()?.ToString()?.ToUpperInvariant();

        if (string.IsNullOrEmpty(firstValue))
            return true;

        // Section marker'ları ATLAMA - parser'ın bunları işlemesine izin ver
        // AK Sigorta formatı: "TAHAKKUK/IPTAL : Tahakkuk" veya "TAHAKKUK/IPTAL : Iptal"
        if (firstValue.Contains("TAHAKKUK/IPTAL") || firstValue.Contains("TAHAKKUK/İPTAL"))
            return false;

        var skipKeywords = new[] { "TAHAKKUK", "İPTAL", "IPTAL", "NET", "BRÜT", "BRUT", "TOPLAM", "NET PRİM", "BRÜT PRİM", "GENEL TOPLAM" };

        if (skipKeywords.Any(k => firstValue.Contains(k)))
            return true;

        // Tarih aralığı satırını atla (örn: "01/01/2024 - 31/12/2024")
        if (firstValue.Contains("/") && firstValue.Contains("-"))
            return true;

        return false;
    }

    /// <summary>
    /// Türkçe karakterleri ASCII karşılıklarına dönüştürür (header tespiti için)
    /// ToUpperInvariant() Türkçe 'i'→'I' yapar (İ değil), bu yüzden ayrıca normalize gerekli
    /// </summary>
    private static string NormalizeForHeaderDetection(string value)
    {
        return value
            .ToUpperInvariant()
            .Replace("İ", "I")   // Türkçe büyük İ → I
            .Replace("Ç", "C")
            .Replace("Ş", "S")
            .Replace("Ö", "O")
            .Replace("Ü", "U")
            .Replace("Ğ", "G");
    }

    /// <summary>
    /// Excel worksheet için header satırını tespit eder (EPPlus - 1-indexed)
    /// </summary>
    private int DetectHeaderRow(ExcelWorksheet worksheet, IExcelParser parser)
    {
        // 1. Parser'dan explicit row varsa kullan
        if (parser.HeaderRowIndex.HasValue)
            return parser.HeaderRowIndex.Value;

        // 2. İlk 10 satırda header keyword'lerini ara
        // Tüm keyword'ler ASCII - NormalizeForHeaderDetection ile karşılaştırılır
        var headerKeywords = new[] { "POLICE", "POLICE NO", "PRIM" };

        for (int row = 1; row <= Math.Min(10, worksheet.Dimension.End.Row); row++)
        {
            for (int col = 1; col <= Math.Min(15, worksheet.Dimension.End.Column); col++)
            {
                var val = worksheet.Cells[row, col].Value?.ToString();
                if (val != null)
                {
                    var normalized = NormalizeForHeaderDetection(val);
                    if (headerKeywords.Any(k => normalized.Contains(k)))
                    {
                        _logger.LogInformation("Auto-detected header row at {Row} based on keyword match: {Value}", row, val);
                        return row;
                    }
                }
            }
        }

        return 1; // Default: ilk satır
    }

    /// <summary>
    /// DataTable için header satırını tespit eder (.xls format - 1-indexed döndürür)
    /// </summary>
    private int DetectHeaderRowFromDataTable(System.Data.DataTable table, IExcelParser parser)
    {
        // 1. Parser'dan explicit row varsa kullan
        if (parser.HeaderRowIndex.HasValue)
            return parser.HeaderRowIndex.Value;

        // 2. İlk 10 satırda header keyword'lerini ara
        var headerKeywords = new[] { "POLICE", "POLICE NO", "PRIM" };

        for (int rowIdx = 0; rowIdx < Math.Min(10, table.Rows.Count); rowIdx++)
        {
            var dataRow = table.Rows[rowIdx];
            for (int col = 0; col < Math.Min(15, table.Columns.Count); col++)
            {
                var val = dataRow[col]?.ToString();
                if (val != null)
                {
                    var normalized = NormalizeForHeaderDetection(val);
                    if (headerKeywords.Any(k => normalized.Contains(k)))
                    {
                        _logger.LogInformation("Auto-detected header row (xls) at {Row} based on keyword match: {Value}", rowIdx + 1, val);
                        return rowIdx + 1; // 1-indexed döndür
                    }
                }
            }
        }

        return 1; // Default: ilk satır
    }

    private async Task EnrichRowsWithLookupDataAsync(List<ExcelImportRowDto> rows)
    {
        // Parser zaten BransId'yi DetectBransIdFromUrunAdi ile belirledi
        // Sadece BransId null olan satırlar için PoliceTurleri tablosundan eşleştirme yap
        var policeTurleri = await _context.PoliceTurleri.ToListAsync();
        _logger.LogInformation("EnrichRows: PoliceTurleri count={Count}", policeTurleri.Count);

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            // Parser BransId verdiyse dokunma
            if (row.BransId.HasValue)
            {
                _logger.LogDebug("EnrichRows Row {Row}: BransId already set to {BransId}, skipping", row.RowNumber, row.BransId);
                continue;
            }

            // BransId yoksa Brans string'inden PoliceTurleri tablosundan bulmaya çalış
            if (!string.IsNullOrEmpty(row.Brans))
            {
                var bransUpper = row.Brans.ToUpperInvariant();

                var policeTuru = policeTurleri.FirstOrDefault(p =>
                    p.Turu != null && bransUpper.Contains(p.Turu.ToUpperInvariant()));

                if (policeTuru != null)
                {
                    _logger.LogInformation("EnrichRows Row {Row}: BransId was null, matched '{Brans}' to PoliceTuru '{Turu}' (Id={Id})",
                        row.RowNumber, row.Brans, policeTuru.Turu, policeTuru.Id);
                    rows[i] = rows[i] with { BransId = policeTuru.Id };
                }
                else
                {
                    _logger.LogWarning("EnrichRows Row {Row}: BransId null, no match found for '{Brans}'", row.RowNumber, row.Brans);
                }
            }
        }
    }

    // ---------------------------------------------------------------
    // Temp dosya yönetimi
    // ---------------------------------------------------------------

    private string SaveRowsToTempFile(List<ExcelImportRowDto> rows)
    {
        Directory.CreateDirectory(TempDirectory);
        var filePath = Path.Combine(TempDirectory, $"{Guid.NewGuid()}.json");
        var json = JsonSerializer.Serialize(rows);
        File.WriteAllText(filePath, json, Encoding.UTF8);
        _logger.LogInformation("Temp dosya yazıldı: {Path}, {Count} satır", filePath, rows.Count);
        return filePath;
    }

    /// <summary>
    /// Session'dan satırları al: önce CachedRows, yoksa temp dosyadan oku ve cache'le
    /// </summary>
    private List<ExcelImportRowDto> GetSessionRows(ImportSessionData session)
    {
        if (session.CachedRows != null)
            return session.CachedRows;

        var rows = ReadRowsFromTempFile(session.TempFilePath);
        session.CachedRows = rows; // Sonraki batch'ler için hafızada tut
        return rows;
    }

    private List<ExcelImportRowDto> ReadRowsFromTempFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            _logger.LogWarning("Temp dosya bulunamadı: {Path}", filePath);
            return new List<ExcelImportRowDto>();
        }

        var json = File.ReadAllText(filePath, Encoding.UTF8);
        return JsonSerializer.Deserialize<List<ExcelImportRowDto>>(json) ?? new List<ExcelImportRowDto>();
    }

    private void DeleteTempFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
        try
        {
            File.Delete(filePath);
            _logger.LogInformation("Temp dosya silindi: {Path}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Temp dosya silinemedi: {Path}", filePath);
        }
    }

    private MemoryCacheEntryOptions CreateCacheOptions()
    {
        return new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
            .RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                // Sadece timeout veya explicit Remove durumunda temp dosyayı sil
                // Replaced (batch timeout uzatma) sırasında SİLME - dosya hala lazım!
                if (reason == Microsoft.Extensions.Caching.Memory.EvictionReason.Replaced)
                    return;

                if (value is ImportSessionData session && !string.IsNullOrEmpty(session.TempFilePath))
                {
                    try
                    {
                        if (File.Exists(session.TempFilePath))
                            File.Delete(session.TempFilePath);
                    }
                    catch { /* best effort */ }
                }
            });
    }

    private static void CleanupOldTempFiles()
    {
        try
        {
            if (!Directory.Exists(TempDirectory)) return;

            foreach (var file in Directory.GetFiles(TempDirectory, "*.json"))
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTimeUtc < DateTime.UtcNow.AddHours(-1))
                        fileInfo.Delete();
                }
                catch { /* best effort */ }
            }
        }
        catch { /* startup cleanup failure is not critical */ }
    }

    private static int GetZeyilNoAsInt(string? value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        if (int.TryParse(value, out var result))
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
    public string UserId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int SigortaSirketiId { get; set; }
    public string TempFilePath { get; set; } = string.Empty;
    public int TotalRowCount { get; set; }
    public int ValidRowCount { get; set; }
    public DateTime CreatedAt { get; set; }

    // Batch işlemi sırasında lazy-load (ilk batch'te doldurulur, sonrakiler yeniden kullanır)
    public List<ExcelImportRowDto>? CachedRows { get; set; }
    public HashSet<string>? CachedPolicyKeys { get; set; }
    public Dictionary<string, int>? CachedCustomerLookup { get; set; }
}
