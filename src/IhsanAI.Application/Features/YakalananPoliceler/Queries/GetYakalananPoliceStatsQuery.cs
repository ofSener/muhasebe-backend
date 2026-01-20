using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.YakalananPoliceler.Queries;

public record YakalananPoliceStatsDto
{
    public int PendingCount { get; init; }
    public decimal TotalGrossPremium { get; init; }
    public int ActiveProducerCount { get; init; }
    public int SentCount { get; init; }
    public List<ActiveProducerDto> ActiveProducers { get; init; } = new();
}

public record ActiveProducerDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int PolicyCount { get; init; }
    public decimal TotalPremium { get; init; }
}

public record GetYakalananPoliceStatsQuery(
    int? FirmaId = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null
) : IRequest<YakalananPoliceStatsDto>;

public class GetYakalananPoliceStatsQueryHandler : IRequestHandler<GetYakalananPoliceStatsQuery, YakalananPoliceStatsDto>
{
    private readonly IApplicationDbContext _context;

    public GetYakalananPoliceStatsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<YakalananPoliceStatsDto> Handle(GetYakalananPoliceStatsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.YakalananPoliceler.AsQueryable();

        if (request.FirmaId.HasValue)
        {
            query = query.Where(x => x.FirmaId == request.FirmaId.Value);
        }

        if (request.StartDate.HasValue)
        {
            query = query.Where(x => x.EklenmeTarihi >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(x => x.EklenmeTarihi <= request.EndDate.Value);
        }

        var policeler = await query.AsNoTracking().ToListAsync(cancellationToken);

        var pendingCount = policeler.Count;
        var totalGrossPremium = (decimal)policeler.Sum(x => x.BrutPrim);

        var producerGroups = policeler
            .GroupBy(x => x.ProduktorId)
            .Select(g => new
            {
                ProduktorId = g.Key,
                PolicyCount = g.Count(),
                TotalPremium = (decimal)g.Sum(x => x.BrutPrim)
            })
            .ToList();

        var activeProducerCount = producerGroups.Count;

        var producerIds = producerGroups.Select(x => x.ProduktorId).ToList();
        var producers = await _context.Kullanicilar
            .Where(k => producerIds.Contains(k.Id))
            .Select(k => new { k.Id, k.Adi, k.Soyadi })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var activeProducers = producerGroups
            .Select(g =>
            {
                var producer = producers.FirstOrDefault(p => p.Id == g.ProduktorId);
                return new ActiveProducerDto
                {
                    Id = g.ProduktorId,
                    Name = producer != null ? $"{producer.Adi} {producer.Soyadi}".Trim() : $"Üretici #{g.ProduktorId}",
                    PolicyCount = g.PolicyCount,
                    TotalPremium = g.TotalPremium
                };
            })
            .OrderByDescending(x => x.TotalPremium)
            .Take(10)
            .ToList();

        return new YakalananPoliceStatsDto
        {
            PendingCount = pendingCount,
            TotalGrossPremium = totalGrossPremium,
            ActiveProducerCount = activeProducerCount,
            SentCount = 0,
            ActiveProducers = activeProducers
        };
    }
}
