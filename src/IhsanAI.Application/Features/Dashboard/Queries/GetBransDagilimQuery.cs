using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Dashboard.Queries;

// Dashboard Filters
public record DashboardFilters
{
    public List<int> BransIds { get; init; } = new();
    public List<int> KullaniciIds { get; init; } = new();
    public List<int> SubeIds { get; init; } = new();
    public List<int> SirketIds { get; init; } = new();

    public DashboardFilters() { }

    public DashboardFilters(string? bransIds, string? kullaniciIds, string? subeIds, string? sirketIds)
    {
        BransIds = ParseIds(bransIds);
        KullaniciIds = ParseIds(kullaniciIds);
        SubeIds = ParseIds(subeIds);
        SirketIds = ParseIds(sirketIds);
    }

    private static List<int> ParseIds(string? ids)
    {
        if (string.IsNullOrEmpty(ids)) return new List<int>();
        return ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();
    }

    public bool HasFilters => BransIds.Count > 0 || KullaniciIds.Count > 0 || SubeIds.Count > 0 || SirketIds.Count > 0;
}

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
    DashboardMode Mode = DashboardMode.Onayli,
    DashboardFilters? Filters = null
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
        var filters = request.Filters ?? new DashboardFilters();

        if (request.Mode == DashboardMode.Yakalama)
        {
            return await GetYakalamaBransDagilim(firmaId, startDate, endDate, filters, cancellationToken);
        }

        return await GetOnayliBransDagilim(firmaId, startDate, endDate, filters, cancellationToken);
    }

    private async Task<BransDagilimResponse> GetOnayliBransDagilim(
        int? firmaId,
        DateTime startDate,
        DateTime endDate,
        DashboardFilters filters,
        CancellationToken cancellationToken)
    {
        var query = _context.Policeler
            .Where(p => p.OnayDurumu == 1)
            .Where(p => p.TanzimTarihi >= startDate && p.TanzimTarihi <= endDate);

        if (firmaId.HasValue)
        {
            query = query.Where(p => p.IsOrtagiFirmaId == firmaId.Value);
        }

        // Apply filters
        if (filters.BransIds.Count > 0)
            query = query.Where(p => filters.BransIds.Contains(p.BransId));
        if (filters.SubeIds.Count > 0)
            query = query.Where(p => filters.SubeIds.Contains(p.IsOrtagiSubeId));
        if (filters.SirketIds.Count > 0)
            query = query.Where(p => filters.SirketIds.Contains(p.SigortaSirketiId));

        var policeler = await query.AsNoTracking().ToListAsync(cancellationToken);

        if (policeler.Count == 0)
        {
            return new BransDagilimResponse { Dagilim = new List<BransDagilimItem>(), Mode = DashboardMode.Onayli };
        }

        // Branşları sigortapoliceturleri tablosundan al (PoliceTurleri)
        var bransIds = policeler.Select(p => p.BransId).Distinct().ToList();
        var policeTuruDict = (await _context.PoliceTurleri
            .Where(pt => bransIds.Contains(pt.Id))
            .Select(pt => new { pt.Id, pt.Turu })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(pt => pt.Id);

        var toplamPrim = policeler.Sum(p => p.BrutPrim);
        var toplamPolice = policeler.Count;

        var dagilim = policeler
            .GroupBy(p => p.BransId)
            .Select(g =>
            {
                policeTuruDict.TryGetValue(g.Key, out var policeTuru);
                return new BransDagilimItem
                {
                    BransId = g.Key,
                    BransAdi = policeTuru?.Turu ?? $"Branş #{g.Key}",
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
        DashboardFilters filters,
        CancellationToken cancellationToken)
    {
        var query = _context.YakalananPoliceler
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
            return new BransDagilimResponse { Dagilim = new List<BransDagilimItem>(), Mode = DashboardMode.Yakalama };
        }

        // PoliceTuru'na göre grupla - sigortapoliceturleri tablosundan tür adını al
        var policeTuruIds = yakalananlar.Select(y => y.PoliceTuru).Distinct().ToList();
        var policeTuruDict = (await _context.PoliceTurleri
            .Where(pt => policeTuruIds.Contains(pt.Id))
            .Select(pt => new { pt.Id, pt.Turu })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
            .ToDictionary(pt => pt.Id);

        var toplamPrim = (decimal)yakalananlar.Sum(y => y.BrutPrim);
        var toplamPolice = yakalananlar.Count;

        var dagilim = yakalananlar
            .GroupBy(y => y.PoliceTuru)
            .Select(g =>
            {
                policeTuruDict.TryGetValue(g.Key, out var policeTuru);
                var brutPrim = (decimal)g.Sum(y => y.BrutPrim);
                return new BransDagilimItem
                {
                    BransId = g.Key,
                    BransAdi = policeTuru?.Turu ?? $"Tür #{g.Key}",
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
