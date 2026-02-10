using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.Policeler.Queries;

namespace IhsanAI.Application.Features.Kullanicilar.Queries;

// Response DTOs
public record KullaniciInfoDto
{
    public int Id { get; init; }
    public string Adi { get; init; } = string.Empty;
    public string Soyadi { get; init; } = string.Empty;
    public string SubeAdi { get; init; } = string.Empty;
    public DateTime? KayitTarihi { get; init; }
    public string? Email { get; init; }
    public string? GsmNo { get; init; }
    public string? ProfilYolu { get; init; }
}

public record KullaniciStatsDto
{
    public decimal ToplamBrutPrim { get; init; }
    public int ToplamPoliceSayisi { get; init; }
    public decimal ToplamKomisyon { get; init; }
}

public record BransDagilimDto
{
    public string BransAdi { get; init; } = string.Empty;
    public int PoliceSayisi { get; init; }
    public decimal ToplamBrutPrim { get; init; }
}

public record SirketDagilimDto
{
    public string SirketAdi { get; init; } = string.Empty;
    public string SirketKodu { get; init; } = string.Empty;
    public int PoliceSayisi { get; init; }
    public decimal ToplamBrutPrim { get; init; }
}

public record GunlukUretimDto
{
    public string Tarih { get; init; } = string.Empty;
    public int PoliceSayisi { get; init; }
    public decimal ToplamBrutPrim { get; init; }
}

public record KullaniciPoliceDto
{
    public int Id { get; init; }
    public string PoliceNo { get; init; } = string.Empty;
    public string BransAdi { get; init; } = string.Empty;
    public string SirketAdi { get; init; } = string.Empty;
    public string SirketKodu { get; init; } = string.Empty;
    public decimal BrutPrim { get; init; }
    public decimal NetPrim { get; init; }
    public decimal Komisyon { get; init; }
    public DateTime BaslangicTarihi { get; init; }
    public DateTime BitisTarihi { get; init; }
    public DateTime TanzimTarihi { get; init; }
    public string MusteriAdi { get; init; } = string.Empty;
}

public record KullaniciDetailsResponse
{
    public KullaniciInfoDto Kullanici { get; init; } = null!;
    public KullaniciStatsDto Stats { get; init; } = null!;
    public List<BransDagilimDto> BransDagilim { get; init; } = new();
    public List<SirketDagilimDto> SirketDagilim { get; init; } = new();
    public List<GunlukUretimDto> GunlukUretim { get; init; } = new();
    public List<KullaniciPoliceDto> Policeler { get; init; } = new();
}

// Query
public record GetKullaniciDetailsQuery(
    int KullaniciId,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int? BransId = null,
    int? SirketId = null,
    string? Search = null
) : IRequest<KullaniciDetailsResponse?>;

// Handler
public class GetKullaniciDetailsQueryHandler : IRequestHandler<GetKullaniciDetailsQuery, KullaniciDetailsResponse?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetKullaniciDetailsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<KullaniciDetailsResponse?> Handle(GetKullaniciDetailsQuery request, CancellationToken cancellationToken)
    {
        // 1. Kullanıcı bilgisi
        var kullaniciInfo = await (
            from k in _context.Kullanicilar.Where(x => x.Id == request.KullaniciId)
            join s in _context.Subeler on k.SubeId equals s.Id into subeler
            from sube in subeler.DefaultIfEmpty()
            select new KullaniciInfoDto
            {
                Id = k.Id,
                Adi = k.Adi ?? "",
                Soyadi = k.Soyadi ?? "",
                SubeAdi = sube != null ? sube.SubeAdi ?? "" : "",
                KayitTarihi = k.KayitTarihi,
                Email = k.Email,
                GsmNo = k.GsmNo,
                ProfilYolu = k.ProfilYolu
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (kullaniciInfo == null)
            return null;

        // 2. Poliçeler - OnayDurumu == 1 && ProduktorId == kullaniciId
        var policeQuery = _context.Policeler
            .Where(p => p.OnayDurumu == 1 && p.ProduktorId == request.KullaniciId)
            .ApplyAuthorizationFilters(_currentUserService);

        // Tarih filtresi (TanzimTarihi)
        if (request.StartDate.HasValue)
            policeQuery = policeQuery.Where(p => p.TanzimTarihi >= request.StartDate.Value.Date);
        if (request.EndDate.HasValue)
            policeQuery = policeQuery.Where(p => p.TanzimTarihi < request.EndDate.Value.Date.AddDays(1));

        // Branş filtresi
        if (request.BransId.HasValue)
            policeQuery = policeQuery.Where(p => p.PoliceTuruId == request.BransId.Value);

        // Şirket filtresi
        if (request.SirketId.HasValue)
            policeQuery = policeQuery.Where(p => p.SigortaSirketiId == request.SirketId.Value);

        // Poliçe no arama
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchTerm = request.Search.Trim();
            policeQuery = policeQuery.Where(p => p.PoliceNumarasi.Contains(searchTerm));
        }

        var policeler = await policeQuery
            .OrderByDescending(p => p.TanzimTarihi)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // 3. Lookup dictionary'leri
        var bransIds = policeler.Select(p => p.PoliceTuruId).Distinct().ToList();
        var sirketIds = policeler.Select(p => p.SigortaSirketiId).Distinct().ToList();
        var musteriIds = policeler.Where(p => p.MusteriId.HasValue).Select(p => p.MusteriId!.Value).Distinct().ToList();

        var bransDict = (await _context.PoliceTurleri
            .Where(b => bransIds.Contains(b.Id))
            .Select(b => new { b.Id, Ad = b.Turu ?? "" })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(b => b.Id);

        var sirketDict = (await _context.SigortaSirketleri
            .Where(s => sirketIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Ad, s.Kod })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(s => s.Id);

        var musteriDict = (await _context.Musteriler
            .Where(m => musteriIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Adi, m.Soyadi })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(m => m.Id);

        // 4. Stats
        var stats = new KullaniciStatsDto
        {
            ToplamBrutPrim = policeler.Sum(p => (decimal)p.BrutPrim),
            ToplamPoliceSayisi = policeler.Count,
            ToplamKomisyon = policeler.Sum(p => (decimal)(p.Komisyon ?? 0))
        };

        // 5. Branş dağılımı
        var bransDagilim = policeler
            .GroupBy(p => p.PoliceTuruId)
            .Select(g =>
            {
                bransDict.TryGetValue(g.Key, out var brans);
                return new BransDagilimDto
                {
                    BransAdi = brans?.Ad ?? $"Branş #{g.Key}",
                    PoliceSayisi = g.Count(),
                    ToplamBrutPrim = g.Sum(p => (decimal)p.BrutPrim)
                };
            })
            .OrderByDescending(x => x.ToplamBrutPrim)
            .ToList();

        // 6. Şirket dağılımı
        var sirketDagilim = policeler
            .GroupBy(p => p.SigortaSirketiId)
            .Select(g =>
            {
                sirketDict.TryGetValue(g.Key, out var sirket);
                return new SirketDagilimDto
                {
                    SirketAdi = sirket?.Ad ?? $"Şirket #{g.Key}",
                    SirketKodu = sirket?.Kod ?? "",
                    PoliceSayisi = g.Count(),
                    ToplamBrutPrim = g.Sum(p => (decimal)p.BrutPrim)
                };
            })
            .OrderByDescending(x => x.ToplamBrutPrim)
            .ToList();

        // 7. Günlük üretim
        var gunlukUretim = policeler
            .GroupBy(p => p.TanzimTarihi.Date)
            .Select(g => new GunlukUretimDto
            {
                Tarih = g.Key.ToString("yyyy-MM-dd"),
                PoliceSayisi = g.Count(),
                ToplamBrutPrim = g.Sum(p => (decimal)p.BrutPrim)
            })
            .OrderBy(x => x.Tarih)
            .ToList();

        // 8. Poliçe listesi
        var policeListesi = policeler.Select(p =>
        {
            bransDict.TryGetValue(p.PoliceTuruId, out var brans);
            sirketDict.TryGetValue(p.SigortaSirketiId, out var sirket);
            musteriDict.TryGetValue(p.MusteriId ?? 0, out var musteri);

            return new KullaniciPoliceDto
            {
                Id = p.Id,
                PoliceNo = p.PoliceNumarasi,
                BransAdi = brans?.Ad ?? $"Branş #{p.PoliceTuruId}",
                SirketAdi = sirket?.Ad ?? $"Şirket #{p.SigortaSirketiId}",
                SirketKodu = sirket?.Kod ?? "",
                BrutPrim = (decimal)p.BrutPrim,
                NetPrim = (decimal)p.NetPrim,
                Komisyon = (decimal)(p.Komisyon ?? 0),
                BaslangicTarihi = p.BaslangicTarihi,
                BitisTarihi = p.BitisTarihi,
                TanzimTarihi = p.TanzimTarihi,
                MusteriAdi = musteri != null ? $"{musteri.Adi} {musteri.Soyadi}".Trim() : (p.SigortaliAdi ?? "")
            };
        }).ToList();

        return new KullaniciDetailsResponse
        {
            Kullanici = kullaniciInfo,
            Stats = stats,
            BransDagilim = bransDagilim,
            SirketDagilim = sirketDagilim,
            GunlukUretim = gunlukUretim,
            Policeler = policeListesi
        };
    }
}
