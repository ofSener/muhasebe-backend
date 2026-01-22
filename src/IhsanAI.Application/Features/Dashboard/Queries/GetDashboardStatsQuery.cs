using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Dashboard.Queries;

// Response DTO
public record DashboardStatsResponse
{
    public int ToplamPoliceSayisi { get; init; }
    public int ToplamMusteriSayisi { get; init; }
    public decimal ToplamBrutPrim { get; init; }
    public decimal ToplamNetPrim { get; init; }
    public decimal ToplamKomisyon { get; init; }
    public int BekleyenPoliceSayisi { get; init; }
    public decimal BekleyenPrim { get; init; }
    public int AktifCalisanSayisi { get; init; }

    // Karşılaştırma için önceki dönem
    public decimal OncekiDonemBrutPrim { get; init; }
    public decimal OncekiDonemKomisyon { get; init; }
    public int OncekiDonemPoliceSayisi { get; init; }
}

// Query
public record GetDashboardStatsQuery(
    int? FirmaId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null
) : IRequest<DashboardStatsResponse>;

// Handler
public class GetDashboardStatsQueryHandler : IRequestHandler<GetDashboardStatsQuery, DashboardStatsResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public GetDashboardStatsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<DashboardStatsResponse> Handle(GetDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        // Firma ID belirleme
        var firmaId = request.FirmaId ?? _currentUserService.FirmaId;

        // Tarih aralığı belirleme (varsayılan: bu ay)
        var now = _dateTimeService.Now;
        var startDate = request.StartDate ?? new DateTime(now.Year, now.Month, 1);
        var endDate = request.EndDate ?? now;

        // Önceki dönem hesaplama (aynı uzunlukta önceki dönem)
        var periodLength = (endDate - startDate).Days;
        var prevEndDate = startDate.AddDays(-1);
        var prevStartDate = prevEndDate.AddDays(-periodLength);

        // Police sorgusu
        var policeQuery = _context.Policeler.AsQueryable();

        if (firmaId.HasValue)
        {
            policeQuery = policeQuery.Where(p => p.IsOrtagiFirmaId == firmaId.Value);
        }

        // Mevcut dönem poliçeleri
        var currentPoliceler = await policeQuery
            .Where(p => p.TanzimTarihi >= startDate && p.TanzimTarihi <= endDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Önceki dönem poliçeleri
        var prevPoliceler = await policeQuery
            .Where(p => p.TanzimTarihi >= prevStartDate && p.TanzimTarihi <= prevEndDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Toplam (tüm zamanlar) poliçe sayısı
        var toplamPoliceSayisi = await policeQuery.CountAsync(cancellationToken);

        // Müşteri sayısı
        var musteriQuery = _context.Musteriler.AsQueryable();
        if (firmaId.HasValue)
        {
            musteriQuery = musteriQuery.Where(m => m.EkleyenFirmaId == firmaId.Value);
        }
        var toplamMusteriSayisi = await musteriQuery.CountAsync(cancellationToken);

        // Bekleyen (yakalanan) poliçeler
        var yakalananQuery = _context.YakalananPoliceler.AsQueryable();
        if (firmaId.HasValue)
        {
            yakalananQuery = yakalananQuery.Where(y => y.FirmaId == firmaId.Value);
        }
        var bekleyenPoliceler = await yakalananQuery.AsNoTracking().ToListAsync(cancellationToken);

        // Aktif çalışan sayısı (Onay = 1 olan kullanıcılar)
        var calisanQuery = _context.Kullanicilar.AsQueryable();
        if (firmaId.HasValue)
        {
            calisanQuery = calisanQuery.Where(k => k.FirmaId == firmaId.Value);
        }
        var aktifCalisanSayisi = await calisanQuery
            .Where(k => k.Onay == 1)
            .CountAsync(cancellationToken);

        return new DashboardStatsResponse
        {
            ToplamPoliceSayisi = toplamPoliceSayisi,
            ToplamMusteriSayisi = toplamMusteriSayisi,
            ToplamBrutPrim = currentPoliceler.Sum(p => p.BrutPrim),
            ToplamNetPrim = currentPoliceler.Sum(p => p.NetPrim),
            ToplamKomisyon = currentPoliceler.Sum(p => p.Komisyon),
            BekleyenPoliceSayisi = bekleyenPoliceler.Count,
            BekleyenPrim = (decimal)bekleyenPoliceler.Sum(y => y.BrutPrim),
            AktifCalisanSayisi = aktifCalisanSayisi,
            OncekiDonemBrutPrim = prevPoliceler.Sum(p => p.BrutPrim),
            OncekiDonemKomisyon = prevPoliceler.Sum(p => p.Komisyon),
            OncekiDonemPoliceSayisi = prevPoliceler.Count
        };
    }
}
