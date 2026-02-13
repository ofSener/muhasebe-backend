using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using System.Globalization;
using IhsanAI.Application.Features.Policeler.Queries;
using IhsanAI.Application.Features.YakalananPoliceler.Queries;

namespace IhsanAI.Application.Features.Dashboard.Queries;

// Response DTO'ları
public record KarsilastirmaTrendItem
{
    public string Etiket { get; init; } = string.Empty;  // "15 Oca" veya "Oca 2025"
    public DateTime Tarih { get; init; }
    public decimal BrutPrim { get; init; }
    public int PoliceSayisi { get; init; }
}

public record KarsilastirmaEntitySeries
{
    public int EntityId { get; init; }
    public string EntityAdi { get; init; } = string.Empty;
    public List<KarsilastirmaTrendItem> Trend { get; init; } = new();
}

public record KarsilastirmaTrendResponse
{
    public List<KarsilastirmaEntitySeries> Series { get; init; } = new();
    public string Granularity { get; init; } = "daily";  // "daily" veya "monthly"
    public DashboardMode Mode { get; init; }
}

// Query
public record GetKarsilastirmaTrendQuery(
    int? FirmaId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string GroupBy = "calisan",        // "calisan" veya "sube"
    string? EntityIds = null,          // Virgülle ayrılmış ID'ler
    DashboardMode Mode = DashboardMode.Onayli,
    DashboardFilters? Filters = null
) : IRequest<KarsilastirmaTrendResponse>;

// Handler
public class GetKarsilastirmaTrendQueryHandler : IRequestHandler<GetKarsilastirmaTrendQuery, KarsilastirmaTrendResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public GetKarsilastirmaTrendQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<KarsilastirmaTrendResponse> Handle(GetKarsilastirmaTrendQuery request, CancellationToken cancellationToken)
    {
        var firmaId = request.FirmaId ?? _currentUserService.FirmaId;
        var now = _dateTimeService.Now;
        var startDate = request.StartDate ?? new DateTime(now.Year, now.Month, 1);
        var endDate = request.EndDate ?? now;
        var filters = request.Filters ?? new DashboardFilters();
        var entityIds = ParseEntityIds(request.EntityIds);

        if (entityIds.Count == 0)
        {
            return new KarsilastirmaTrendResponse { Mode = request.Mode };
        }

        var granularity = (endDate - startDate).TotalDays <= 31 ? "daily" : "monthly";

        if (request.Mode == DashboardMode.Yakalama)
        {
            return await GetYakalamaTrend(firmaId, startDate, endDate, request.GroupBy, entityIds, filters, granularity, cancellationToken);
        }

        return await GetOnayliTrend(firmaId, startDate, endDate, request.GroupBy, entityIds, filters, granularity, cancellationToken);
    }

    private static List<int> ParseEntityIds(string? ids)
    {
        if (string.IsNullOrEmpty(ids)) return new List<int>();
        return ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var id) ? (int?)id : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
    }

    private async Task<KarsilastirmaTrendResponse> GetOnayliTrend(
        int? firmaId,
        DateTime startDate,
        DateTime endDate,
        string groupBy,
        List<int> entityIds,
        DashboardFilters filters,
        string granularity,
        CancellationToken cancellationToken)
    {
        var query = _context.Policeler
            .Where(p => p.OnayDurumu == 1)
            .ApplyAuthorizationFilters(_currentUserService)
            .Where(p => p.TanzimTarihi >= startDate && p.TanzimTarihi <= endDate);

        if (firmaId.HasValue)
            query = query.Where(p => p.FirmaId == firmaId.Value);

        // Apply DashboardFilters
        if (filters.BransIds.Count > 0)
            query = query.Where(p => filters.BransIds.Contains(p.PoliceTuruId));
        if (filters.SubeIds.Count > 0)
            query = query.Where(p => filters.SubeIds.Contains(p.SubeId));
        if (filters.SirketIds.Count > 0)
            query = query.Where(p => filters.SirketIds.Contains(p.SigortaSirketiId));

        // Entity filtresi
        if (groupBy == "sube")
            query = query.Where(p => entityIds.Contains(p.SubeId));
        else
            query = query.Where(p => entityIds.Contains(p.UyeId));

        var policeler = await query.AsNoTracking().ToListAsync(cancellationToken);

        // Entity isimlerini çözümle
        var entityNames = await ResolveEntityNames(groupBy, entityIds, cancellationToken);

        // Seri oluştur
        var series = BuildOnayliSeries(policeler, groupBy, entityIds, entityNames, startDate, endDate, granularity);

        return new KarsilastirmaTrendResponse
        {
            Series = series,
            Granularity = granularity,
            Mode = DashboardMode.Onayli
        };
    }

    private async Task<KarsilastirmaTrendResponse> GetYakalamaTrend(
        int? firmaId,
        DateTime startDate,
        DateTime endDate,
        string groupBy,
        List<int> entityIds,
        DashboardFilters filters,
        string granularity,
        CancellationToken cancellationToken)
    {
        var query = _context.YakalananPoliceler
            .AsQueryable()
            .ApplyAuthorizationFilters(_currentUserService)
            .Where(y => y.TanzimTarihi >= startDate && y.TanzimTarihi <= endDate);

        if (firmaId.HasValue)
            query = query.Where(y => y.FirmaId == firmaId.Value);

        // Apply DashboardFilters
        if (filters.BransIds.Count > 0)
            query = query.Where(y => filters.BransIds.Contains(y.PoliceTuru));
        if (filters.SubeIds.Count > 0)
            query = query.Where(y => filters.SubeIds.Contains(y.SubeId));
        if (filters.SirketIds.Count > 0)
            query = query.Where(y => filters.SirketIds.Contains(y.SigortaSirketi));
        if (filters.KullaniciIds.Count > 0)
            query = query.Where(y => filters.KullaniciIds.Contains(y.ProduktorId));

        // Entity filtresi
        if (groupBy == "sube")
            query = query.Where(y => entityIds.Contains(y.SubeId));
        else
            query = query.Where(y => entityIds.Contains(y.UyeId));

        var yakalananlar = await query.AsNoTracking().ToListAsync(cancellationToken);

        // Entity isimlerini çözümle
        var entityNames = await ResolveEntityNames(groupBy, entityIds, cancellationToken);

        // Seri oluştur
        var series = BuildYakalamaSeries(yakalananlar, groupBy, entityIds, entityNames, startDate, endDate, granularity);

        return new KarsilastirmaTrendResponse
        {
            Series = series,
            Granularity = granularity,
            Mode = DashboardMode.Yakalama
        };
    }

    private async Task<Dictionary<int, string>> ResolveEntityNames(
        string groupBy,
        List<int> entityIds,
        CancellationToken cancellationToken)
    {
        var names = new Dictionary<int, string>();

        if (groupBy == "sube")
        {
            var subeler = await _context.Subeler
                .Where(s => entityIds.Contains(s.Id))
                .Select(s => new { s.Id, s.SubeAdi })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            foreach (var s in subeler)
                names[s.Id] = s.SubeAdi ?? $"Şube #{s.Id}";
        }
        else
        {
            // Aktif kullanıcılar
            var kullanicilar = await _context.Kullanicilar
                .Where(k => entityIds.Contains(k.Id))
                .Select(k => new { k.Id, k.Adi, k.Soyadi })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            foreach (var k in kullanicilar)
                names[k.Id] = $"{k.Adi} {k.Soyadi}".Trim();

            // Eksik olanları eski tablodan ara
            var eksikIds = entityIds.Where(id => !names.ContainsKey(id)).ToList();
            if (eksikIds.Count > 0)
            {
                var eskiKullanicilar = await _context.KullanicilarEski
                    .Where(k => eksikIds.Contains(k.Id))
                    .Select(k => new { k.Id, k.Adi, k.Soyadi })
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                foreach (var k in eskiKullanicilar)
                    names[k.Id] = $"{k.Adi} {k.Soyadi}".Trim();
            }
        }

        // İsim bulunamayan entity'ler için fallback
        foreach (var id in entityIds)
        {
            if (!names.ContainsKey(id))
                names[id] = groupBy == "sube" ? $"Şube #{id}" : $"Kullanıcı #{id}";
        }

        return names;
    }

    private List<KarsilastirmaEntitySeries> BuildOnayliSeries(
        List<Domain.Entities.Police> policeler,
        string groupBy,
        List<int> entityIds,
        Dictionary<int, string> entityNames,
        DateTime startDate,
        DateTime endDate,
        string granularity)
    {
        var turkishCulture = new CultureInfo("tr-TR");
        var series = new List<KarsilastirmaEntitySeries>();

        foreach (var entityId in entityIds)
        {
            var entityPoliceler = groupBy == "sube"
                ? policeler.Where(p => p.SubeId == entityId).ToList()
                : policeler.Where(p => p.UyeId == entityId).ToList();

            var trend = new List<KarsilastirmaTrendItem>();

            if (granularity == "daily")
            {
                var days = (int)(endDate.Date - startDate.Date).TotalDays + 1;
                for (int i = 0; i < days; i++)
                {
                    var gun = startDate.Date.AddDays(i);
                    var gunSonu = gun.AddDays(1);
                    var gunluk = entityPoliceler.Where(p => p.TanzimTarihi >= gun && p.TanzimTarihi < gunSonu).ToList();

                    trend.Add(new KarsilastirmaTrendItem
                    {
                        Etiket = gun.ToString("d MMM", turkishCulture),
                        Tarih = gun,
                        BrutPrim = (decimal)gunluk.Sum(p => p.BrutPrim),
                        PoliceSayisi = gunluk.Count
                    });
                }
            }
            else
            {
                var ayStart = new DateTime(startDate.Year, startDate.Month, 1);
                var ayEnd = new DateTime(endDate.Year, endDate.Month, 1);
                while (ayStart <= ayEnd)
                {
                    var ayBitis = ayStart.AddMonths(1);
                    var aylik = entityPoliceler.Where(p => p.TanzimTarihi >= ayStart && p.TanzimTarihi < ayBitis).ToList();

                    trend.Add(new KarsilastirmaTrendItem
                    {
                        Etiket = ayStart.ToString("MMM yyyy", turkishCulture),
                        Tarih = ayStart,
                        BrutPrim = (decimal)aylik.Sum(p => p.BrutPrim),
                        PoliceSayisi = aylik.Count
                    });

                    ayStart = ayBitis;
                }
            }

            series.Add(new KarsilastirmaEntitySeries
            {
                EntityId = entityId,
                EntityAdi = entityNames.GetValueOrDefault(entityId, $"#{entityId}"),
                Trend = trend
            });
        }

        return series;
    }

    private List<KarsilastirmaEntitySeries> BuildYakalamaSeries(
        List<Domain.Entities.YakalananPolice> yakalananlar,
        string groupBy,
        List<int> entityIds,
        Dictionary<int, string> entityNames,
        DateTime startDate,
        DateTime endDate,
        string granularity)
    {
        var turkishCulture = new CultureInfo("tr-TR");
        var series = new List<KarsilastirmaEntitySeries>();

        foreach (var entityId in entityIds)
        {
            var entityYakalananlar = groupBy == "sube"
                ? yakalananlar.Where(y => y.SubeId == entityId).ToList()
                : yakalananlar.Where(y => y.UyeId == entityId).ToList();

            var trend = new List<KarsilastirmaTrendItem>();

            if (granularity == "daily")
            {
                var days = (int)(endDate.Date - startDate.Date).TotalDays + 1;
                for (int i = 0; i < days; i++)
                {
                    var gun = startDate.Date.AddDays(i);
                    var gunSonu = gun.AddDays(1);
                    var gunluk = entityYakalananlar.Where(y => y.TanzimTarihi >= gun && y.TanzimTarihi < gunSonu).ToList();

                    trend.Add(new KarsilastirmaTrendItem
                    {
                        Etiket = gun.ToString("d MMM", turkishCulture),
                        Tarih = gun,
                        BrutPrim = (decimal)gunluk.Sum(y => y.BrutPrim),
                        PoliceSayisi = gunluk.Count
                    });
                }
            }
            else
            {
                var ayStart = new DateTime(startDate.Year, startDate.Month, 1);
                var ayEnd = new DateTime(endDate.Year, endDate.Month, 1);
                while (ayStart <= ayEnd)
                {
                    var ayBitis = ayStart.AddMonths(1);
                    var aylik = entityYakalananlar.Where(y => y.TanzimTarihi >= ayStart && y.TanzimTarihi < ayBitis).ToList();

                    trend.Add(new KarsilastirmaTrendItem
                    {
                        Etiket = ayStart.ToString("MMM yyyy", turkishCulture),
                        Tarih = ayStart,
                        BrutPrim = (decimal)aylik.Sum(y => y.BrutPrim),
                        PoliceSayisi = aylik.Count
                    });

                    ayStart = ayBitis;
                }
            }

            series.Add(new KarsilastirmaEntitySeries
            {
                EntityId = entityId,
                EntityAdi = entityNames.GetValueOrDefault(entityId, $"#{entityId}"),
                Trend = trend
            });
        }

        return series;
    }
}
