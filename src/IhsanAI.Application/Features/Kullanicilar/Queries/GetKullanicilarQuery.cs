using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.DTOs;

namespace IhsanAI.Application.Features.Kullanicilar.Queries;

public record GetKullanicilarQuery(int? FirmaId = null, int? Limit = null) : IRequest<List<KullaniciListDto>>;

public class GetKullanicilarQueryHandler : IRequestHandler<GetKullanicilarQuery, List<KullaniciListDto>>
{
    private readonly IApplicationDbContext _context;

    public GetKullanicilarQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<KullaniciListDto>> Handle(GetKullanicilarQuery request, CancellationToken cancellationToken)
    {
        var result = new List<KullaniciListDto>();

        // ========== AKTİF KULLANICILAR ==========
        var aktifQuery = _context.Kullanicilar.AsQueryable();

        if (request.FirmaId.HasValue)
        {
            aktifQuery = aktifQuery.Where(x => x.FirmaId == request.FirmaId.Value);
        }

        var aktifKullanicilar = await aktifQuery
            .GroupJoin(
                _context.Yetkiler,
                k => k.MuhasebeYetkiId,
                y => y.Id,
                (k, yetkiler) => new { k, yetki = yetkiler.FirstOrDefault() })
            .Select(x => new KullaniciListDto
            {
                Id = x.k.Id,
                Adi = x.k.Adi,
                Soyadi = x.k.Soyadi,
                Email = x.k.Email,
                GsmNo = x.k.GsmNo,
                KullaniciTuru = x.k.KullaniciTuru,
                AnaYoneticimi = x.k.AnaYoneticimi,
                MuhasebeYetkiId = x.k.MuhasebeYetkiId,
                YetkiAdi = x.yetki != null ? x.yetki.YetkiAdi : null,
                Onay = x.k.Onay,
                KayitTarihi = x.k.KayitTarihi,
                IsEski = false
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        result.AddRange(aktifKullanicilar);

        // ========== ESKİ KULLANICILAR ==========
        var eskiQuery = _context.KullanicilarEski.AsQueryable();

        if (request.FirmaId.HasValue)
        {
            eskiQuery = eskiQuery.Where(x => x.FirmaId == request.FirmaId.Value);
        }

        var eskiKullanicilar = await eskiQuery
            .Select(x => new KullaniciListDto
            {
                Id = x.Id,
                Adi = x.Adi,
                Soyadi = x.Soyadi,
                Email = x.Email,
                GsmNo = x.GsmNo,
                KullaniciTuru = x.KullaniciTuru,
                AnaYoneticimi = x.AnaYoneticimi,
                MuhasebeYetkiId = null,
                YetkiAdi = null,
                Onay = 0, // Eski kullanıcılar her zaman Pasif
                KayitTarihi = x.KayitTarihi,
                IsEski = true
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        result.AddRange(eskiKullanicilar);

        // ========== SIRALA ==========
        result = result
            .OrderByDescending(x => x.KayitTarihi)
            .ToList();

        // Limit varsa uygula
        if (request.Limit.HasValue)
        {
            result = result.Take(request.Limit.Value).ToList();
        }

        // ========== POLİÇE SAYILARINI AL ==========
        var tumIds = result.Select(k => k.Id).ToList();

        var policeSayilari = await _context.YakalananPoliceler
            .Where(p => tumIds.Contains(p.ProduktorId))
            .GroupBy(p => p.ProduktorId)
            .Select(g => new { ProduktorId = g.Key, Sayi = g.Count() })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var policeSayilariDict = policeSayilari.ToDictionary(x => x.ProduktorId, x => x.Sayi);

        // Poliçe sayılarını ekleyerek sonuç döndür
        return result.Select(k => k with
        {
            PoliceSayisi = policeSayilariDict.GetValueOrDefault(k.Id, 0)
        }).ToList();
    }
}

public record GetKullaniciByIdQuery(int Id) : IRequest<KullaniciDto?>;

public class GetKullaniciByIdQueryHandler : IRequestHandler<GetKullaniciByIdQuery, KullaniciDto?>
{
    private readonly IApplicationDbContext _context;

    public GetKullaniciByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<KullaniciDto?> Handle(GetKullaniciByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.Kullanicilar
            .Where(x => x.Id == request.Id)
            .Select(x => new KullaniciDto
            {
                Id = x.Id,
                FirmaId = x.FirmaId,
                SubeId = x.SubeId,
                YetkiId = x.YetkiId,
                KullaniciTuru = x.KullaniciTuru,
                Adi = x.Adi,
                Soyadi = x.Soyadi,
                Email = x.Email,
                GsmNo = x.GsmNo,
                SabitTel = x.SabitTel,
                Onay = x.Onay,
                AnaYoneticimi = x.AnaYoneticimi,
                KayitTarihi = x.KayitTarihi,
                GuncellemeTarihi = x.GuncellemeTarihi,
                SonGirisZamani = x.SonGirisZamani,
                ProfilYolu = x.ProfilYolu
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public record ProducerSearchDto
{
    public int Id { get; init; }
    public string? Adi { get; init; }
    public string? Soyadi { get; init; }
    public string? Email { get; init; }
    public string? GsmNo { get; init; }
}

public record SearchProducersQuery(string Name, int? FirmaId = null, int Limit = 20) : IRequest<List<ProducerSearchDto>>;

public class SearchProducersQueryHandler : IRequestHandler<SearchProducersQuery, List<ProducerSearchDto>>
{
    private readonly IApplicationDbContext _context;

    public SearchProducersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProducerSearchDto>> Handle(SearchProducersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Kullanicilar.AsQueryable();

        if (request.FirmaId.HasValue)
        {
            query = query.Where(x => x.FirmaId == request.FirmaId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var searchTerm = request.Name.ToLower();
            query = query.Where(x =>
                (x.Adi != null && x.Adi.ToLower().Contains(searchTerm)) ||
                (x.Soyadi != null && x.Soyadi.ToLower().Contains(searchTerm)));
        }

        return await query
            .OrderBy(x => x.Adi)
            .ThenBy(x => x.Soyadi)
            .Take(request.Limit)
            .Select(x => new ProducerSearchDto
            {
                Id = x.Id,
                Adi = x.Adi,
                Soyadi = x.Soyadi,
                Email = x.Email,
                GsmNo = x.GsmNo
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
