using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Domain.Entities;

namespace IhsanAI.Application.Features.YakalananPoliceler.Queries;

public record GetYakalananPolicelerQuery(
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? SortBy = null,
    string? SortDir = null,
    int? Limit = 500
) : IRequest<List<YakalananPolice>>;

public class GetYakalananPolicelerQueryHandler : IRequestHandler<GetYakalananPolicelerQuery, List<YakalananPolice>>
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

    public async Task<List<YakalananPolice>> Handle(GetYakalananPolicelerQuery request, CancellationToken cancellationToken)
    {
        var query = _context.YakalananPoliceler.AsQueryable();

        // Firma bazlı temel filtre
        if (_currentUserService.FirmaId.HasValue)
        {
            query = query.Where(x => x.FirmaId == _currentUserService.FirmaId.Value);
        }

        // GorebilecegiPoliceler yetkisine göre filtrele
        // "1" = Tüm firma poliçeleri (admin)
        // "2" = Şube poliçeleri (editor)
        // "3" = Sadece kendi poliçeleri (viewer)
        var gorebilecegiPoliceler = _currentUserService.GorebilecegiPoliceler ?? "3";
        var userId = int.TryParse(_currentUserService.UserId, out var uid) ? uid : 0;

        query = gorebilecegiPoliceler switch
        {
            "1" => query, // Tüm firma poliçeleri - ek filtre yok
            "2" => _currentUserService.SubeId.HasValue
                ? query.Where(x => x.SubeId == _currentUserService.SubeId.Value)
                : query,
            "3" => query.Where(x => x.UyeId == userId), // Sadece kendi poliçeleri
            "4" => query.Where(x => false), // Hiçbir poliçeyi göremez
            _ => query.Where(x => x.UyeId == userId) // Default - sadece kendi poliçeleri
        };

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
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public record GetYakalananPoliceByIdQuery(int Id) : IRequest<YakalananPolice?>;

public class GetYakalananPoliceByIdQueryHandler : IRequestHandler<GetYakalananPoliceByIdQuery, YakalananPolice?>
{
    private readonly IApplicationDbContext _context;

    public GetYakalananPoliceByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<YakalananPolice?> Handle(GetYakalananPoliceByIdQuery request, CancellationToken cancellationToken)
    {
        return await _context.YakalananPoliceler
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
