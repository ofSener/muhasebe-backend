using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.DTOs;

namespace IhsanAI.Application.Features.Kullanicilar.Queries;

public record GetKullanicilarQuery(int? FirmaId = null, int? Limit = 100) : IRequest<List<KullaniciListDto>>;

public class GetKullanicilarQueryHandler : IRequestHandler<GetKullanicilarQuery, List<KullaniciListDto>>
{
    private readonly IApplicationDbContext _context;

    public GetKullanicilarQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<KullaniciListDto>> Handle(GetKullanicilarQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Kullanicilar.AsQueryable();

        if (request.FirmaId.HasValue)
        {
            query = query.Where(x => x.FirmaId == request.FirmaId.Value);
        }

        // Önce kullanıcıları al
        var kullanicilar = await query
            .OrderByDescending(x => x.KayitTarihi)
            .Take(request.Limit ?? 100)
            .GroupJoin(
                _context.Yetkiler,
                k => k.MuhasebeYetkiId,
                y => y.Id,
                (k, yetkiler) => new { k, yetki = yetkiler.FirstOrDefault() })
            .Select(x => new
            {
                x.k.Id,
                x.k.Adi,
                x.k.Soyadi,
                x.k.Email,
                x.k.GsmNo,
                x.k.KullaniciTuru,
                x.k.AnaYoneticimi,
                x.k.MuhasebeYetkiId,
                YetkiAdi = x.yetki != null ? x.yetki.YetkiAdi : null,
                x.k.Onay,
                x.k.KayitTarihi
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Kullanıcı ID'lerini al
        var kullaniciIds = kullanicilar.Select(k => k.Id).ToList();

        // Her kullanıcı için poliçe sayısını al
        var policeSayilari = await _context.YakalananPoliceler
            .Where(p => kullaniciIds.Contains(p.ProduktorId))
            .GroupBy(p => p.ProduktorId)
            .Select(g => new { ProduktorId = g.Key, Sayi = g.Count() })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var policeSayilariDict = policeSayilari.ToDictionary(x => x.ProduktorId, x => x.Sayi);

        // Sonuçları birleştir
        return kullanicilar.Select(k => new KullaniciListDto
        {
            Id = k.Id,
            Adi = k.Adi,
            Soyadi = k.Soyadi,
            Email = k.Email,
            GsmNo = k.GsmNo,
            KullaniciTuru = k.KullaniciTuru,
            AnaYoneticimi = k.AnaYoneticimi,
            MuhasebeYetkiId = k.MuhasebeYetkiId,
            YetkiAdi = k.YetkiAdi,
            Onay = k.Onay,
            KayitTarihi = k.KayitTarihi,
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
