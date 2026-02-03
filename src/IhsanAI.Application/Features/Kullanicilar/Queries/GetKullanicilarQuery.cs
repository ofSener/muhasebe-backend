using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.DTOs;

namespace IhsanAI.Application.Features.Kullanicilar.Queries;

public record GetKullanicilarQuery(int? FirmaId = null, int? Limit = null) : IRequest<List<KullaniciListDto>>;

public class GetKullanicilarQueryHandler : IRequestHandler<GetKullanicilarQuery, List<KullaniciListDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetKullanicilarQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<KullaniciListDto>> Handle(GetKullanicilarQuery request, CancellationToken cancellationToken)
    {
        var result = new List<KullaniciListDto>();

        // ========== AKTİF KULLANICILAR ==========
        var aktifQuery = _context.Kullanicilar.AsQueryable();

        // Use FirmaId from current user's JWT claims instead of trusting client parameter
        var firmaId = _currentUserService.FirmaId;
        if (firmaId.HasValue)
        {
            aktifQuery = aktifQuery.Where(x => x.FirmaId == firmaId.Value);
        }

        var aktifKullanicilar = await (
            from k in aktifQuery
            join y in _context.Yetkiler on k.MuhasebeYetkiId equals y.Id into yetkiler
            from yetki in yetkiler.DefaultIfEmpty()
            join s in _context.Subeler on k.SubeId equals s.Id into subeler
            from sube in subeler.DefaultIfEmpty()
            select new KullaniciListDto
            {
                Id = k.Id,
                Adi = k.Adi,
                Soyadi = k.Soyadi,
                Email = k.Email,
                GsmNo = k.GsmNo,
                KullaniciTuru = k.KullaniciTuru,
                AnaYoneticimi = k.AnaYoneticimi,
                MuhasebeYetkiId = k.MuhasebeYetkiId,
                YetkiAdi = yetki != null ? yetki.YetkiAdi : null,
                SubeId = k.SubeId,
                SubeAdi = sube != null ? sube.SubeAdi : null,
                Onay = k.Onay,
                KayitTarihi = k.KayitTarihi,
                IsEski = false
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        result.AddRange(aktifKullanicilar);

        // ========== ESKİ KULLANICILAR ==========
        var eskiQuery = _context.KullanicilarEski.AsQueryable();

        // Use FirmaId from current user's JWT claims
        if (firmaId.HasValue)
        {
            eskiQuery = eskiQuery.Where(x => x.FirmaId == firmaId.Value);
        }

        var eskiKullanicilar = await (
            from k in eskiQuery
            join s in _context.Subeler on k.SubeId equals s.Id into subeler
            from sube in subeler.DefaultIfEmpty()
            select new KullaniciListDto
            {
                Id = k.Id,
                Adi = k.Adi,
                Soyadi = k.Soyadi,
                Email = k.Email,
                GsmNo = k.GsmNo,
                KullaniciTuru = k.KullaniciTuru,
                AnaYoneticimi = k.AnaYoneticimi,
                MuhasebeYetkiId = null,
                YetkiAdi = null,
                SubeId = k.SubeId,
                SubeAdi = sube != null ? sube.SubeAdi : null,
                Onay = 0, // Eski kullanıcılar her zaman Pasif
                KayitTarihi = k.KayitTarihi,
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

// ========== AKTİF KULLANICILAR QUERY ==========
public record AktifKullaniciDto
{
    public int Id { get; init; }
    public string? Adi { get; init; }
    public string? Soyadi { get; init; }
    public string? Email { get; init; }
    public string? GsmNo { get; init; }
    public int? SubeId { get; init; }
}

public record GetAktifKullanicilarQuery(int? FirmaId = null, int? Limit = null) : IRequest<List<AktifKullaniciDto>>;

public class GetAktifKullanicilarQueryHandler : IRequestHandler<GetAktifKullanicilarQuery, List<AktifKullaniciDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetAktifKullanicilarQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<AktifKullaniciDto>> Handle(GetAktifKullanicilarQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Kullanicilar
            .Where(x => x.Onay == 1); // Sadece aktif kullanıcılar

        // Use FirmaId from current user's JWT claims
        var firmaId = _currentUserService.FirmaId;
        if (firmaId.HasValue)
        {
            query = query.Where(x => x.FirmaId == firmaId.Value);
        }

        var result = query
            .OrderBy(x => x.Adi)
            .ThenBy(x => x.Soyadi)
            .Select(x => new AktifKullaniciDto
            {
                Id = x.Id,
                Adi = x.Adi,
                Soyadi = x.Soyadi,
                Email = x.Email,
                GsmNo = x.GsmNo,
                SubeId = x.SubeId
            });

        if (request.Limit.HasValue)
        {
            result = result.Take(request.Limit.Value);
        }

        return await result
            .AsNoTracking()
            .ToListAsync(cancellationToken);
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
    private readonly ICurrentUserService _currentUserService;

    public SearchProducersQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<ProducerSearchDto>> Handle(SearchProducersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Kullanicilar.AsQueryable();

        // Use FirmaId from current user's JWT claims
        var firmaId = _currentUserService.FirmaId;
        if (firmaId.HasValue)
        {
            query = query.Where(x => x.FirmaId == firmaId.Value);
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
