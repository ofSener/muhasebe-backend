using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.Policeler.Queries;
using IhsanAI.Application.Features.YakalananPoliceler.Queries;

namespace IhsanAI.Application.Features.Dashboard.Queries;

// Response DTO
public record SirketDagilimItem
{
    public int SirketId { get; init; }
    public string SirketAdi { get; init; } = string.Empty;
    public string SirketKodu { get; init; } = string.Empty;
    public int PoliceSayisi { get; init; }
    public decimal ToplamBrutPrim { get; init; }
    public decimal ToplamKomisyon { get; init; }
    public decimal Yuzde { get; init; }
}

public record SirketDagilimResponse
{
    public List<SirketDagilimItem> Dagilim { get; init; } = new();
    public decimal ToplamPrim { get; init; }
    public int ToplamPolice { get; init; }
    public DashboardMode Mode { get; init; }
}

// Query
public record GetSirketDagilimQuery(
    int? FirmaId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int Limit = 10,
    DashboardMode Mode = DashboardMode.Onayli,
    DashboardFilters? Filters = null
) : IRequest<SirketDagilimResponse>;

// Handler
public class GetSirketDagilimQueryHandler : IRequestHandler<GetSirketDagilimQuery, SirketDagilimResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public GetSirketDagilimQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<SirketDagilimResponse> Handle(GetSirketDagilimQuery request, CancellationToken cancellationToken)
    {
        var firmaId = request.FirmaId ?? _currentUserService.FirmaId;
        var now = _dateTimeService.Now;
        var startDate = request.StartDate ?? new DateTime(now.Year, 1, 1);
        var endDate = request.EndDate ?? now;
        var limit = Math.Min(Math.Max(request.Limit, 1), 50);
        var filters = request.Filters ?? new DashboardFilters();

        if (request.Mode == DashboardMode.Yakalama)
        {
            return await GetYakalamaSirketDagilim(firmaId, startDate, endDate, limit, filters, cancellationToken);
        }

        return await GetOnayliSirketDagilim(firmaId, startDate, endDate, limit, filters, cancellationToken);
    }

    private async Task<SirketDagilimResponse> GetOnayliSirketDagilim(
        int? firmaId,
        DateTime startDate,
        DateTime endDate,
        int limit,
        DashboardFilters filters,
        CancellationToken cancellationToken)
    {
        var policeQuery = _context.Policeler
            .Where(p => p.OnayDurumu == 1)
            .ApplyAuthorizationFilters(_currentUserService);
        if (firmaId.HasValue)
        {
            policeQuery = policeQuery.Where(p => p.FirmaId == firmaId.Value);
        }

        // Apply filters
        if (filters.BransIds.Count > 0)
            policeQuery = policeQuery.Where(p => filters.BransIds.Contains(p.PoliceTuruId));
        if (filters.SubeIds.Count > 0)
            policeQuery = policeQuery.Where(p => filters.SubeIds.Contains(p.SubeId));
        if (filters.SirketIds.Count > 0)
            policeQuery = policeQuery.Where(p => filters.SirketIds.Contains(p.SigortaSirketiId));

        var policeler = await policeQuery
            .Where(p => p.TanzimTarihi >= startDate && p.TanzimTarihi <= endDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var sirketIds = policeler.Select(p => p.SigortaSirketiId).Distinct().ToList();
        var sirketler = await _context.SigortaSirketleri
            .Where(s => sirketIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Ad, s.Kod })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var toplamPrim = (decimal)policeler.Sum(p => p.BrutPrim);
        var toplamPolice = policeler.Count;

        var dagilim = policeler
            .GroupBy(p => p.SigortaSirketiId)
            .Select(g =>
            {
                var sirket = sirketler.FirstOrDefault(s => s.Id == g.Key);
                var brutPrim = (decimal)g.Sum(p => p.BrutPrim);

                return new SirketDagilimItem
                {
                    SirketId = g.Key,
                    SirketAdi = sirket?.Ad ?? $"Şirket #{g.Key}",
                    SirketKodu = sirket?.Kod ?? "",
                    PoliceSayisi = g.Count(),
                    ToplamBrutPrim = brutPrim,
                    ToplamKomisyon = (decimal)g.Sum(p => p.Komisyon ?? 0),
                    Yuzde = toplamPrim > 0 ? Math.Round(brutPrim / toplamPrim * 100, 1) : 0
                };
            })
            .OrderByDescending(x => x.ToplamBrutPrim)
            .Take(limit)
            .ToList();

        return new SirketDagilimResponse
        {
            Dagilim = dagilim,
            ToplamPrim = (decimal)toplamPrim,
            ToplamPolice = toplamPolice,
            Mode = DashboardMode.Onayli
        };
    }

    private async Task<SirketDagilimResponse> GetYakalamaSirketDagilim(
        int? firmaId,
        DateTime startDate,
        DateTime endDate,
        int limit,
        DashboardFilters filters,
        CancellationToken cancellationToken)
    {
        var yakalamaQuery = _context.YakalananPoliceler
            .AsQueryable()
            .ApplyAuthorizationFilters(_currentUserService);
        if (firmaId.HasValue)
        {
            yakalamaQuery = yakalamaQuery.Where(y => y.FirmaId == firmaId.Value);
        }

        // Apply filters
        if (filters.BransIds.Count > 0)
            yakalamaQuery = yakalamaQuery.Where(y => filters.BransIds.Contains(y.PoliceTuru));
        if (filters.SubeIds.Count > 0)
            yakalamaQuery = yakalamaQuery.Where(y => filters.SubeIds.Contains(y.SubeId));
        if (filters.SirketIds.Count > 0)
            yakalamaQuery = yakalamaQuery.Where(y => filters.SirketIds.Contains(y.SigortaSirketi));
        if (filters.KullaniciIds.Count > 0)
            yakalamaQuery = yakalamaQuery.Where(y => filters.KullaniciIds.Contains(y.ProduktorId));

        var yakalananlar = await yakalamaQuery
            .Where(y => y.TanzimTarihi >= startDate && y.TanzimTarihi <= endDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var sirketIds = yakalananlar.Select(y => y.SigortaSirketi).Distinct().ToList();
        var sirketler = await _context.SigortaSirketleri
            .Where(s => sirketIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Ad, s.Kod })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var toplamPrim = (decimal)yakalananlar.Sum(y => y.BrutPrim);
        var toplamPolice = yakalananlar.Count;

        var dagilim = yakalananlar
            .GroupBy(y => y.SigortaSirketi)
            .Select(g =>
            {
                var sirket = sirketler.FirstOrDefault(s => s.Id == g.Key);
                var brutPrim = (decimal)g.Sum(y => y.BrutPrim);

                return new SirketDagilimItem
                {
                    SirketId = g.Key,
                    SirketAdi = sirket?.Ad ?? $"Şirket #{g.Key}",
                    SirketKodu = sirket?.Kod ?? "",
                    PoliceSayisi = g.Count(),
                    ToplamBrutPrim = brutPrim,
                    ToplamKomisyon = 0, // Yakalanan poliçelerde komisyon yok
                    Yuzde = toplamPrim > 0 ? Math.Round(brutPrim / toplamPrim * 100, 1) : 0
                };
            })
            .OrderByDescending(x => x.ToplamBrutPrim)
            .Take(limit)
            .ToList();

        return new SirketDagilimResponse
        {
            Dagilim = dagilim,
            ToplamPrim = toplamPrim,
            ToplamPolice = toplamPolice,
            Mode = DashboardMode.Yakalama
        };
    }
}
