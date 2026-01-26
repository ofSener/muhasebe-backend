using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Extensions;

namespace IhsanAI.Application.Features.Musteriler.Queries;

public record CustomerStatsResponse
{
    public int ToplamMusteriSayisi { get; init; }
    public int BireyselMusteriSayisi { get; init; }
    public int KurumsalMusteriSayisi { get; init; }
    public int BuAyYeniMusteriSayisi { get; init; }
    public int ToplamPoliceSayisi { get; init; }
    public decimal ToplamPrim { get; init; }
}

public record GetCustomerStatsQuery() : IRequest<CustomerStatsResponse>;

public class GetCustomerStatsQueryHandler : IRequestHandler<GetCustomerStatsQuery, CustomerStatsResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public GetCustomerStatsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<CustomerStatsResponse> Handle(GetCustomerStatsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Musteriler.AsQueryable();

        // GÜVENLİK: Token'dan gelen FirmaId ile filtrele
        query = query.ApplyFirmaFilterNullable(_currentUserService, x => x.EkleyenFirmaId);

        var now = _dateTimeService.Now;
        var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);

        // Total customer count
        var toplamMusteriSayisi = await query.CountAsync(cancellationToken);

        // Individual customers (SahipTuru = 1 means bireysel)
        var bireyselMusteriSayisi = await query
            .Where(m => m.SahipTuru == 1)
            .CountAsync(cancellationToken);

        // Corporate customers (SahipTuru = 2 means kurumsal)
        var kurumsalMusteriSayisi = await query
            .Where(m => m.SahipTuru == 2)
            .CountAsync(cancellationToken);

        // New customers this month
        var buAyYeniMusteriSayisi = await query
            .Where(m => m.EklenmeZamani >= firstDayOfMonth)
            .CountAsync(cancellationToken);

        // Policy stats for this firm's customers
        var policeQuery = _context.Policeler
            .Where(p => p.OnayDurumu == 1);

        if (_currentUserService.FirmaId.HasValue)
        {
            policeQuery = policeQuery.Where(p => p.IsOrtagiFirmaId == _currentUserService.FirmaId.Value);
        }

        var toplamPoliceSayisi = await policeQuery.CountAsync(cancellationToken);
        var toplamPrim = await policeQuery.SumAsync(p => p.BrutPrim, cancellationToken);

        return new CustomerStatsResponse
        {
            ToplamMusteriSayisi = toplamMusteriSayisi,
            BireyselMusteriSayisi = bireyselMusteriSayisi,
            KurumsalMusteriSayisi = kurumsalMusteriSayisi,
            BuAyYeniMusteriSayisi = buAyYeniMusteriSayisi,
            ToplamPoliceSayisi = toplamPoliceSayisi,
            ToplamPrim = toplamPrim
        };
    }
}
