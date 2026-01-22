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
    DateTime? StartDate = null,
    DateTime? EndDate = null
) : IRequest<YakalananPoliceStatsDto>;

public class GetYakalananPoliceStatsQueryHandler : IRequestHandler<GetYakalananPoliceStatsQuery, YakalananPoliceStatsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetYakalananPoliceStatsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<YakalananPoliceStatsDto> Handle(GetYakalananPoliceStatsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.YakalananPoliceler.AsQueryable();

        // Firma bazlı temel filtre
        if (_currentUserService.FirmaId.HasValue)
        {
            query = query.Where(x => x.FirmaId == _currentUserService.FirmaId.Value);
        }

        // GorebilecegiPoliceler yetkisine göre filtrele
        var gorebilecegiPoliceler = _currentUserService.GorebilecegiPoliceler ?? "3";
        var userId = int.TryParse(_currentUserService.UserId, out var uid) ? uid : 0;

        query = gorebilecegiPoliceler switch
        {
            "1" => query, // Tüm firma poliçeleri
            "2" => _currentUserService.SubeId.HasValue
                ? query.Where(x => x.SubeId == _currentUserService.SubeId.Value)
                : query,
            "3" => query.Where(x => x.UyeId == userId), // Sadece kendi poliçeleri
            "4" => query.Where(x => false), // Hiçbir poliçeyi göremez
            _ => query.Where(x => x.UyeId == userId) // Default - sadece kendi poliçeleri
        };

        // Tarih filtreleme (TanzimTarihi'ne göre)
        if (request.StartDate.HasValue)
        {
            var startDate = request.StartDate.Value.Date;
            query = query.Where(x => x.TanzimTarihi >= startDate);
        }

        if (request.EndDate.HasValue)
        {
            var endDate = request.EndDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(x => x.TanzimTarihi <= endDate);
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
