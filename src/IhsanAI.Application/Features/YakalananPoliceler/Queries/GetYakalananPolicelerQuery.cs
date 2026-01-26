using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.DTOs;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.YakalananPoliceler.Queries;

public record GetYakalananPolicelerQuery(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? SortBy = null,
    string? SortDir = null,
    int? Limit = 500
) : IRequest<List<YakalananPoliceDto>>;

public class GetYakalananPolicelerQueryHandler : IRequestHandler<GetYakalananPolicelerQuery, List<YakalananPoliceDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetYakalananPolicelerQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<YakalananPoliceDto>> Handle(GetYakalananPolicelerQuery request, CancellationToken cancellationToken)
    {
        var query = _context.YakalananPoliceler
            .AsQueryable()
            .ApplyAuthorizationFilters(_currentUserService);

        // Tarih filtreleme (TanzimTarihi'ne göre)
        if (request.StartDate.HasValue)
        {
            var startDate = request.StartDate.Value.Date;
            query = query.Where(x => x.TanzimTarihi >= startDate);
        }

        if (request.EndDate.HasValue)
        {
            var endDate = request.EndDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(x => x.TanzimTarihi <= endDate);
        }

        // Sıralama
        query = (request.SortBy?.ToLower(), request.SortDir?.ToLower()) switch
        {
            ("tanzimtarihi", "asc") => query.OrderBy(x => x.TanzimTarihi),
            ("tanzimtarihi", "desc") => query.OrderByDescending(x => x.TanzimTarihi),
            ("brutprim", "asc") => query.OrderBy(x => x.BrutPrim),
            ("brutprim", "desc") => query.OrderByDescending(x => x.BrutPrim),
            ("sigortaliadi", "asc") => query.OrderBy(x => x.SigortaliAdi),
            ("sigortaliadi", "desc") => query.OrderByDescending(x => x.SigortaliAdi),
            ("policenumara", "asc") => query.OrderBy(x => x.PoliceNumarasi),
            ("policenumara", "desc") => query.OrderByDescending(x => x.PoliceNumarasi),
            ("eklenmeTarihi", "asc") => query.OrderBy(x => x.EklenmeTarihi),
            _ => query.OrderByDescending(x => x.EklenmeTarihi) // Default
        };

        return await query
            .Take(request.Limit ?? 500)
            .GroupJoin(
                _context.Subeler,
                p => p.SubeId,
                s => s.Id,
                (p, subeler) => new { p, sube = subeler.FirstOrDefault() })
            .Select(x => new YakalananPoliceDto
            {
                Id = x.p.Id,
                SigortaSirketi = x.p.SigortaSirketi,
                PoliceTuru = x.p.PoliceTuru,
                PoliceNumarasi = x.p.PoliceNumarasi,
                Plaka = x.p.Plaka,
                TanzimTarihi = x.p.TanzimTarihi,
                BaslangicTarihi = x.p.BaslangicTarihi,
                BitisTarihi = x.p.BitisTarihi,
                BrutPrim = x.p.BrutPrim,
                NetPrim = x.p.NetPrim,
                SigortaliAdi = x.p.SigortaliAdi,
                ProduktorId = x.p.ProduktorId,
                ProduktorSubeId = x.p.ProduktorSubeId,
                UyeId = x.p.UyeId,
                SubeId = x.p.SubeId,
                SubeAdi = x.sube != null ? x.sube.SubeAdi : null,
                FirmaId = x.p.FirmaId,
                MusteriId = x.p.MusteriId,
                CepTelefonu = x.p.CepTelefonu,
                GuncelleyenUyeId = x.p.GuncelleyenUyeId,
                DisPolice = x.p.DisPolice,
                AcenteAdi = x.p.AcenteAdi,
                AcenteNo = x.p.AcenteNo,
                EklenmeTarihi = x.p.EklenmeTarihi,
                GuncellenmeTarihi = x.p.GuncellenmeTarihi,
                Aciklama = x.p.Aciklama
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public record GetYakalananPoliceByIdQuery(int Id) : IRequest<YakalananPoliceDto?>;

public class GetYakalananPoliceByIdQueryHandler : IRequestHandler<GetYakalananPoliceByIdQuery, YakalananPoliceDto?>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetYakalananPoliceByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<YakalananPoliceDto?> Handle(GetYakalananPoliceByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.YakalananPoliceler
            .Where(x => x.Id == request.Id)
            .ApplyAuthorizationFilters(_currentUserService)
            .GroupJoin(
                _context.Subeler,
                p => p.SubeId,
                s => s.Id,
                (p, subeler) => new { p, sube = subeler.FirstOrDefault() })
            .Select(x => new YakalananPoliceDto
            {
                Id = x.p.Id,
                SigortaSirketi = x.p.SigortaSirketi,
                PoliceTuru = x.p.PoliceTuru,
                PoliceNumarasi = x.p.PoliceNumarasi,
                Plaka = x.p.Plaka,
                TanzimTarihi = x.p.TanzimTarihi,
                BaslangicTarihi = x.p.BaslangicTarihi,
                BitisTarihi = x.p.BitisTarihi,
                BrutPrim = x.p.BrutPrim,
                NetPrim = x.p.NetPrim,
                SigortaliAdi = x.p.SigortaliAdi,
                ProduktorId = x.p.ProduktorId,
                ProduktorSubeId = x.p.ProduktorSubeId,
                UyeId = x.p.UyeId,
                SubeId = x.p.SubeId,
                SubeAdi = x.sube != null ? x.sube.SubeAdi : null,
                FirmaId = x.p.FirmaId,
                MusteriId = x.p.MusteriId,
                CepTelefonu = x.p.CepTelefonu,
                GuncelleyenUyeId = x.p.GuncelleyenUyeId,
                DisPolice = x.p.DisPolice,
                AcenteAdi = x.p.AcenteAdi,
                AcenteNo = x.p.AcenteNo,
                EklenmeTarihi = x.p.EklenmeTarihi,
                GuncellenmeTarihi = x.p.GuncellenmeTarihi,
                Aciklama = x.p.Aciklama
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
