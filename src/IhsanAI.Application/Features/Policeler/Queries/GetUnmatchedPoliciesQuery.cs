using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Policeler.Queries;

/// <summary>
/// MusteriId'si null olan onaylı poliçeleri sayfalı döndürür.
/// </summary>
public record GetUnmatchedPoliciesQuery : IRequest<UnmatchedPoliciesResult>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int? SigortaSirketiId { get; init; }
    public string? Search { get; init; }
}

public record UnmatchedPoliciesResult
{
    public List<UnmatchedPolicyDto> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int TotalUnmatched { get; init; }
    public int ThisMonthUnmatched { get; init; }
    public double MatchPercentage { get; init; }
}

public record UnmatchedPolicyDto
{
    public int Id { get; init; }
    public string PoliceNumarasi { get; init; } = string.Empty;
    public string? SigortaliAdi { get; init; }
    public string? Plaka { get; init; }
    public string? TcKimlikNo { get; init; }
    public string? VergiNo { get; init; }
    public float BrutPrim { get; init; }
    public DateTime BaslangicTarihi { get; init; }
    public DateTime BitisTarihi { get; init; }
    public int SigortaSirketiId { get; init; }
    public string? SigortaSirketiAdi { get; init; }
    public int PoliceTuruId { get; init; }
    public string? PoliceTuruAdi { get; init; }
}

public class GetUnmatchedPoliciesQueryHandler : IRequestHandler<GetUnmatchedPoliciesQuery, UnmatchedPoliciesResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetUnmatchedPoliciesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<UnmatchedPoliciesResult> Handle(GetUnmatchedPoliciesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Policeler
            .Where(p => p.MusteriId == null && p.OnayDurumu == 1);

        // Firma filtresi
        if (_currentUserService.FirmaId.HasValue)
            query = query.Where(p => p.FirmaId == _currentUserService.FirmaId.Value);

        // Tarih filtresi
        if (request.StartDate.HasValue)
            query = query.Where(p => p.BaslangicTarihi >= request.StartDate.Value);
        if (request.EndDate.HasValue)
            query = query.Where(p => p.BaslangicTarihi <= request.EndDate.Value);

        // Sigorta şirketi filtresi
        if (request.SigortaSirketiId.HasValue)
            query = query.Where(p => p.SigortaSirketiId == request.SigortaSirketiId.Value);

        // Arama
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchTerm = request.Search.Trim();
            query = query.Where(p =>
                (p.SigortaliAdi != null && p.SigortaliAdi.Contains(searchTerm)) ||
                p.PoliceNumarasi.Contains(searchTerm) ||
                p.Plaka.Contains(searchTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Lookup tablolarını yükle
        var sirketDict = await _context.SigortaSirketleri
            .ToDictionaryAsync(x => x.Id, x => x.SirketAdi, cancellationToken);
        var turDict = await _context.PoliceTurleri
            .ToDictionaryAsync(x => x.Id, x => x.Turu, cancellationToken);

        var items = await query
            .OrderByDescending(p => p.EklenmeTarihi)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new UnmatchedPolicyDto
            {
                Id = p.Id,
                PoliceNumarasi = p.PoliceNumarasi,
                SigortaliAdi = p.SigortaliAdi,
                Plaka = p.Plaka,
                TcKimlikNo = p.TcKimlikNo,
                VergiNo = p.VergiNo,
                BrutPrim = p.BrutPrim,
                BaslangicTarihi = p.BaslangicTarihi,
                BitisTarihi = p.BitisTarihi,
                SigortaSirketiId = p.SigortaSirketiId,
                PoliceTuruId = p.PoliceTuruId
            })
            .ToListAsync(cancellationToken);

        // Lookup adlarını ata
        foreach (var item in items)
        {
            // record with yapısı kullanılamaz, DTO'yu class'a çevirelim
        }

        // İstatistikler
        var totalPolicies = await _context.Policeler
            .Where(p => p.OnayDurumu == 1)
            .Apply(q => _currentUserService.FirmaId.HasValue
                ? q.Where(p => p.FirmaId == _currentUserService.FirmaId.Value)
                : q)
            .CountAsync(cancellationToken);

        var totalUnmatched = await _context.Policeler
            .Where(p => p.MusteriId == null && p.OnayDurumu == 1)
            .Apply(q => _currentUserService.FirmaId.HasValue
                ? q.Where(p => p.FirmaId == _currentUserService.FirmaId.Value)
                : q)
            .CountAsync(cancellationToken);

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var thisMonthUnmatched = await _context.Policeler
            .Where(p => p.MusteriId == null && p.OnayDurumu == 1 && p.EklenmeTarihi >= monthStart)
            .Apply(q => _currentUserService.FirmaId.HasValue
                ? q.Where(p => p.FirmaId == _currentUserService.FirmaId.Value)
                : q)
            .CountAsync(cancellationToken);

        var matchPercentage = totalPolicies > 0
            ? ((double)(totalPolicies - totalUnmatched) / totalPolicies) * 100
            : 100;

        // Lookup adlarını set et (items'ı yeniden oluştur çünkü record)
        var enrichedItems = items.Select(i => i with
        {
            SigortaSirketiAdi = sirketDict.GetValueOrDefault(i.SigortaSirketiId),
            PoliceTuruAdi = turDict.GetValueOrDefault(i.PoliceTuruId)
        }).ToList();

        return new UnmatchedPoliciesResult
        {
            Items = enrichedItems,
            TotalCount = totalCount,
            TotalUnmatched = totalUnmatched,
            ThisMonthUnmatched = thisMonthUnmatched,
            MatchPercentage = Math.Round(matchPercentage, 1)
        };
    }
}

// Extension method for conditional query building
internal static class QueryableExtensions
{
    public static IQueryable<T> Apply<T>(this IQueryable<T> query, Func<IQueryable<T>, IQueryable<T>> transform)
        => transform(query);
}
