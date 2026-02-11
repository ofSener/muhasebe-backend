using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Musteriler.Queries;

/// <summary>
/// TC/VKN/isim/plaka sinyallerine göre aday müşterileri döndürür.
/// Frontend eşleştirme panelini besler.
/// </summary>
public record FindCustomerCandidatesQuery : IRequest<FindCustomerCandidatesResult>
{
    public string? TcKimlikNo { get; init; }
    public string? VergiNo { get; init; }
    public string? Name { get; init; }
    public string? Plaka { get; init; }
    public int? Limit { get; init; }
}

public record FindCustomerCandidatesResult
{
    public List<CustomerCandidateDto> Candidates { get; init; } = new();
}

public record CustomerCandidateDto
{
    public int Id { get; init; }
    public string? Adi { get; init; }
    public string? Soyadi { get; init; }
    public string? TcKimlikNo { get; init; }
    public string? VergiNo { get; init; }
    public string? Gsm { get; init; }
    public string? Email { get; init; }
    public int PolicyCount { get; init; }
    public string MatchSignal { get; init; } = string.Empty;
    public string Confidence { get; init; } = string.Empty;
}

public class FindCustomerCandidatesQueryHandler : IRequestHandler<FindCustomerCandidatesQuery, FindCustomerCandidatesResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICustomerMatchingService _customerMatchingService;

    public FindCustomerCandidatesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICustomerMatchingService customerMatchingService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _customerMatchingService = customerMatchingService;
    }

    public async Task<FindCustomerCandidatesResult> Handle(FindCustomerCandidatesQuery request, CancellationToken cancellationToken)
    {
        var firmaId = _currentUserService.FirmaId ?? 0;
        var limit = request.Limit ?? 10;
        var candidates = new List<CustomerCandidateDto>();

        // TC ile tam eşleşme
        if (!string.IsNullOrWhiteSpace(request.TcKimlikNo))
        {
            var tcMatch = await _context.Musteriler
                .Where(m => m.TcKimlikNo == request.TcKimlikNo && m.EkleyenFirmaId == firmaId)
                .Select(m => new { m.Id, m.Adi, m.Soyadi, m.TcKimlikNo, m.VergiNo, m.Gsm, m.Email })
                .FirstOrDefaultAsync(cancellationToken);

            if (tcMatch != null)
            {
                var policyCount = await _context.Policeler
                    .CountAsync(p => p.MusteriId == tcMatch.Id, cancellationToken);

                candidates.Add(new CustomerCandidateDto
                {
                    Id = tcMatch.Id,
                    Adi = tcMatch.Adi,
                    Soyadi = tcMatch.Soyadi,
                    TcKimlikNo = tcMatch.TcKimlikNo,
                    VergiNo = tcMatch.VergiNo,
                    Gsm = tcMatch.Gsm,
                    Email = tcMatch.Email,
                    PolicyCount = policyCount,
                    MatchSignal = "TcKimlikNo",
                    Confidence = "Exact"
                });
            }
        }

        // VKN ile tam eşleşme
        if (!string.IsNullOrWhiteSpace(request.VergiNo))
        {
            var vknMatch = await _context.Musteriler
                .Where(m => m.VergiNo == request.VergiNo && m.EkleyenFirmaId == firmaId)
                .Select(m => new { m.Id, m.Adi, m.Soyadi, m.TcKimlikNo, m.VergiNo, m.Gsm, m.Email })
                .FirstOrDefaultAsync(cancellationToken);

            if (vknMatch != null && candidates.All(c => c.Id != vknMatch.Id))
            {
                var policyCount = await _context.Policeler
                    .CountAsync(p => p.MusteriId == vknMatch.Id, cancellationToken);

                candidates.Add(new CustomerCandidateDto
                {
                    Id = vknMatch.Id,
                    Adi = vknMatch.Adi,
                    Soyadi = vknMatch.Soyadi,
                    TcKimlikNo = vknMatch.TcKimlikNo,
                    VergiNo = vknMatch.VergiNo,
                    Gsm = vknMatch.Gsm,
                    Email = vknMatch.Email,
                    PolicyCount = policyCount,
                    MatchSignal = "VergiNo",
                    Confidence = "Exact"
                });
            }
        }

        // İsim ile eşleşme
        if (!string.IsNullOrWhiteSpace(request.Name) && candidates.Count < limit)
        {
            var searchName = request.Name.Trim();
            var nameMatches = await _context.Musteriler
                .Where(m =>
                    m.EkleyenFirmaId == firmaId &&
                    m.Adi != null &&
                    (m.Adi.Contains(searchName) || (m.Soyadi != null && m.Soyadi.Contains(searchName))))
                .Take(limit)
                .Select(m => new { m.Id, m.Adi, m.Soyadi, m.TcKimlikNo, m.VergiNo, m.Gsm, m.Email })
                .ToListAsync(cancellationToken);

            foreach (var m in nameMatches)
            {
                if (candidates.All(c => c.Id != m.Id))
                {
                    var policyCount = await _context.Policeler
                        .CountAsync(p => p.MusteriId == m.Id, cancellationToken);

                    candidates.Add(new CustomerCandidateDto
                    {
                        Id = m.Id,
                        Adi = m.Adi,
                        Soyadi = m.Soyadi,
                        TcKimlikNo = m.TcKimlikNo,
                        VergiNo = m.VergiNo,
                        Gsm = m.Gsm,
                        Email = m.Email,
                        PolicyCount = policyCount,
                        MatchSignal = "Name",
                        Confidence = "Medium"
                    });
                }
            }
        }

        return new FindCustomerCandidatesResult
        {
            Candidates = candidates.Take(limit).ToList()
        };
    }
}
