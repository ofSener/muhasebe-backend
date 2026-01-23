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

    // Hangi mod kullanıldı
    public DashboardMode Mode { get; init; }
}

// Query
public record GetDashboardStatsQuery(
    int? FirmaId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    DashboardMode Mode = DashboardMode.Onayli
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
        var firmaId = request.FirmaId ?? _currentUserService.FirmaId;
        var now = _dateTimeService.Now;
        var startDate = request.StartDate ?? new DateTime(now.Year, now.Month, 1);
        var endDate = request.EndDate ?? now;

        // Önceki dönem hesaplama
        var periodLength = (endDate - startDate).Days;
        var prevEndDate = startDate.AddDays(-1);
        var prevStartDate = prevEndDate.AddDays(-periodLength);

        if (request.Mode == DashboardMode.Yakalama)
        {
            return await GetYakalamaStats(firmaId, startDate, endDate, prevStartDate, prevEndDate, cancellationToken);
        }

        return await GetOnayliStats(firmaId, startDate, endDate, prevStartDate, prevEndDate, cancellationToken);
    }

    private async Task<DashboardStatsResponse> GetOnayliStats(
        int? firmaId,
        DateTime startDate,
        DateTime endDate,
        DateTime prevStartDate,
        DateTime prevEndDate,
        CancellationToken cancellationToken)
    {
        // Onaylı poliçeler (OnayDurumu = 1)
        var policeQuery = _context.Policeler
            .Where(p => p.OnayDurumu == 1);

        if (firmaId.HasValue)
        {
            policeQuery = policeQuery.Where(p => p.IsOrtagiFirmaId == firmaId.Value);
        }

        // Mevcut dönem
        var currentPoliceler = await policeQuery
            .Where(p => p.TanzimTarihi >= startDate && p.TanzimTarihi <= endDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Önceki dönem
        var prevPoliceler = await policeQuery
            .Where(p => p.TanzimTarihi >= prevStartDate && p.TanzimTarihi <= prevEndDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Toplam poliçe sayısı (tüm zamanlar)
        var toplamPoliceSayisi = await policeQuery.CountAsync(cancellationToken);

        // Müşteri sayısı
        var musteriQuery = _context.Musteriler.AsQueryable();
        if (firmaId.HasValue)
        {
            musteriQuery = musteriQuery.Where(m => m.EkleyenFirmaId == firmaId.Value);
        }
        var toplamMusteriSayisi = await musteriQuery.CountAsync(cancellationToken);

        // Bekleyen (havuzdaki) poliçeler (OnayDurumu = 0)
        var bekleyenQuery = _context.Policeler.Where(p => p.OnayDurumu == 0);
        if (firmaId.HasValue)
        {
            bekleyenQuery = bekleyenQuery.Where(p => p.IsOrtagiFirmaId == firmaId.Value);
        }
        var bekleyenPoliceler = await bekleyenQuery.AsNoTracking().ToListAsync(cancellationToken);

        // Aktif çalışan sayısı
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
            BekleyenPrim = bekleyenPoliceler.Sum(p => p.BrutPrim),
            AktifCalisanSayisi = aktifCalisanSayisi,
            OncekiDonemBrutPrim = prevPoliceler.Sum(p => p.BrutPrim),
            OncekiDonemKomisyon = prevPoliceler.Sum(p => p.Komisyon),
            OncekiDonemPoliceSayisi = prevPoliceler.Count,
            Mode = DashboardMode.Onayli
        };
    }

    private async Task<DashboardStatsResponse> GetYakalamaStats(
        int? firmaId,
        DateTime startDate,
        DateTime endDate,
        DateTime prevStartDate,
        DateTime prevEndDate,
        CancellationToken cancellationToken)
    {
        // Yakalanan poliçeler
        var yakalamaQuery = _context.YakalananPoliceler.AsQueryable();

        if (firmaId.HasValue)
        {
            yakalamaQuery = yakalamaQuery.Where(y => y.FirmaId == firmaId.Value);
        }

        // Mevcut dönem
        var currentYakalanan = await yakalamaQuery
            .Where(y => y.TanzimTarihi >= startDate && y.TanzimTarihi <= endDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Önceki dönem
        var prevYakalanan = await yakalamaQuery
            .Where(y => y.TanzimTarihi >= prevStartDate && y.TanzimTarihi <= prevEndDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Toplam yakalanan poliçe sayısı
        var toplamPoliceSayisi = await yakalamaQuery.CountAsync(cancellationToken);

        // Müşteri sayısı
        var musteriQuery = _context.Musteriler.AsQueryable();
        if (firmaId.HasValue)
        {
            musteriQuery = musteriQuery.Where(m => m.EkleyenFirmaId == firmaId.Value);
        }
        var toplamMusteriSayisi = await musteriQuery.CountAsync(cancellationToken);

        // Aktif çalışan sayısı
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
            ToplamBrutPrim = (decimal)currentYakalanan.Sum(y => y.BrutPrim),
            ToplamNetPrim = (decimal)currentYakalanan.Sum(y => y.NetPrim),
            ToplamKomisyon = 0, // Yakalanan poliçelerde komisyon yok
            BekleyenPoliceSayisi = toplamPoliceSayisi, // Tümü beklemede
            BekleyenPrim = (decimal)currentYakalanan.Sum(y => y.BrutPrim),
            AktifCalisanSayisi = aktifCalisanSayisi,
            OncekiDonemBrutPrim = (decimal)prevYakalanan.Sum(y => y.BrutPrim),
            OncekiDonemKomisyon = 0,
            OncekiDonemPoliceSayisi = prevYakalanan.Count,
            Mode = DashboardMode.Yakalama
        };
    }
}
