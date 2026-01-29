using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using System.Globalization;

namespace IhsanAI.Application.Features.Dashboard.Queries;

// Response DTO
public record AylikTrendItem
{
    public string Ay { get; init; } = string.Empty;
    public int Yil { get; init; }
    public int AySirasi { get; init; }
    public int PoliceSayisi { get; init; }
    public decimal BrutPrim { get; init; }
    public decimal NetPrim { get; init; }
    public decimal Komisyon { get; init; }
    public int YeniMusteriSayisi { get; init; }
}

public record AylikTrendResponse
{
    public List<AylikTrendItem> Trend { get; init; } = new();
    public DashboardMode Mode { get; init; }
}

// Query
public record GetAylikTrendQuery(
    int? FirmaId = null,
    int Months = 12,
    DashboardMode Mode = DashboardMode.Onayli
) : IRequest<AylikTrendResponse>;

// Handler
public class GetAylikTrendQueryHandler : IRequestHandler<GetAylikTrendQuery, AylikTrendResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public GetAylikTrendQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<AylikTrendResponse> Handle(GetAylikTrendQuery request, CancellationToken cancellationToken)
    {
        var firmaId = request.FirmaId ?? _currentUserService.FirmaId;
        var now = _dateTimeService.Now;
        var months = Math.Min(Math.Max(request.Months, 1), 24);
        var startDate = new DateTime(now.Year, now.Month, 1).AddMonths(-(months - 1));

        if (request.Mode == DashboardMode.Yakalama)
        {
            return await GetYakalamaTrend(firmaId, startDate, months, cancellationToken);
        }

        return await GetOnayliTrend(firmaId, startDate, months, cancellationToken);
    }

    private async Task<AylikTrendResponse> GetOnayliTrend(
        int? firmaId,
        DateTime startDate,
        int months,
        CancellationToken cancellationToken)
    {
        var policeQuery = _context.Policeler.Where(p => p.OnayDurumu == 1);
        if (firmaId.HasValue)
        {
            policeQuery = policeQuery.Where(p => p.FirmaId == firmaId.Value);
        }

        var policeler = await policeQuery
            .Where(p => p.TanzimTarihi >= startDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var musteriQuery = _context.Musteriler.AsQueryable();
        if (firmaId.HasValue)
        {
            musteriQuery = musteriQuery.Where(m => m.EkleyenFirmaId == firmaId.Value);
        }

        var musteriler = await musteriQuery
            .Where(m => m.EklenmeZamani >= startDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var trend = BuildTrend(months, startDate, policeler, musteriler, false);

        return new AylikTrendResponse
        {
            Trend = trend,
            Mode = DashboardMode.Onayli
        };
    }

    private async Task<AylikTrendResponse> GetYakalamaTrend(
        int? firmaId,
        DateTime startDate,
        int months,
        CancellationToken cancellationToken)
    {
        var yakalamaQuery = _context.YakalananPoliceler.AsQueryable();
        if (firmaId.HasValue)
        {
            yakalamaQuery = yakalamaQuery.Where(y => y.FirmaId == firmaId.Value);
        }

        var yakalananlar = await yakalamaQuery
            .Where(y => y.TanzimTarihi >= startDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var musteriQuery = _context.Musteriler.AsQueryable();
        if (firmaId.HasValue)
        {
            musteriQuery = musteriQuery.Where(m => m.EkleyenFirmaId == firmaId.Value);
        }

        var musteriler = await musteriQuery
            .Where(m => m.EklenmeZamani >= startDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var trend = BuildYakalamaTrend(months, startDate, yakalananlar, musteriler);

        return new AylikTrendResponse
        {
            Trend = trend,
            Mode = DashboardMode.Yakalama
        };
    }

    private List<AylikTrendItem> BuildTrend(
        int months,
        DateTime startDate,
        List<Domain.Entities.Police> policeler,
        List<Domain.Entities.Musteri> musteriler,
        bool isYakalama)
    {
        var trend = new List<AylikTrendItem>();
        var turkishCulture = new CultureInfo("tr-TR");

        for (int i = 0; i < months; i++)
        {
            var ay = startDate.AddMonths(i);
            var ayBaslangic = new DateTime(ay.Year, ay.Month, 1);
            var ayBitis = ayBaslangic.AddMonths(1);

            var aylikPoliceler = policeler
                .Where(p => p.TanzimTarihi >= ayBaslangic && p.TanzimTarihi < ayBitis)
                .ToList();

            var aylikMusteriler = musteriler
                .Where(m => m.EklenmeZamani >= ayBaslangic && m.EklenmeZamani < ayBitis)
                .Count();

            trend.Add(new AylikTrendItem
            {
                Ay = ay.ToString("MMM", turkishCulture),
                Yil = ay.Year,
                AySirasi = ay.Month,
                PoliceSayisi = aylikPoliceler.Count,
                BrutPrim = (decimal)aylikPoliceler.Sum(p => p.BrutPrim),
                NetPrim = (decimal)aylikPoliceler.Sum(p => p.NetPrim),
                Komisyon = (decimal)aylikPoliceler.Sum(p => p.Komisyon ?? 0),
                YeniMusteriSayisi = aylikMusteriler
            });
        }

        return trend;
    }

    private List<AylikTrendItem> BuildYakalamaTrend(
        int months,
        DateTime startDate,
        List<Domain.Entities.YakalananPolice> yakalananlar,
        List<Domain.Entities.Musteri> musteriler)
    {
        var trend = new List<AylikTrendItem>();
        var turkishCulture = new CultureInfo("tr-TR");

        for (int i = 0; i < months; i++)
        {
            var ay = startDate.AddMonths(i);
            var ayBaslangic = new DateTime(ay.Year, ay.Month, 1);
            var ayBitis = ayBaslangic.AddMonths(1);

            var aylikYakalananlar = yakalananlar
                .Where(y => y.TanzimTarihi >= ayBaslangic && y.TanzimTarihi < ayBitis)
                .ToList();

            var aylikMusteriler = musteriler
                .Where(m => m.EklenmeZamani >= ayBaslangic && m.EklenmeZamani < ayBitis)
                .Count();

            trend.Add(new AylikTrendItem
            {
                Ay = ay.ToString("MMM", turkishCulture),
                Yil = ay.Year,
                AySirasi = ay.Month,
                PoliceSayisi = aylikYakalananlar.Count,
                BrutPrim = (decimal)aylikYakalananlar.Sum(y => y.BrutPrim),
                NetPrim = (decimal)aylikYakalananlar.Sum(y => y.NetPrim),
                Komisyon = 0, // Yakalanan poliçelerde komisyon yok
                YeniMusteriSayisi = aylikMusteriler
            });
        }

        return trend;
    }
}
