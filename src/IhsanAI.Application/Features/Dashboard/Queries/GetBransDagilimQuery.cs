using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Dashboard.Queries;

// Response DTO
public record BransDagilimItem
{
    public int BransId { get; init; }
    public string BransAdi { get; init; } = string.Empty;
    public int PoliceSayisi { get; init; }
    public decimal ToplamBrutPrim { get; init; }
    public decimal ToplamKomisyon { get; init; }
    public decimal Yuzde { get; init; }
}

public record BransDagilimResponse
{
    public List<BransDagilimItem> Dagilim { get; init; } = new();
    public decimal ToplamPrim { get; init; }
    public int ToplamPolice { get; init; }
    public DashboardMode Mode { get; init; }
}

// Query
public record GetBransDagilimQuery(
    int? FirmaId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    DashboardMode Mode = DashboardMode.Onayli
) : IRequest<BransDagilimResponse>;

// Handler
public class GetBransDagilimQueryHandler : IRequestHandler<GetBransDagilimQuery, BransDagilimResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public GetBransDagilimQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<BransDagilimResponse> Handle(GetBransDagilimQuery request, CancellationToken cancellationToken)
    {
        var firmaId = request.FirmaId ?? _currentUserService.FirmaId;
        var now = _dateTimeService.Now;
        var startDate = request.StartDate ?? new DateTime(now.Year, 1, 1);
        var endDate = request.EndDate ?? now;

        if (request.Mode == DashboardMode.Yakalama)
        {
            return await GetYakalamaBransDagilim(firmaId, startDate, endDate, cancellationToken);
        }

        return await GetOnayliBransDagilim(firmaId, startDate, endDate, cancellationToken);
    }

    private async Task<BransDagilimResponse> GetOnayliBransDagilim(
        int? firmaId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var query = _context.Policeler
            .Where(p => p.OnayDurumu == 1)
            .Where(p => p.TanzimTarihi >= startDate && p.TanzimTarihi <= endDate);

        if (firmaId.HasValue)
        {
            query = query.Where(p => p.IsOrtagiFirmaId == firmaId.Value);
        }

        var policeler = await query.AsNoTracking().ToListAsync(cancellationToken);

        if (policeler.Count == 0)
        {
            return new BransDagilimResponse { Dagilim = new List<BransDagilimItem>(), Mode = DashboardMode.Onayli };
        }

        // Branşları Dictionary ile O(1) lookup
        var bransIds = policeler.Select(p => p.BransId).Distinct().ToList();
        var bransDict = (await _context.Branslar
            .Where(b => bransIds.Contains(b.Id))
            .Select(b => new { b.Id, b.Ad })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(b => b.Id);

        var toplamPrim = policeler.Sum(p => p.BrutPrim);
        var toplamPolice = policeler.Count;

        var dagilim = policeler
            .GroupBy(p => p.BransId)
            .Select(g =>
            {
                bransDict.TryGetValue(g.Key, out var brans);
                return new BransDagilimItem
                {
                    BransId = g.Key,
                    BransAdi = brans?.Ad ?? $"Branş #{g.Key}",
                    PoliceSayisi = g.Count(),
                    ToplamBrutPrim = g.Sum(p => p.BrutPrim),
                    ToplamKomisyon = g.Sum(p => p.Komisyon),
                    Yuzde = toplamPrim > 0 ? Math.Round(g.Sum(p => p.BrutPrim) / toplamPrim * 100, 1) : 0
                };
            })
            .OrderByDescending(x => x.ToplamBrutPrim)
            .ToList();

        return new BransDagilimResponse
        {
            Dagilim = dagilim,
            ToplamPrim = toplamPrim,
            ToplamPolice = toplamPolice,
            Mode = DashboardMode.Onayli
        };
    }

    private async Task<BransDagilimResponse> GetYakalamaBransDagilim(
        int? firmaId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var query = _context.YakalananPoliceler
            .Where(y => y.TanzimTarihi >= startDate && y.TanzimTarihi <= endDate);

        if (firmaId.HasValue)
        {
            query = query.Where(y => y.FirmaId == firmaId.Value);
        }

        var yakalananlar = await query.AsNoTracking().ToListAsync(cancellationToken);

        if (yakalananlar.Count == 0)
        {
            return new BransDagilimResponse { Dagilim = new List<BransDagilimItem>(), Mode = DashboardMode.Yakalama };
        }

        // PoliceTuru'na göre grupla (YakalananPolice'de BransId yerine PoliceTuru var)
        var policeTuruIds = yakalananlar.Select(y => y.PoliceTuru).Distinct().ToList();
        var bransDict = (await _context.Branslar
            .Where(b => policeTuruIds.Contains(b.Id))
            .Select(b => new { b.Id, b.Ad })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(b => b.Id);

        var toplamPrim = (decimal)yakalananlar.Sum(y => y.BrutPrim);
        var toplamPolice = yakalananlar.Count;

        var dagilim = yakalananlar
            .GroupBy(y => y.PoliceTuru)
            .Select(g =>
            {
                bransDict.TryGetValue(g.Key, out var brans);
                var brutPrim = (decimal)g.Sum(y => y.BrutPrim);
                return new BransDagilimItem
                {
                    BransId = g.Key,
                    BransAdi = brans?.Ad ?? $"Branş #{g.Key}",
                    PoliceSayisi = g.Count(),
                    ToplamBrutPrim = brutPrim,
                    ToplamKomisyon = 0, // Yakalanan poliçelerde komisyon yok
                    Yuzde = toplamPrim > 0 ? Math.Round(brutPrim / toplamPrim * 100, 1) : 0
                };
            })
            .OrderByDescending(x => x.ToplamBrutPrim)
            .ToList();

        return new BransDagilimResponse
        {
            Dagilim = dagilim,
            ToplamPrim = toplamPrim,
            ToplamPolice = toplamPolice,
            Mode = DashboardMode.Yakalama
        };
    }
}
