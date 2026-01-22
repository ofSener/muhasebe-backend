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
}

// Query
public record GetTopPerformersQuery(
    int? FirmaId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int Limit = 10
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
        var startDate = request.StartDate ?? new DateTime(now.Year, now.Month, 1); // Bu ay başı
        var endDate = request.EndDate ?? now;
        var limit = Math.Min(Math.Max(request.Limit, 1), 50); // 1-50 arası

        // Poliçeleri getir
        var policeQuery = _context.Policeler.AsQueryable();
        if (firmaId.HasValue)
        {
            policeQuery = policeQuery.Where(p => p.IsOrtagiFirmaId == firmaId.Value);
        }

        var policeler = await policeQuery
            .Where(p => p.TanzimTarihi >= startDate && p.TanzimTarihi <= endDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Kullanıcı bilgilerini getir
        var uyeIds = policeler.Select(p => p.IsOrtagiUyeId).Distinct().ToList();
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
            .GroupBy(p => p.IsOrtagiUyeId)
            .Select(g =>
            {
                var kullanici = kullanicilar.FirstOrDefault(k => k.Id == g.Key);
                var sube = kullanici?.SubeId.HasValue == true
                    ? subeler.FirstOrDefault(s => s.Id == kullanici.SubeId.Value)
                    : null;

                var toplamBrutPrim = g.Sum(p => p.BrutPrim);
                var toplamKomisyon = g.Sum(p => p.IsOrtagiKomisyon);

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
            ToplamBrutPrim = policeler.Sum(p => p.BrutPrim),
            ToplamKomisyon = policeler.Sum(p => p.IsOrtagiKomisyon)
        };
    }
}
