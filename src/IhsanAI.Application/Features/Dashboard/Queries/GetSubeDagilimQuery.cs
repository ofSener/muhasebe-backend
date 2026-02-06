using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.Policeler.Queries;
using IhsanAI.Application.Features.YakalananPoliceler.Queries;

namespace IhsanAI.Application.Features.Dashboard.Queries;

// Response DTO
public record SubeDagilimItem
{
    public int SubeId { get; init; }
    public string SubeAdi { get; init; } = string.Empty;
    public int PoliceSayisi { get; init; }
    public decimal ToplamBrutPrim { get; init; }
    public decimal ToplamKomisyon { get; init; }
    public decimal Yuzde { get; init; }
}

public record SubeDagilimResponse
{
    public List<SubeDagilimItem> Dagilim { get; init; } = new();
    public decimal ToplamPrim { get; init; }
    public int ToplamPolice { get; init; }
    public DashboardMode Mode { get; init; }
}

// Query
public record GetSubeDagilimQuery(
    int? FirmaId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    DashboardMode Mode = DashboardMode.Onayli,
    DashboardFilters? Filters = null
) : IRequest<SubeDagilimResponse>;

// Handler
public class GetSubeDagilimQueryHandler : IRequestHandler<GetSubeDagilimQuery, SubeDagilimResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public GetSubeDagilimQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<SubeDagilimResponse> Handle(GetSubeDagilimQuery request, CancellationToken cancellationToken)
    {
        var firmaId = request.FirmaId ?? _currentUserService.FirmaId;
        var now = _dateTimeService.Now;
        var startDate = request.StartDate ?? new DateTime(now.Year, 1, 1);
        var endDate = request.EndDate ?? now;
        var filters = request.Filters ?? new DashboardFilters();

        if (request.Mode == DashboardMode.Yakalama)
        {
            return await GetYakalamaSubeDagilim(firmaId, startDate, endDate, filters, cancellationToken);
        }

        return await GetOnayliSubeDagilim(firmaId, startDate, endDate, filters, cancellationToken);
    }

    private async Task<SubeDagilimResponse> GetOnayliSubeDagilim(
        int? firmaId,
        DateTime startDate,
        DateTime endDate,
        DashboardFilters filters,
        CancellationToken cancellationToken)
    {
        var query = _context.Policeler
            .Where(p => p.OnayDurumu == 1)
            .ApplyAuthorizationFilters(_currentUserService)
            .Where(p => p.TanzimTarihi >= startDate && p.TanzimTarihi <= endDate);

        if (firmaId.HasValue)
        {
            query = query.Where(p => p.FirmaId == firmaId.Value);
        }

        // Apply filters
        if (filters.BransIds.Count > 0)
            query = query.Where(p => filters.BransIds.Contains(p.PoliceTuruId));
        if (filters.SubeIds.Count > 0)
            query = query.Where(p => filters.SubeIds.Contains(p.SubeId));
        if (filters.SirketIds.Count > 0)
            query = query.Where(p => filters.SirketIds.Contains(p.SigortaSirketiId));

        var policeler = await query.AsNoTracking().ToListAsync(cancellationToken);

        if (policeler.Count == 0)
        {
            return new SubeDagilimResponse
            {
                Dagilim = new List<SubeDagilimItem>(),
                Mode = DashboardMode.Onayli
            };
        }

        // Şube bilgilerini al
        var subeIds = policeler.Select(p => p.SubeId).Distinct().ToList();
        var subeDict = (await _context.Subeler
            .Where(s => subeIds.Contains(s.Id))
            .Select(s => new { s.Id, s.SubeAdi })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(s => s.Id);

        var toplamPrim = (decimal)policeler.Sum(p => p.BrutPrim);
        var toplamPolice = policeler.Count;

        var dagilim = policeler
            .GroupBy(p => p.SubeId)
            .Select(g =>
            {
                subeDict.TryGetValue(g.Key, out var sube);
                var brutPrim = (decimal)g.Sum(p => p.BrutPrim);
                return new SubeDagilimItem
                {
                    SubeId = g.Key,
                    SubeAdi = sube?.SubeAdi ?? $"Şube #{g.Key}",
                    PoliceSayisi = g.Count(),
                    ToplamBrutPrim = brutPrim,
                    ToplamKomisyon = (decimal)g.Sum(p => p.Komisyon ?? 0),
                    Yuzde = toplamPrim > 0 ? Math.Round(brutPrim / toplamPrim * 100, 1) : 0
                };
            })
            .OrderByDescending(x => x.ToplamBrutPrim)
            .ToList();

        return new SubeDagilimResponse
        {
            Dagilim = dagilim,
            ToplamPrim = toplamPrim,
            ToplamPolice = toplamPolice,
            Mode = DashboardMode.Onayli
        };
    }

    private async Task<SubeDagilimResponse> GetYakalamaSubeDagilim(
        int? firmaId,
        DateTime startDate,
        DateTime endDate,
        DashboardFilters filters,
        CancellationToken cancellationToken)
    {
        var query = _context.YakalananPoliceler
            .ApplyAuthorizationFilters(_currentUserService)
            .Where(y => y.TanzimTarihi >= startDate && y.TanzimTarihi <= endDate);

        if (firmaId.HasValue)
        {
            query = query.Where(y => y.FirmaId == firmaId.Value);
        }

        // Apply filters
        if (filters.BransIds.Count > 0)
            query = query.Where(y => filters.BransIds.Contains(y.PoliceTuru));
        if (filters.SubeIds.Count > 0)
            query = query.Where(y => filters.SubeIds.Contains(y.SubeId));
        if (filters.SirketIds.Count > 0)
            query = query.Where(y => filters.SirketIds.Contains(y.SigortaSirketi));
        if (filters.KullaniciIds.Count > 0)
            query = query.Where(y => filters.KullaniciIds.Contains(y.ProduktorId));

        var yakalananlar = await query.AsNoTracking().ToListAsync(cancellationToken);

        if (yakalananlar.Count == 0)
        {
            return new SubeDagilimResponse
            {
                Dagilim = new List<SubeDagilimItem>(),
                Mode = DashboardMode.Yakalama
            };
        }

        // Şube bilgilerini al
        var subeIds = yakalananlar.Select(y => y.SubeId).Distinct().ToList();
        var subeDict = (await _context.Subeler
            .Where(s => subeIds.Contains(s.Id))
            .Select(s => new { s.Id, s.SubeAdi })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(s => s.Id);

        var toplamPrim = (decimal)yakalananlar.Sum(y => y.BrutPrim);
        var toplamPolice = yakalananlar.Count;

        var dagilim = yakalananlar
            .GroupBy(y => y.SubeId)
            .Select(g =>
            {
                subeDict.TryGetValue(g.Key, out var sube);
                var brutPrim = (decimal)g.Sum(y => y.BrutPrim);
                return new SubeDagilimItem
                {
                    SubeId = g.Key,
                    SubeAdi = sube?.SubeAdi ?? $"Şube #{g.Key}",
                    PoliceSayisi = g.Count(),
                    ToplamBrutPrim = brutPrim,
                    ToplamKomisyon = 0, // Yakalanan poliçelerde komisyon yok
                    Yuzde = toplamPrim > 0 ? Math.Round(brutPrim / toplamPrim * 100, 1) : 0
                };
            })
            .OrderByDescending(x => x.ToplamBrutPrim)
            .ToList();

        return new SubeDagilimResponse
        {
            Dagilim = dagilim,
            ToplamPrim = toplamPrim,
            ToplamPolice = toplamPolice,
            Mode = DashboardMode.Yakalama
        };
    }
}
