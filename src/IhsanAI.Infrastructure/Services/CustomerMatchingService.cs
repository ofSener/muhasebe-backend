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
                return new CustomerMatchResult
                {
                    MusteriId = musteri.Id,
                    Confidence = MatchConfidence.Exact,
                    MatchedBy = MatchSignal.TcKimlikNo
                };
            }

            // TC var ama müşteri yok → otomatik oluştur
            var newMusteri = await AutoCreateCustomerAsync(
                request.SigortaliAdi, request.TcKimlikNo, null, firmaId, ct);
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
                return new CustomerMatchResult
                {
                    MusteriId = musteri.Id,
                    Confidence = MatchConfidence.Exact,
                    MatchedBy = MatchSignal.VergiNo
                };
            }

            // VKN var ama müşteri yok → otomatik oluştur (kurumsal)
            var newMusteri = await AutoCreateCustomerAsync(
                request.SigortaliAdi, null, request.VergiNo, firmaId, ct);
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
                        // İsim uyuşmuyor - araç el değiştirmiş olabilir, aday olarak dön
                        var candidateMusteri = await _context.Musteriler
                            .Where(m => m.Id == plakaMatch.MusteriId)
                            .Select(m => new CustomerCandidate
                            {
                                MusteriId = m.Id,
                                Adi = m.Adi,
                                Soyadi = m.Soyadi,
                                TcKimlikNo = m.TcKimlikNo,
                                VergiNo = m.VergiNo,
                                Confidence = MatchConfidence.Low,
                                MatchedBy = MatchSignal.Plaka
                            })
                            .FirstOrDefaultAsync(ct);

                        return new CustomerMatchResult
                        {
                            Confidence = MatchConfidence.Low,
                            MatchedBy = MatchSignal.Plaka,
                            Candidates = candidateMusteri != null ? new List<CustomerCandidate> { candidateMusteri } : new()
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

        // 4. İsim ile eşleştir (en düşük güvenilirlik)
        if (!string.IsNullOrWhiteSpace(request.SigortaliAdi))
        {
            var normalizedName = NormalizeTurkish(request.SigortaliAdi);
            var candidates = await FindByNameAsync(normalizedName, firmaId, ct);

            if (candidates.Count == 1)
            {
                return new CustomerMatchResult
                {
                    MusteriId = candidates[0].MusteriId,
                    Confidence = MatchConfidence.Medium,
                    MatchedBy = MatchSignal.Name,
                    Candidates = candidates
                };
            }

            if (candidates.Count > 1)
            {
                // Birden fazla aday - otomatik atama yapma
                return new CustomerMatchResult
                {
                    Confidence = MatchConfidence.Low,
                    MatchedBy = MatchSignal.Name,
                    Candidates = candidates
                };
            }
        }

        // Hiçbir eşleşme bulunamadı
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

        // Firma müşterilerini toplu yükle
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
        var nameLookup = new Dictionary<string, List<(int Id, string? Adi, string? Soyadi, string? TcKimlikNo, string? VergiNo)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in musteriler)
        {
            if (!string.IsNullOrWhiteSpace(m.TcKimlikNo) && !tcLookup.ContainsKey(m.TcKimlikNo))
                tcLookup[m.TcKimlikNo] = m.Id;

            if (!string.IsNullOrWhiteSpace(m.VergiNo) && !vknLookup.ContainsKey(m.VergiNo))
                vknLookup[m.VergiNo] = m.Id;

            var fullName = NormalizeTurkish(((m.Adi ?? "") + " " + (m.Soyadi ?? "")).Trim());
            if (!string.IsNullOrEmpty(fullName))
            {
                if (!nameLookup.ContainsKey(fullName))
                    nameLookup[fullName] = new();
                nameLookup[fullName].Add((m.Id, m.Adi, m.Soyadi, m.TcKimlikNo, m.VergiNo));
            }

            var firstName = NormalizeTurkish((m.Adi ?? "").Trim());
            if (!string.IsNullOrEmpty(firstName) && firstName != fullName)
            {
                if (!nameLookup.ContainsKey(firstName))
                    nameLookup[firstName] = new();
                nameLookup[firstName].Add((m.Id, m.Adi, m.Soyadi, m.TcKimlikNo, m.VergiNo));
            }
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

        foreach (var req in requests)
        {
            // 1. TC eşleştir
            if (!string.IsNullOrWhiteSpace(req.TcKimlikNo) && IsValidTc(req.TcKimlikNo))
            {
                if (tcLookup.TryGetValue(req.TcKimlikNo, out var tcMusteriId))
                {
                    results[req.RowIndex] = new CustomerMatchResult
                    {
                        MusteriId = tcMusteriId,
                        Confidence = MatchConfidence.Exact,
                        MatchedBy = MatchSignal.TcKimlikNo
                    };
                    continue;
                }

                // Yeni müşteri oluşturulacak
                var newMusteri = CreateMusteriEntity(req.SigortaliAdi, req.TcKimlikNo, null, firmaId);
                newCustomersToCreate.Add((req.RowIndex, newMusteri, MatchSignal.TcKimlikNo));
                continue;
            }

            // 2. VKN eşleştir
            if (!string.IsNullOrWhiteSpace(req.VergiNo) && IsValidVkn(req.VergiNo))
            {
                if (vknLookup.TryGetValue(req.VergiNo, out var vknMusteriId))
                {
                    results[req.RowIndex] = new CustomerMatchResult
                    {
                        MusteriId = vknMusteriId,
                        Confidence = MatchConfidence.Exact,
                        MatchedBy = MatchSignal.VergiNo
                    };
                    continue;
                }

                var newMusteri = CreateMusteriEntity(req.SigortaliAdi, null, req.VergiNo, firmaId);
                newCustomersToCreate.Add((req.RowIndex, newMusteri, MatchSignal.VergiNo));
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
                            // İsim uyuşmuyor, sadece aday olarak dön
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

            // 4. İsim eşleştir
            if (!string.IsNullOrWhiteSpace(req.SigortaliAdi))
            {
                var normalizedName = NormalizeTurkish(req.SigortaliAdi);
                if (nameLookup.TryGetValue(normalizedName, out var nameMatches))
                {
                    if (nameMatches.Count == 1)
                    {
                        results[req.RowIndex] = new CustomerMatchResult
                        {
                            MusteriId = nameMatches[0].Id,
                            Confidence = MatchConfidence.Medium,
                            MatchedBy = MatchSignal.Name
                        };
                        continue;
                    }

                    // Birden fazla aday - otomatik atama yapma
                    results[req.RowIndex] = new CustomerMatchResult
                    {
                        Confidence = MatchConfidence.Low,
                        MatchedBy = MatchSignal.Name,
                        Candidates = nameMatches.Select(m => new CustomerCandidate
                        {
                            MusteriId = m.Id,
                            Adi = m.Adi,
                            Soyadi = m.Soyadi,
                            TcKimlikNo = m.TcKimlikNo,
                            VergiNo = m.VergiNo,
                            Confidence = MatchConfidence.Low,
                            MatchedBy = MatchSignal.Name
                        }).ToList()
                    };
                    continue;
                }
            }

            // Eşleşme yok
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

    private async Task<List<CustomerCandidate>> FindByNameAsync(string normalizedName, int firmaId, CancellationToken ct)
    {
        // DB'den tüm müşterileri çekmek yerine, isim parçaları ile filtrele
        var parts = normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return new();

        // İlk parça (ad) ile ön-filtreleme
        var firstPart = parts[0];

        var candidates = await _context.Musteriler
            .Where(m => m.EkleyenFirmaId == firmaId && m.Adi != null)
            .Select(m => new { m.Id, m.Adi, m.Soyadi, m.TcKimlikNo, m.VergiNo })
            .ToListAsync(ct);

        var matched = new List<CustomerCandidate>();
        foreach (var m in candidates)
        {
            var fullName = NormalizeTurkish(((m.Adi ?? "") + " " + (m.Soyadi ?? "")).Trim());
            var firstName = NormalizeTurkish((m.Adi ?? "").Trim());

            if (fullName == normalizedName || firstName == normalizedName)
            {
                matched.Add(new CustomerCandidate
                {
                    MusteriId = m.Id,
                    Adi = m.Adi,
                    Soyadi = m.Soyadi,
                    TcKimlikNo = m.TcKimlikNo,
                    VergiNo = m.VergiNo,
                    Confidence = fullName == normalizedName ? MatchConfidence.Medium : MatchConfidence.Low,
                    MatchedBy = MatchSignal.Name
                });
            }
        }

        return matched;
    }

    private async Task<Musteri> AutoCreateCustomerAsync(
        string? sigortaliAdi, string? tcKimlikNo, string? vergiNo, int firmaId, CancellationToken ct)
    {
        var musteri = CreateMusteriEntity(sigortaliAdi, tcKimlikNo, vergiNo, firmaId);
        _context.Musteriler.Add(musteri);
        await _context.SaveChangesAsync(ct);
        return musteri;
    }

    private Musteri CreateMusteriEntity(string? sigortaliAdi, string? tcKimlikNo, string? vergiNo, int firmaId)
    {
        // İsimden ad/soyad ayırma
        string? adi = null;
        string? soyadi = null;
        if (!string.IsNullOrWhiteSpace(sigortaliAdi))
        {
            var parts = sigortaliAdi.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                soyadi = parts[^1]; // Son kelime soyad
                adi = string.Join(" ", parts[..^1]); // Geri kalanı ad
            }
            else
            {
                adi = sigortaliAdi.Trim();
            }
        }

        return new Musteri
        {
            SahipTuru = vergiNo != null ? (sbyte)2 : (sbyte)1, // 1=Bireysel, 2=Kurumsal
            TcKimlikNo = tcKimlikNo,
            VergiNo = vergiNo,
            TcVergiNo = tcKimlikNo ?? vergiNo,
            Adi = adi,
            Soyadi = soyadi,
            EkleyenFirmaId = firmaId,
            EkleyenUyeId = int.TryParse(_currentUserService.UserId, out var uid) ? uid : null,
            EkleyenSubeId = _currentUserService.SubeId,
            EklenmeZamani = _dateTimeService.Now
        };
    }
}
