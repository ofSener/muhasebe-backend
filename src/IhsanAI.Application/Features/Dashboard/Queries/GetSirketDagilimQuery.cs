using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Dashboard.Queries;

// Response DTO
public record SirketDagilimItem
{
    public int SirketId { get; init; }
    public string SirketAdi { get; init; } = string.Empty;
    public string SirketKodu { get; init; } = string.Empty;
    public int PoliceSayisi { get; init; }
    public decimal ToplamBrutPrim { get; init; }
    public decimal ToplamKomisyon { get; init; }
    public decimal Yuzde { get; init; }
}

public record SirketDagilimResponse
{
    public List<SirketDagilimItem> Dagilim { get; init; } = new();
    public decimal ToplamPrim { get; init; }
    public int ToplamPolice { get; init; }
}

// Query
public record GetSirketDagilimQuery(
    int? FirmaId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int Limit = 10
) : IRequest<SirketDagilimResponse>;

// Handler
public class GetSirketDagilimQueryHandler : IRequestHandler<GetSirketDagilimQuery, SirketDagilimResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    public GetSirketDagilimQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<SirketDagilimResponse> Handle(GetSirketDagilimQuery request, CancellationToken cancellationToken)
    {
        var firmaId = request.FirmaId ?? _currentUserService.FirmaId;
        var now = _dateTimeService.Now;
        var startDate = request.StartDate ?? new DateTime(now.Year, 1, 1); // Yıl başından itibaren
        var endDate = request.EndDate ?? now;
        var limit = Math.Min(Math.Max(request.Limit, 1), 50);

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

        // Sigorta şirketlerini getir
        var sirketIds = policeler.Select(p => p.SigortaSirketiId).Distinct().ToList();
        var sirketler = await _context.SigortaSirketleri
            .Where(s => sirketIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Ad, s.Kod })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var toplamPrim = policeler.Sum(p => p.BrutPrim);
        var toplamPolice = policeler.Count;

        // Şirket dağılımı hesapla
        var dagilim = policeler
            .GroupBy(p => p.SigortaSirketiId)
            .Select(g =>
            {
                var sirket = sirketler.FirstOrDefault(s => s.Id == g.Key);
                var brutPrim = g.Sum(p => p.BrutPrim);

                return new SirketDagilimItem
                {
                    SirketId = g.Key,
                    SirketAdi = sirket?.Ad ?? $"Şirket #{g.Key}",
                    SirketKodu = sirket?.Kod ?? "",
                    PoliceSayisi = g.Count(),
                    ToplamBrutPrim = brutPrim,
                    ToplamKomisyon = g.Sum(p => p.Komisyon),
                    Yuzde = toplamPrim > 0 ? Math.Round(brutPrim / toplamPrim * 100, 1) : 0
                };
            })
            .OrderByDescending(x => x.ToplamBrutPrim)
            .Take(limit)
            .ToList();

        return new SirketDagilimResponse
        {
            Dagilim = dagilim,
            ToplamPrim = toplamPrim,
            ToplamPolice = toplamPolice
        };
    }
}
