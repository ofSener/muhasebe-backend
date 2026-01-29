using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Dashboard.Queries;

// Response DTO
public record TopPerformerItem
{
    public int UyeId { get; init; }
    public string AdSoyad { get; init; } = string.Empty;
    public string? SubeAdi { get; init; }
    public int PoliceSayisi { get; init; }
    public decimal ToplamBrutPrim { get; init; }
    public decimal ToplamKomisyon { get; init; }
    public decimal KazancOrani { get; init; } // Komisyon / BrutPrim yüzdesi
}

public record TopPerformersResponse
{
    public List<TopPerformerItem> Performers { get; init; } = new();
    public decimal ToplamBrutPrim { get; init; }
    public decimal ToplamKomisyon { get; init; }
    public DashboardMode Mode { get; init; }
}

// Query
public record GetTopPerformersQuery(
    int? FirmaId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int Limit = 10,
    DashboardMode Mode = DashboardMode.Onayli,
    DashboardFilters? Filters = null
) : IRequest<TopPerformersResponse>;

// Handler
public class GetTopPerformersQueryHandler : IRequestHandler<GetTopPerformersQuery, TopPerformersResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public GetTopPerformersQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<TopPerformersResponse> Handle(GetTopPerformersQuery request, CancellationToken cancellationToken)
    {
        var firmaId = request.FirmaId ?? _currentUserService.FirmaId;
        var now = _dateTimeService.Now;
        var startDate = request.StartDate ?? new DateTime(now.Year, now.Month, 1);
        var endDate = request.EndDate ?? now;
        var limit = Math.Min(Math.Max(request.Limit, 1), 50);
        var filters = request.Filters ?? new DashboardFilters();

        if (request.Mode == DashboardMode.Yakalama)
        {
            return await GetYakalamaPerformers(firmaId, startDate, endDate, limit, filters, cancellationToken);
        }

        return await GetOnayliPerformers(firmaId, startDate, endDate, limit, filters, cancellationToken);
    }

    private async Task<TopPerformersResponse> GetOnayliPerformers(
        int? firmaId,
        DateTime startDate,
        DateTime endDate,
        int limit,
        DashboardFilters filters,
        CancellationToken cancellationToken)
    {
        var policeQuery = _context.Policeler.Where(p => p.OnayDurumu == 1);
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

        // Kullanıcı bilgilerini getir
        var uyeIds = policeler.Select(p => p.UyeId).Distinct().ToList();
        var kullanicilar = await _context.Kullanicilar
            .Where(k => uyeIds.Contains(k.Id))
            .Select(k => new { k.Id, k.Adi, k.Soyadi, k.SubeId })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Şube bilgilerini getir
        var subeIds = kullanicilar.Where(k => k.SubeId.HasValue).Select(k => k.SubeId!.Value).Distinct().ToList();
        var subeler = await _context.Subeler
            .Where(s => subeIds.Contains(s.Id))
            .Select(s => new { s.Id, s.SubeAdi })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Performans hesaplama
        var performers = policeler
            .GroupBy(p => p.UyeId)
            .Select(g =>
            {
                var kullanici = kullanicilar.FirstOrDefault(k => k.Id == g.Key);
                var sube = kullanici?.SubeId.HasValue == true
                    ? subeler.FirstOrDefault(s => s.Id == kullanici.SubeId.Value)
                    : null;

                var toplamBrutPrim = (decimal)g.Sum(p => p.BrutPrim);
                var toplamKomisyon = (decimal)g.Sum(p => p.Komisyon ?? 0);

                return new TopPerformerItem
                {
                    UyeId = g.Key,
                    AdSoyad = kullanici != null ? $"{kullanici.Adi} {kullanici.Soyadi}".Trim() : $"Kullanıcı #{g.Key}",
                    SubeAdi = sube?.SubeAdi,
                    PoliceSayisi = g.Count(),
                    ToplamBrutPrim = toplamBrutPrim,
                    ToplamKomisyon = toplamKomisyon,
                    KazancOrani = toplamBrutPrim > 0 ? Math.Round(toplamKomisyon / toplamBrutPrim * 100, 2) : 0
                };
            })
            .OrderByDescending(x => x.ToplamBrutPrim)
            .Take(limit)
            .ToList();

        return new TopPerformersResponse
        {
            Performers = performers,
            ToplamBrutPrim = (decimal)policeler.Sum(p => p.BrutPrim),
            ToplamKomisyon = (decimal)policeler.Sum(p => p.Komisyon ?? 0),
            Mode = DashboardMode.Onayli
        };
    }

    private async Task<TopPerformersResponse> GetYakalamaPerformers(
        int? firmaId,
        DateTime startDate,
        DateTime endDate,
        int limit,
        DashboardFilters filters,
        CancellationToken cancellationToken)
    {
        var yakalamaQuery = _context.YakalananPoliceler.AsQueryable();
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

        // Kullanıcı bilgilerini getir (UyeId kullanılıyor)
        var uyeIds = yakalananlar.Select(y => y.UyeId).Distinct().ToList();
        var kullanicilar = await _context.Kullanicilar
            .Where(k => uyeIds.Contains(k.Id))
            .Select(k => new { k.Id, k.Adi, k.Soyadi, k.SubeId })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Şube bilgilerini getir
        var subeIds = kullanicilar.Where(k => k.SubeId.HasValue).Select(k => k.SubeId!.Value).Distinct().ToList();
        var subeler = await _context.Subeler
            .Where(s => subeIds.Contains(s.Id))
            .Select(s => new { s.Id, s.SubeAdi })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Performans hesaplama
        var performers = yakalananlar
            .GroupBy(y => y.UyeId)
            .Select(g =>
            {
                var kullanici = kullanicilar.FirstOrDefault(k => k.Id == g.Key);
                var sube = kullanici?.SubeId.HasValue == true
                    ? subeler.FirstOrDefault(s => s.Id == kullanici.SubeId.Value)
                    : null;

                var toplamBrutPrim = (decimal)g.Sum(y => y.BrutPrim);

                return new TopPerformerItem
                {
                    UyeId = g.Key,
                    AdSoyad = kullanici != null ? $"{kullanici.Adi} {kullanici.Soyadi}".Trim() : $"Kullanıcı #{g.Key}",
                    SubeAdi = sube?.SubeAdi,
                    PoliceSayisi = g.Count(),
                    ToplamBrutPrim = toplamBrutPrim,
                    ToplamKomisyon = 0, // Yakalanan poliçelerde komisyon yok
                    KazancOrani = 0
                };
            })
            .OrderByDescending(x => x.ToplamBrutPrim)
            .Take(limit)
            .ToList();

        return new TopPerformersResponse
        {
            Performers = performers,
            ToplamBrutPrim = (decimal)yakalananlar.Sum(y => y.BrutPrim),
            ToplamKomisyon = 0,
            Mode = DashboardMode.Yakalama
        };
    }
}
