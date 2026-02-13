using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Infrastructure.Services;

public class CustomerMatchingService : ICustomerMatchingService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;
    private readonly ILogger<CustomerMatchingService> _logger;

    public CustomerMatchingService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService,
        ILogger<CustomerMatchingService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
        _logger = logger;
    }

    public async Task<CustomerMatchResult> FindBestMatchAsync(CustomerMatchRequest request, CancellationToken ct = default)
    {
        var firmaId = request.FirmaId;

        // 1. TC Kimlik No ile eşleştir (en yüksek güvenilirlik)
        if (!string.IsNullOrWhiteSpace(request.TcKimlikNo) && IsValidTc(request.TcKimlikNo))
        {
            var musteri = await _context.Musteriler
                .FirstOrDefaultAsync(m =>
                    m.TcKimlikNo == request.TcKimlikNo &&
                    m.EkleyenFirmaId == firmaId, ct);

            if (musteri != null)
            {
                // Mevcut müşterinin boş alanlarını güncelle
                await UpdateExistingCustomerFieldsAsync(musteri, request, ct);
                return new CustomerMatchResult
                {
                    MusteriId = musteri.Id,
                    Confidence = MatchConfidence.Exact,
                    MatchedBy = MatchSignal.TcKimlikNo
                };
            }

            // TC var ama müşteri yok → otomatik oluştur
            var newMusteri = await AutoCreateCustomerAsync(request, request.TcKimlikNo, null, firmaId, ct);
            return new CustomerMatchResult
            {
                MusteriId = newMusteri.Id,
                Confidence = MatchConfidence.Exact,
                MatchedBy = MatchSignal.TcKimlikNo,
                AutoCreated = true
            };
        }

        // 2. Vergi No ile eşleştir (kurumsal müşteri)
        if (!string.IsNullOrWhiteSpace(request.VergiNo) && IsValidVkn(request.VergiNo))
        {
            var musteri = await _context.Musteriler
                .FirstOrDefaultAsync(m =>
                    m.VergiNo == request.VergiNo &&
                    m.EkleyenFirmaId == firmaId, ct);

            if (musteri != null)
            {
                // Mevcut müşterinin boş alanlarını güncelle
                await UpdateExistingCustomerFieldsAsync(musteri, request, ct);
                return new CustomerMatchResult
                {
                    MusteriId = musteri.Id,
                    Confidence = MatchConfidence.Exact,
                    MatchedBy = MatchSignal.VergiNo
                };
            }

            // VKN var ama müşteri yok → otomatik oluştur (kurumsal)
            var newMusteri = await AutoCreateCustomerAsync(request, null, request.VergiNo, firmaId, ct);
            return new CustomerMatchResult
            {
                MusteriId = newMusteri.Id,
                Confidence = MatchConfidence.Exact,
                MatchedBy = MatchSignal.VergiNo,
                AutoCreated = true
            };
        }

        // 3. Plaka ile eşleştir (son 2 yıldaki poliçelerden)
        if (!string.IsNullOrWhiteSpace(request.Plaka))
        {
            var twoYearsAgo = _dateTimeService.Now.AddYears(-2);
            var plakaMatch = await _context.Policeler
                .Where(p =>
                    p.Plaka == request.Plaka &&
                    p.MusteriId != null &&
                    p.FirmaId == firmaId &&
                    p.BaslangicTarihi >= twoYearsAgo)
                .OrderByDescending(p => p.BaslangicTarihi)
                .Select(p => new { p.MusteriId, p.SigortaliAdi })
                .FirstOrDefaultAsync(ct);

            if (plakaMatch?.MusteriId != null)
            {
                // İsim uyumu kontrolü: plakadaki kişi ile gelen isim uyuşuyor mu?
                if (!string.IsNullOrWhiteSpace(request.SigortaliAdi) &&
                    !string.IsNullOrWhiteSpace(plakaMatch.SigortaliAdi))
                {
                    var normalizedRequest = NormalizeTurkish(request.SigortaliAdi);
                    var normalizedExisting = NormalizeTurkish(plakaMatch.SigortaliAdi);

                    if (normalizedRequest != normalizedExisting)
                    {
                        // İsim uyuşmuyor - araç el değiştirmiş olabilir
                        return new CustomerMatchResult
                        {
                            Confidence = MatchConfidence.Low,
                            MatchedBy = MatchSignal.Plaka
                        };
                    }
                }

                return new CustomerMatchResult
                {
                    MusteriId = plakaMatch.MusteriId,
                    Confidence = MatchConfidence.Medium,
                    MatchedBy = MatchSignal.Plaka
                };
            }
        }

        // İsim ile eşleştirme yapılmaz - güvenilirliği düşük
        return new CustomerMatchResult
        {
            Confidence = MatchConfidence.None,
            MatchedBy = MatchSignal.None
        };
    }

    public async Task<Dictionary<int, CustomerMatchResult>> BatchMatchAsync(
        List<CustomerMatchRequest> requests, int firmaId, CancellationToken ct = default)
    {
        var results = new Dictionary<int, CustomerMatchResult>();
        if (requests.Count == 0) return results;

        // Firma müşterilerini toplu yükle (boş alan kontrolü için ek alanlar dahil)
        var musteriler = await _context.Musteriler
            .Where(m => m.EkleyenFirmaId == firmaId)
            .Select(m => new
            {
                m.Id, m.Adi, m.Soyadi, m.TcKimlikNo, m.VergiNo
            })
            .ToListAsync(ct);

        // Lookup dictionary'leri oluştur
        var tcLookup = new Dictionary<string, int>(StringComparer.Ordinal);
        var vknLookup = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var m in musteriler)
        {
            if (!string.IsNullOrWhiteSpace(m.TcKimlikNo) && !tcLookup.ContainsKey(m.TcKimlikNo))
                tcLookup[m.TcKimlikNo] = m.Id;

            if (!string.IsNullOrWhiteSpace(m.VergiNo) && !vknLookup.ContainsKey(m.VergiNo))
                vknLookup[m.VergiNo] = m.Id;
        }

        // Plaka lookup: son 2 yılda kullanılan plakalar
        var plakas = requests
            .Where(r => !string.IsNullOrWhiteSpace(r.Plaka))
            .Select(r => r.Plaka!)
            .Distinct()
            .ToList();

        var plakaLookup = new Dictionary<string, (int? MusteriId, string? SigortaliAdi)>(StringComparer.OrdinalIgnoreCase);
        if (plakas.Count > 0)
        {
            var twoYearsAgo = _dateTimeService.Now.AddYears(-2);
            var plakaMatches = await _context.Policeler
                .Where(p =>
                    plakas.Contains(p.Plaka) &&
                    p.MusteriId != null &&
                    p.FirmaId == firmaId &&
                    p.BaslangicTarihi >= twoYearsAgo)
                .OrderByDescending(p => p.BaslangicTarihi)
                .Select(p => new { p.Plaka, p.MusteriId, p.SigortaliAdi })
                .ToListAsync(ct);

            foreach (var pm in plakaMatches)
            {
                if (!plakaLookup.ContainsKey(pm.Plaka))
                    plakaLookup[pm.Plaka] = (pm.MusteriId, pm.SigortaliAdi);
            }
        }

        // Her satır için eşleştir
        var newCustomersToCreate = new List<(int RowIndex, Musteri Musteri, MatchSignal Signal)>();
        // Aynı batch'te aynı TC/VKN ile birden fazla satır varsa, aynı entity'yi paylaşsınlar
        var pendingTcEntities = new Dictionary<string, Musteri>(StringComparer.Ordinal);
        var pendingVknEntities = new Dictionary<string, Musteri>(StringComparer.Ordinal);
        // SaveChanges sonrası pending entity'den ID alacak satırlar
        var pendingRows = new List<(int RowIndex, Musteri Musteri, MatchSignal Signal)>();
        // Mevcut müşterilerin boş alanlarını güncellemek için (MusteriId, Request)
        var customersToUpdate = new List<(int MusteriId, CustomerMatchRequest Request)>();

        foreach (var req in requests)
        {
            // 1. TC eşleştir
            if (!string.IsNullOrWhiteSpace(req.TcKimlikNo) && IsValidTc(req.TcKimlikNo))
            {
                if (tcLookup.TryGetValue(req.TcKimlikNo, out var tcMusteriId))
                {
                    customersToUpdate.Add((tcMusteriId, req));
                    results[req.RowIndex] = new CustomerMatchResult
                    {
                        MusteriId = tcMusteriId,
                        Confidence = MatchConfidence.Exact,
                        MatchedBy = MatchSignal.TcKimlikNo
                    };
                    continue;
                }

                // Aynı batch'te bu TC için zaten müşteri oluşturuldu mu?
                if (pendingTcEntities.TryGetValue(req.TcKimlikNo, out var pendingMusteri))
                {
                    pendingRows.Add((req.RowIndex, pendingMusteri, MatchSignal.TcKimlikNo));
                    continue;
                }

                // Yeni müşteri oluşturulacak
                var newMusteri = CreateMusteriEntity(req, req.TcKimlikNo, null, firmaId);
                newCustomersToCreate.Add((req.RowIndex, newMusteri, MatchSignal.TcKimlikNo));
                pendingTcEntities[req.TcKimlikNo] = newMusteri;
                continue;
            }

            // 2. VKN eşleştir
            if (!string.IsNullOrWhiteSpace(req.VergiNo) && IsValidVkn(req.VergiNo))
            {
                if (vknLookup.TryGetValue(req.VergiNo, out var vknMusteriId))
                {
                    customersToUpdate.Add((vknMusteriId, req));
                    results[req.RowIndex] = new CustomerMatchResult
                    {
                        MusteriId = vknMusteriId,
                        Confidence = MatchConfidence.Exact,
                        MatchedBy = MatchSignal.VergiNo
                    };
                    continue;
                }

                // Aynı batch'te bu VKN için zaten müşteri oluşturuldu mu?
                if (pendingVknEntities.TryGetValue(req.VergiNo, out var pendingVknMusteri))
                {
                    pendingRows.Add((req.RowIndex, pendingVknMusteri, MatchSignal.VergiNo));
                    continue;
                }

                var newMusteri = CreateMusteriEntity(req, null, req.VergiNo, firmaId);
                newCustomersToCreate.Add((req.RowIndex, newMusteri, MatchSignal.VergiNo));
                pendingVknEntities[req.VergiNo] = newMusteri;
                continue;
            }

            // 3. Plaka eşleştir
            if (!string.IsNullOrWhiteSpace(req.Plaka) && plakaLookup.TryGetValue(req.Plaka, out var plakaMatch))
            {
                if (plakaMatch.MusteriId.HasValue)
                {
                    // İsim çapraz kontrolü
                    if (!string.IsNullOrWhiteSpace(req.SigortaliAdi) &&
                        !string.IsNullOrWhiteSpace(plakaMatch.SigortaliAdi))
                    {
                        var normReq = NormalizeTurkish(req.SigortaliAdi);
                        var normExist = NormalizeTurkish(plakaMatch.SigortaliAdi);

                        if (normReq != normExist)
                        {
                            // İsim uyuşmuyor - eşleştirme yapma
                            results[req.RowIndex] = new CustomerMatchResult
                            {
                                Confidence = MatchConfidence.Low,
                                MatchedBy = MatchSignal.Plaka
                            };
                            continue;
                        }
                    }

                    results[req.RowIndex] = new CustomerMatchResult
                    {
                        MusteriId = plakaMatch.MusteriId,
                        Confidence = MatchConfidence.Medium,
                        MatchedBy = MatchSignal.Plaka
                    };
                    continue;
                }
            }

            // İsim ile eşleştirme yapılmaz - güvenilirliği düşük
            results[req.RowIndex] = new CustomerMatchResult
            {
                Confidence = MatchConfidence.None,
                MatchedBy = MatchSignal.None
            };
        }

        // Yeni müşterileri toplu oluştur
        if (newCustomersToCreate.Count > 0)
        {
            foreach (var (_, musteri, _) in newCustomersToCreate)
            {
                _context.Musteriler.Add(musteri);
            }
            await _context.SaveChangesAsync(ct);

            // Sonuçlara ekle + lookup'ları güncelle
            foreach (var (rowIndex, musteri, signal) in newCustomersToCreate)
            {
                results[rowIndex] = new CustomerMatchResult
                {
                    MusteriId = musteri.Id,
                    Confidence = MatchConfidence.Exact,
                    MatchedBy = signal,
                    AutoCreated = true
                };

                // Sonraki batch'ler için lookup'ı güncelle
                if (!string.IsNullOrWhiteSpace(musteri.TcKimlikNo))
                    tcLookup[musteri.TcKimlikNo] = musteri.Id;
                if (!string.IsNullOrWhiteSpace(musteri.VergiNo))
                    vknLookup[musteri.VergiNo] = musteri.Id;
            }

            // Aynı TC/VKN'yi paylaşan pending satırları da güncelle (SaveChanges sonrası ID artık mevcut)
            foreach (var (rowIndex, musteri, signal) in pendingRows)
            {
                results[rowIndex] = new CustomerMatchResult
                {
                    MusteriId = musteri.Id,
                    Confidence = MatchConfidence.Exact,
                    MatchedBy = signal,
                    AutoCreated = false
                };
            }
        }

        // Mevcut müşterilerin boş alanlarını toplu güncelle (ExecuteUpdateAsync ile)
        if (customersToUpdate.Count > 0)
        {
            await BatchUpdateExistingCustomerFieldsAsync(customersToUpdate, firmaId, ct);
        }

        return results;
    }

    // --- Yardımcı metodlar ---

    /// <summary>
    /// Türkçe karakter normalizasyonu: İ↔I, Ö↔O, Ş↔S, Ç↔C, Ğ↔G, Ü↔U
    /// </summary>
    public static string NormalizeTurkish(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        return name.Trim()
            .Replace("İ", "I").Replace("ı", "I").Replace("i", "I")
            .Replace("Ğ", "G").Replace("ğ", "G")
            .Replace("Ü", "U").Replace("ü", "U")
            .Replace("Ş", "S").Replace("ş", "S")
            .Replace("Ö", "O").Replace("ö", "O")
            .Replace("Ç", "C").Replace("ç", "C")
            .ToUpperInvariant()
            .Trim();
    }

    private static bool IsValidTc(string tc)
    {
        // TC Kimlik No: tam 11 haneli, hepsi rakam, ilk hane 0 olamaz
        if (string.IsNullOrWhiteSpace(tc) || tc.Length != 11) return false;
        if (tc[0] == '0') return false;
        foreach (var c in tc)
        {
            if (c < '0' || c > '9') return false;
        }
        return true;
    }

    private static bool IsValidVkn(string vkn)
    {
        // Vergi No: tam 10 haneli, hepsi rakam
        if (string.IsNullOrWhiteSpace(vkn) || vkn.Length != 10) return false;
        foreach (var c in vkn)
        {
            if (c < '0' || c > '9') return false;
        }
        return true;
    }

    private async Task<Musteri> AutoCreateCustomerAsync(
        CustomerMatchRequest request, string? tcKimlikNo, string? vergiNo, int firmaId, CancellationToken ct)
    {
        var musteri = CreateMusteriEntity(request, tcKimlikNo, vergiNo, firmaId);
        _context.Musteriler.Add(musteri);
        await _context.SaveChangesAsync(ct);
        return musteri;
    }

    private Musteri CreateMusteriEntity(CustomerMatchRequest request, string? tcKimlikNo, string? vergiNo, int firmaId)
    {
        var (adi, soyadi) = SplitAdSoyad(request.SigortaliAdi, request.SigortaliSoyadi);

        var adres = request.Adres?.Trim();
        if (adres != null && adres.Length > 500) adres = adres[..500];

        return new Musteri
        {
            SahipTuru = vergiNo != null ? (sbyte)2 : (sbyte)1, // 1=Bireysel, 2=Kurumsal
            TcKimlikNo = tcKimlikNo?.Length > 11 ? tcKimlikNo[..11] : tcKimlikNo,
            VergiNo = vergiNo?.Length > 10 ? vergiNo[..10] : vergiNo,
            TcVergiNo = (tcKimlikNo ?? vergiNo) is { Length: > 11 } tv ? tv[..11] : (tcKimlikNo ?? vergiNo),
            Adi = adi,
            Soyadi = soyadi,
            Adres = adres,
            EkleyenFirmaId = firmaId,
            EkleyenUyeId = int.TryParse(_currentUserService.UserId, out var uid) ? uid : null,
            EkleyenSubeId = _currentUserService.SubeId,
            EklenmeZamani = _dateTimeService.Now
        };
    }

    /// <summary>
    /// Ad/Soyad ayırma: Soyad ayrı gelmişse kullan, gelmemişse son kelimeyi soyad yap.
    /// </summary>
    private static (string? Adi, string? Soyadi) SplitAdSoyad(string? sigortaliAdi, string? sigortaliSoyadi)
    {
        string? adi = null;
        string? soyadi = null;

        if (!string.IsNullOrWhiteSpace(sigortaliSoyadi))
        {
            // Soyad ayrı gelmiş (Quick, Doğa, Koru, Unico parser'ları)
            adi = sigortaliAdi?.Trim();
            soyadi = sigortaliSoyadi.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(sigortaliAdi))
        {
            // Soyad ayrı gelmemiş - son kelimeyi soyad yap
            var parts = sigortaliAdi.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                soyadi = parts[^1];
                adi = string.Join(" ", parts[..^1]);
            }
            else
            {
                adi = sigortaliAdi.Trim();
            }
        }

        // DB sütun limitlerine göre truncate
        if (adi != null && adi.Length > 150) adi = adi[..150];
        if (soyadi != null && soyadi.Length > 30) soyadi = soyadi[..30];

        return (adi, soyadi);
    }

    /// <summary>
    /// FindBestMatchAsync'te mevcut müşterinin boş alanlarını günceller (tek kayıt).
    /// </summary>
    private async Task UpdateExistingCustomerFieldsAsync(
        Musteri musteri, CustomerMatchRequest request, CancellationToken ct)
    {
        var (adi, soyadi) = SplitAdSoyad(request.SigortaliAdi, request.SigortaliSoyadi);
        var needsUpdate = false;

        if (string.IsNullOrWhiteSpace(musteri.Adi) && !string.IsNullOrWhiteSpace(adi))
        {
            musteri.Adi = adi;
            needsUpdate = true;
        }
        if (string.IsNullOrWhiteSpace(musteri.Soyadi) && !string.IsNullOrWhiteSpace(soyadi))
        {
            musteri.Soyadi = soyadi;
            needsUpdate = true;
        }
        if (string.IsNullOrWhiteSpace(musteri.TcKimlikNo) && !string.IsNullOrWhiteSpace(request.TcKimlikNo) && IsValidTc(request.TcKimlikNo))
        {
            musteri.TcKimlikNo = request.TcKimlikNo;
            musteri.TcVergiNo = request.TcKimlikNo;
            needsUpdate = true;
        }
        if (string.IsNullOrWhiteSpace(musteri.VergiNo) && !string.IsNullOrWhiteSpace(request.VergiNo) && IsValidVkn(request.VergiNo))
        {
            musteri.VergiNo = request.VergiNo;
            if (string.IsNullOrWhiteSpace(musteri.TcVergiNo))
                musteri.TcVergiNo = request.VergiNo;
            needsUpdate = true;
        }
        if (string.IsNullOrWhiteSpace(musteri.Adres) && !string.IsNullOrWhiteSpace(request.Adres))
        {
            var adres = request.Adres.Trim();
            musteri.Adres = adres.Length > 500 ? adres[..500] : adres;
            needsUpdate = true;
        }

        // SahipTuru: TC varsa Bireysel(1), VKN varsa Kurumsal(2)
        if (musteri.SahipTuru == null || musteri.SahipTuru == 0)
        {
            var tcExists = !string.IsNullOrWhiteSpace(musteri.TcKimlikNo) || (!string.IsNullOrWhiteSpace(request.TcKimlikNo) && IsValidTc(request.TcKimlikNo));
            var vknExists = !string.IsNullOrWhiteSpace(musteri.VergiNo) || (!string.IsNullOrWhiteSpace(request.VergiNo) && IsValidVkn(request.VergiNo));
            if (vknExists && !tcExists)
            {
                musteri.SahipTuru = 2; // Kurumsal
                needsUpdate = true;
            }
            else if (tcExists)
            {
                musteri.SahipTuru = 1; // Bireysel
                needsUpdate = true;
            }
        }

        if (needsUpdate)
        {
            musteri.GuncellenmeZamani = _dateTimeService.Now;
            await _context.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// BatchMatchAsync'te mevcut müşterilerin boş alanlarını toplu günceller (ExecuteUpdateAsync).
    /// </summary>
    private async Task BatchUpdateExistingCustomerFieldsAsync(
        List<(int MusteriId, CustomerMatchRequest Request)> updates, int firmaId, CancellationToken ct)
    {
        try
        {
            // Aynı müşteri birden fazla satırda olabilir - ilk request'i al
            var uniqueUpdates = updates
                .GroupBy(u => u.MusteriId)
                .Select(g => g.First())
                .ToList();

            var musteriIds = uniqueUpdates.Select(u => u.MusteriId).ToList();

            // Mevcut müşteri bilgilerini oku (hangi alanlar boş?)
            var existingCustomers = await _context.Musteriler
                .AsNoTracking()
                .Where(m => musteriIds.Contains(m.Id) && m.EkleyenFirmaId == firmaId)
                .Select(m => new { m.Id, m.Adi, m.Soyadi, m.TcKimlikNo, m.VergiNo, m.TcVergiNo, m.Adres, m.SahipTuru })
                .ToListAsync(ct);

            var customerDict = existingCustomers.ToDictionary(c => c.Id);
            var now = _dateTimeService.Now;

            foreach (var (musteriId, req) in uniqueUpdates)
            {
                if (!customerDict.TryGetValue(musteriId, out var existing)) continue;

                var (adi, soyadi) = SplitAdSoyad(req.SigortaliAdi, req.SigortaliSoyadi);

                var newAdi = string.IsNullOrWhiteSpace(existing.Adi) && !string.IsNullOrWhiteSpace(adi) ? adi : null;
                var newSoyadi = string.IsNullOrWhiteSpace(existing.Soyadi) && !string.IsNullOrWhiteSpace(soyadi) ? soyadi : null;
                var newTc = string.IsNullOrWhiteSpace(existing.TcKimlikNo) && !string.IsNullOrWhiteSpace(req.TcKimlikNo) && IsValidTc(req.TcKimlikNo) ? req.TcKimlikNo : null;
                var newVkn = string.IsNullOrWhiteSpace(existing.VergiNo) && !string.IsNullOrWhiteSpace(req.VergiNo) && IsValidVkn(req.VergiNo) ? req.VergiNo : null;
                var newAdres = string.IsNullOrWhiteSpace(existing.Adres) && !string.IsNullOrWhiteSpace(req.Adres)
                    ? (req.Adres.Trim().Length > 500 ? req.Adres.Trim()[..500] : req.Adres.Trim())
                    : null;

                // SahipTuru: NULL/0 ise TC→Bireysel(1), VKN→Kurumsal(2)
                sbyte? newSahipTuru = null;
                if (existing.SahipTuru == null || existing.SahipTuru == 0)
                {
                    var tcExists = !string.IsNullOrWhiteSpace(existing.TcKimlikNo) || !string.IsNullOrWhiteSpace(newTc);
                    var vknExists = !string.IsNullOrWhiteSpace(existing.VergiNo) || !string.IsNullOrWhiteSpace(newVkn);
                    if (vknExists && !tcExists)
                        newSahipTuru = 2; // Kurumsal
                    else if (tcExists)
                        newSahipTuru = 1; // Bireysel
                }

                if (newAdi != null || newSoyadi != null || newTc != null || newVkn != null || newAdres != null || newSahipTuru != null)
                {
                    var id = musteriId;
                    await _context.Musteriler
                        .Where(m => m.Id == id)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(m => m.Adi, m => newAdi ?? m.Adi)
                            .SetProperty(m => m.Soyadi, m => newSoyadi ?? m.Soyadi)
                            .SetProperty(m => m.TcKimlikNo, m => newTc ?? m.TcKimlikNo)
                            .SetProperty(m => m.VergiNo, m => newVkn ?? m.VergiNo)
                            .SetProperty(m => m.TcVergiNo, m => (newTc ?? newVkn) != null ? (newTc ?? newVkn ?? m.TcVergiNo) : m.TcVergiNo)
                            .SetProperty(m => m.Adres, m => newAdres ?? m.Adres)
                            .SetProperty(m => m.SahipTuru, m => newSahipTuru ?? m.SahipTuru)
                            .SetProperty(m => m.GuncellenmeZamani, now), ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mevcut müşteri boş alan güncelleme hatası (import'u etkilemez): {Message}",
                ex.InnerException?.Message ?? ex.Message);
        }
    }
}
