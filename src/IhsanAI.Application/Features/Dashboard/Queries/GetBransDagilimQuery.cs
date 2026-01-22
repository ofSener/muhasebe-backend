using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Dashboard.Queries;

// Response DTO
public record BransDagilimItem
{
    public int BransId { get; init; }
    public string BransAdi { get; init; } = string.Empty;
    public int PoliceSayisi { get; init; }
    public decimal ToplamBrutPrim { get; init; }
    public decimal ToplamKomisyon { get; init; }
    public decimal Yuzde { get; init; }
}

public record BransDagilimResponse
{
    public List<BransDagilimItem> Dagilim { get; init; } = new();
    public decimal ToplamPrim { get; init; }
    public int ToplamPolice { get; init; }
}

// Query
public record GetBransDagilimQuery(
    int? FirmaId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null
) : IRequest<BransDagilimResponse>;

// Handler
public class GetBransDagilimQueryHandler : IRequestHandler<GetBransDagilimQuery, BransDagilimResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeService _dateTimeService;

    // Branş ID -> Branş Adı eşleştirmesi
    private static readonly Dictionary<int, string> BransAdlari = new()
    {
        { 1, "Trafik" },
        { 2, "Kasko" },
        { 3, "DASK" },
        { 4, "Konut" },
        { 5, "Sağlık" },
        { 6, "Ferdi Kaza" },
        { 7, "Seyahat" },
        { 8, "Nakliyat" },
        { 9, "İşyeri" },
        { 10, "Diğer" }
    };

    public GetBransDagilimQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeService = dateTimeService;
    }

    public async Task<BransDagilimResponse> Handle(GetBransDagilimQuery request, CancellationToken cancellationToken)
    {
        var firmaId = request.FirmaId ?? _currentUserService.FirmaId;
        var now = _dateTimeService.Now;
        var startDate = request.StartDate ?? new DateTime(now.Year, 1, 1); // Yıl başından itibaren
        var endDate = request.EndDate ?? now;

        var query = _context.Policeler.AsQueryable();

        if (firmaId.HasValue)
        {
            query = query.Where(p => p.IsOrtagiFirmaId == firmaId.Value);
        }

        query = query.Where(p => p.TanzimTarihi >= startDate && p.TanzimTarihi <= endDate);

        var policeler = await query.AsNoTracking().ToListAsync(cancellationToken);

        var toplamPrim = policeler.Sum(p => p.BrutPrim);
        var toplamPolice = policeler.Count;

        var dagilim = policeler
            .GroupBy(p => p.BransId)
            .Select(g => new BransDagilimItem
            {
                BransId = g.Key,
                BransAdi = BransAdlari.GetValueOrDefault(g.Key, $"Branş #{g.Key}"),
                PoliceSayisi = g.Count(),
                ToplamBrutPrim = g.Sum(p => p.BrutPrim),
                ToplamKomisyon = g.Sum(p => p.Komisyon),
                Yuzde = toplamPrim > 0 ? Math.Round(g.Sum(p => p.BrutPrim) / toplamPrim * 100, 1) : 0
            })
            .OrderByDescending(x => x.ToplamBrutPrim)
            .ToList();

        return new BransDagilimResponse
        {
            Dagilim = dagilim,
            ToplamPrim = toplamPrim,
            ToplamPolice = toplamPolice
        };
    }
}
