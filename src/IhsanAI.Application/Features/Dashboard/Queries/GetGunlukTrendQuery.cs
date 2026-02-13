using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using System.Globalization;
using IhsanAI.Application.Features.Policeler.Queries;
using IhsanAI.Application.Features.YakalananPoliceler.Queries;

namespace IhsanAI.Application.Features.Dashboard.Queries;

// Response DTO
public record GunlukTrendItem
{
    public string Gun { get; init; } = string.Empty;   // "15 Oca" formatında
    public DateTime Tarih { get; init; }
    public int GunSirasi { get; init; }                 // Ayın günü: 1..31
    public int PoliceSayisi { get; init; }
    public decimal BrutPrim { get; init; }
    public decimal NetPrim { get; init; }
    public decimal Komisyon { get; init; }
}

public record GunlukTrendResponse
{
    public List<GunlukTrendItem> Trend { get; init; } = new();
    public DashboardMode Mode { get; init; }
}

// Query
public record GetGunlukTrendQuery(
    int? FirmaId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    DashboardMode Mode = DashboardMode.Onayli,
    DashboardFilters? Filters = null
) : IRequest<GunlukTrendResponse>;

// Handler
public class GetGunlukTrendQueryHandler : IRequestHandler<GetGunlukTrendQuery, GunlukTrendResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public GetGunlukTrendQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<GunlukTrendResponse> Handle(GetGunlukTrendQuery request, CancellationToken cancellationToken)
    {
        var firmaId = request.FirmaId ?? _currentUserService.FirmaId;
        var now = _dateTimeService.Now;
        var startDate = request.StartDate ?? new DateTime(now.Year, now.Month, 1);
        var endDate = request.EndDate ?? now;
        var filters = request.Filters ?? new DashboardFilters();

        // Maksimum 366 gün
        if ((endDate - startDate).TotalDays > 366)
            startDate = endDate.AddDays(-366);

        if (request.Mode == DashboardMode.Yakalama)
        {
            return await GetYakalamaGunlukTrend(firmaId, startDate, endDate, filters, cancellationToken);
        }

        return await GetOnayliGunlukTrend(firmaId, startDate, endDate, filters, cancellationToken);
    }

    private async Task<GunlukTrendResponse> GetOnayliGunlukTrend(
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

        var trend = BuildOnayliTrend(startDate, endDate, policeler);

        return new GunlukTrendResponse
        {
            Trend = trend,
            Mode = DashboardMode.Onayli
        };
    }

    private async Task<GunlukTrendResponse> GetYakalamaGunlukTrend(
        int? firmaId,
        DateTime startDate,
        DateTime endDate,
        DashboardFilters filters,
        CancellationToken cancellationToken)
    {
        var query = _context.YakalananPoliceler
            .AsQueryable()
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

        var trend = BuildYakalamaTrend(startDate, endDate, yakalananlar);

        return new GunlukTrendResponse
        {
            Trend = trend,
            Mode = DashboardMode.Yakalama
        };
    }

    private List<GunlukTrendItem> BuildOnayliTrend(
        DateTime startDate,
        DateTime endDate,
        List<Domain.Entities.Police> policeler)
    {
        var trend = new List<GunlukTrendItem>();
        var turkishCulture = new CultureInfo("tr-TR");
        var days = (int)(endDate.Date - startDate.Date).TotalDays + 1;

        for (int i = 0; i < days; i++)
        {
            var gun = startDate.Date.AddDays(i);
            var gunSonu = gun.AddDays(1);

            var gunlukPoliceler = policeler
                .Where(p => p.TanzimTarihi >= gun && p.TanzimTarihi < gunSonu)
                .ToList();

            trend.Add(new GunlukTrendItem
            {
                Gun = gun.ToString("d MMM", turkishCulture),
                Tarih = gun,
                GunSirasi = gun.Day,
                PoliceSayisi = gunlukPoliceler.Count,
                BrutPrim = (decimal)gunlukPoliceler.Sum(p => p.BrutPrim),
                NetPrim = (decimal)gunlukPoliceler.Sum(p => p.NetPrim),
                Komisyon = (decimal)gunlukPoliceler.Sum(p => p.Komisyon ?? 0)
            });
        }

        return trend;
    }

    private List<GunlukTrendItem> BuildYakalamaTrend(
        DateTime startDate,
        DateTime endDate,
        List<Domain.Entities.YakalananPolice> yakalananlar)
    {
        var trend = new List<GunlukTrendItem>();
        var turkishCulture = new CultureInfo("tr-TR");
        var days = (int)(endDate.Date - startDate.Date).TotalDays + 1;

        for (int i = 0; i < days; i++)
        {
            var gun = startDate.Date.AddDays(i);
            var gunSonu = gun.AddDays(1);

            var gunlukYakalananlar = yakalananlar
                .Where(y => y.TanzimTarihi >= gun && y.TanzimTarihi < gunSonu)
                .ToList();

            trend.Add(new GunlukTrendItem
            {
                Gun = gun.ToString("d MMM", turkishCulture),
                Tarih = gun,
                GunSirasi = gun.Day,
                PoliceSayisi = gunlukYakalananlar.Count,
                BrutPrim = (decimal)gunlukYakalananlar.Sum(y => y.BrutPrim),
                NetPrim = (decimal)gunlukYakalananlar.Sum(y => y.NetPrim),
                Komisyon = 0 // Yakalanan poliçelerde komisyon yok
            });
        }

        return trend;
    }
}
