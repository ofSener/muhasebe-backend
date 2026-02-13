using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Extensions;
using IhsanAI.Application.Features.Policeler.Queries;

namespace IhsanAI.Application.Features.Musteriler.Queries;

// Response DTOs
public record MusteriDetailsInfoDto
{
    public int Id { get; init; }
    public sbyte? SahipTuru { get; init; }
    public string? TcKimlikNo { get; init; }
    public string? VergiNo { get; init; }
    public string? Adi { get; init; }
    public string? Soyadi { get; init; }
    public DateTime? DogumTarihi { get; init; }
    public string? Gsm { get; init; }
    public string? Gsm2 { get; init; }
    public string? Telefon { get; init; }
    public string? Email { get; init; }
    public string? Meslek { get; init; }
    public string? YasadigiIl { get; init; }
    public string? YasadigiIlce { get; init; }
    public string? Adres { get; init; }
    public string? Cinsiyet { get; init; }
    public string? BabaAdi { get; init; }
    public int? Boy { get; init; }
    public int? Kilo { get; init; }
    public DateTime? EklenmeZamani { get; init; }
}

public record MusteriDetailsStatsDto
{
    public int AktifPoliceSayisi { get; init; }
    public int ToplamPoliceSayisi { get; init; }
    public decimal ToplamPrim { get; init; }
    public int YaklasanYenilemeSayisi { get; init; }
    public int Yenileme7Gun { get; init; }
    public int Yenileme15Gun { get; init; }
    public int Yenileme30Gun { get; init; }
    public int Yenileme60Gun { get; init; }
}

public record MusteriPoliceDetayDto
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
    public string ProduktorAdi { get; init; } = string.Empty;
    public int YenilemeDurumu { get; init; }
    public sbyte Zeyil { get; init; }
    public string Plaka { get; init; } = string.Empty;
}

public record MusteriBransDagilimDto
{
    public string BransAdi { get; init; } = string.Empty;
    public int PoliceSayisi { get; init; }
}

public record MusteriNotDto
{
    public int Id { get; init; }
    public string Icerik { get; init; } = string.Empty;
    public bool OnemliMi { get; init; }
    public string EkleyenAdi { get; init; } = string.Empty;
    public DateTime EklemeTarihi { get; init; }
}

public record MusteriDetailsResponse
{
    public MusteriDetailsInfoDto Musteri { get; init; } = null!;
    public MusteriDetailsStatsDto Stats { get; init; } = null!;
    public List<MusteriPoliceDetayDto> Policeler { get; init; } = new();
    public List<MusteriBransDagilimDto> BransDagilim { get; init; } = new();
    public List<MusteriNotDto> Notlar { get; init; } = new();
}

// Query
public record GetMusteriDetailsQuery(int MusteriId) : IRequest<MusteriDetailsResponse?>;

// Handler
public class GetMusteriDetailsQueryHandler : IRequestHandler<GetMusteriDetailsQuery, MusteriDetailsResponse?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMusteriDetailsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<MusteriDetailsResponse?> Handle(GetMusteriDetailsQuery request, CancellationToken cancellationToken)
    {
        // 1. Müşteri bilgisi (erişim kontrolü ile)
        var musteriQuery = _context.Musteriler.AsQueryable();
        musteriQuery = musteriQuery.ApplyMusteriAccessFilter(_currentUserService, x => x.EkleyenFirmaId, x => x.EkleyenSubeId);

        var musteri = await musteriQuery
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.MusteriId, cancellationToken);

        if (musteri == null)
            return null;

        var musteriInfo = new MusteriDetailsInfoDto
        {
            Id = musteri.Id,
            SahipTuru = musteri.SahipTuru,
            TcKimlikNo = musteri.TcKimlikNo,
            VergiNo = musteri.VergiNo,
            Adi = musteri.Adi,
            Soyadi = musteri.Soyadi,
            DogumTarihi = musteri.DogumTarihi,
            Gsm = musteri.Gsm,
            Gsm2 = musteri.Gsm2,
            Telefon = musteri.Telefon,
            Email = musteri.Email,
            Meslek = musteri.Meslek,
            YasadigiIl = musteri.YasadigiIl,
            YasadigiIlce = musteri.YasadigiIlce,
            Adres = musteri.Adres,
            Cinsiyet = musteri.Cinsiyet,
            BabaAdi = musteri.BabaAdi,
            Boy = musteri.Boy,
            Kilo = musteri.Kilo,
            EklenmeZamani = musteri.EklenmeZamani
        };

        // 2. Poliçeler (OnayDurumu == 1 && MusteriId == musteriId)
        var policeler = await _context.Policeler
            .Where(p => p.OnayDurumu == 1 && p.MusteriId == request.MusteriId)
            .ApplyAuthorizationFilters(_currentUserService)
            .OrderByDescending(p => p.TanzimTarihi)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // 3. Lookup dictionary'leri
        var bransIds = policeler.Select(p => p.PoliceTuruId).Distinct().ToList();
        var sirketIds = policeler.Select(p => p.SigortaSirketiId).Distinct().ToList();
        var produktorIds = policeler.Select(p => p.ProduktorId).Distinct().ToList();

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

        var produktorDict = (await _context.Kullanicilar
            .Where(k => produktorIds.Contains(k.Id))
            .Select(k => new { k.Id, k.Adi, k.Soyadi })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(k => k.Id);

        // 4. Stats hesapla
        var today = DateTime.Today;
        var aktifPoliceler = policeler.Where(p => p.BitisTarihi >= today).ToList();

        // Yaklaşan yenilemeler: BitisTarihi >= bugün VE YenilemeDurumu == 0
        var yenilemeAdaylari = policeler
            .Where(p => p.BitisTarihi >= today && p.YenilemeDurumu == 0)
            .ToList();

        var yenileme7 = yenilemeAdaylari.Count(p => p.BitisTarihi <= today.AddDays(7));
        var yenileme15 = yenilemeAdaylari.Count(p => p.BitisTarihi <= today.AddDays(15));
        var yenileme30 = yenilemeAdaylari.Count(p => p.BitisTarihi <= today.AddDays(30));
        var yenileme60 = yenilemeAdaylari.Count(p => p.BitisTarihi <= today.AddDays(60));

        var stats = new MusteriDetailsStatsDto
        {
            AktifPoliceSayisi = aktifPoliceler.Count,
            ToplamPoliceSayisi = policeler.Count,
            ToplamPrim = policeler.Sum(p => (decimal)p.BrutPrim),
            YaklasanYenilemeSayisi = yenileme60,
            Yenileme7Gun = yenileme7,
            Yenileme15Gun = yenileme15,
            Yenileme30Gun = yenileme30,
            Yenileme60Gun = yenileme60
        };

        // 5. Branş dağılımı
        var bransDagilim = policeler
            .GroupBy(p => p.PoliceTuruId)
            .Select(g =>
            {
                bransDict.TryGetValue(g.Key, out var brans);
                return new MusteriBransDagilimDto
                {
                    BransAdi = brans?.Ad ?? $"Branş #{g.Key}",
                    PoliceSayisi = g.Count()
                };
            })
            .OrderByDescending(x => x.PoliceSayisi)
            .ToList();

        // 6. Notlar
        var notlar = await _context.MusteriNotlari
            .Where(n => n.MusteriId == request.MusteriId)
            .OrderByDescending(n => n.EklemeTarihi)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Not ekleyen kullanıcı adlarını çek
        var notEkleyenIds = notlar.Where(n => n.EkleyenUyeId.HasValue).Select(n => n.EkleyenUyeId!.Value).Distinct().ToList();
        var notEkleyenDict = (await _context.Kullanicilar
            .Where(k => notEkleyenIds.Contains(k.Id))
            .Select(k => new { k.Id, k.Adi, k.Soyadi })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(k => k.Id);

        var notlarDto = notlar.Select(n =>
        {
            notEkleyenDict.TryGetValue(n.EkleyenUyeId ?? 0, out var ekleyen);
            return new MusteriNotDto
            {
                Id = n.Id,
                Icerik = n.Icerik,
                OnemliMi = n.OnemliMi,
                EkleyenAdi = ekleyen != null ? $"{ekleyen.Adi} {ekleyen.Soyadi}".Trim() : "",
                EklemeTarihi = n.EklemeTarihi
            };
        }).ToList();

        // 7. Poliçe listesi
        var policeListesi = policeler.Select(p =>
        {
            bransDict.TryGetValue(p.PoliceTuruId, out var brans);
            sirketDict.TryGetValue(p.SigortaSirketiId, out var sirket);
            produktorDict.TryGetValue(p.ProduktorId, out var produktor);

            return new MusteriPoliceDetayDto
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
                ProduktorAdi = produktor != null ? $"{produktor.Adi} {produktor.Soyadi}".Trim() : "",
                YenilemeDurumu = p.YenilemeDurumu,
                Zeyil = p.Zeyil,
                Plaka = p.Plaka
            };
        }).ToList();

        return new MusteriDetailsResponse
        {
            Musteri = musteriInfo,
            Stats = stats,
            Policeler = policeListesi,
            BransDagilim = bransDagilim,
            Notlar = notlarDto
        };
    }
}
