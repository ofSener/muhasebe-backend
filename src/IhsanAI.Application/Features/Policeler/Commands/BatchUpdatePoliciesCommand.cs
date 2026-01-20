using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;

namespace IhsanAI.Application.Features.Policeler.Commands;

public record PolicyUpdateDto
{
    public int Id { get; init; }
    public int? MusteriId { get; init; }
    public int? IsOrtagiUyeId { get; init; }
    public int? IsOrtagiSubeId { get; init; }
    public string? Aciklama { get; init; }
}

public record BatchUpdatePoliciesCommand(List<PolicyUpdateDto> Policies) : IRequest<BatchUpdateResultDto>;

public record BatchUpdateResultDto
{
    public int UpdatedCount { get; init; }
    public int FailedCount { get; init; }
    public List<int> FailedIds { get; init; } = new();
}

public class BatchUpdatePoliciesCommandHandler : IRequestHandler<BatchUpdatePoliciesCommand, BatchUpdateResultDto>
{
    private readonly IApplicationDbContext _context;

    public BatchUpdatePoliciesCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BatchUpdateResultDto> Handle(BatchUpdatePoliciesCommand request, CancellationToken cancellationToken)
    {
        var ids = request.Policies.Select(p => p.Id).ToList();
        var policies = await _context.Policeler
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);

        var updatedCount = 0;
        var failedIds = new List<int>();

        foreach (var update in request.Policies)
        {
            var policy = policies.FirstOrDefault(p => p.Id == update.Id);
            if (policy == null)
            {
                failedIds.Add(update.Id);
                continue;
            }

            if (update.MusteriId.HasValue)
                policy.MusteriId = update.MusteriId;

            if (update.IsOrtagiUyeId.HasValue)
                policy.IsOrtagiUyeId = update.IsOrtagiUyeId.Value;

            if (update.IsOrtagiSubeId.HasValue)
                policy.IsOrtagiSubeId = update.IsOrtagiSubeId.Value;

            if (update.Aciklama != null)
                policy.Aciklama = update.Aciklama;

            policy.GuncellenmeTarihi = DateTime.UtcNow;
            updatedCount++;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new BatchUpdateResultDto
        {
            UpdatedCount = updatedCount,
            FailedCount = failedIds.Count,
            FailedIds = failedIds
        };
    }
}
