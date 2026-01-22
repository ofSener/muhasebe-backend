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
}

// Query
public record GetAylikTrendQuery(
    int? FirmaId = null,
    int Months = 12
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
        var months = Math.Min(Math.Max(request.Months, 1), 24); // 1-24 ay arası

        // Son X ay için başlangıç tarihi
        var startDate = new DateTime(now.Year, now.Month, 1).AddMonths(-(months - 1));

        // Poliçeleri getir
        var policeQuery = _context.Policeler.AsQueryable();
        if (firmaId.HasValue)
        {
            policeQuery = policeQuery.Where(p => p.IsOrtagiFirmaId == firmaId.Value);
        }

        var policeler = await policeQuery
            .Where(p => p.TanzimTarihi >= startDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Müşterileri getir (yeni müşteri sayısı için)
        var musteriQuery = _context.Musteriler.AsQueryable();
        if (firmaId.HasValue)
        {
            musteriQuery = musteriQuery.Where(m => m.EkleyenFirmaId == firmaId.Value);
        }

        var musteriler = await musteriQuery
            .Where(m => m.EklenmeZamani >= startDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Aylık trend oluştur
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
                BrutPrim = aylikPoliceler.Sum(p => p.BrutPrim),
                NetPrim = aylikPoliceler.Sum(p => p.NetPrim),
                Komisyon = aylikPoliceler.Sum(p => p.Komisyon),
                YeniMusteriSayisi = aylikMusteriler
            });
        }

        return new AylikTrendResponse
        {
            Trend = trend
        };
    }
}
