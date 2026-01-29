using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Features.Policeler.Commands;

namespace IhsanAI.Application.Features.YakalananPoliceler.Commands;

public record YakalananPoliceChangesDto
{
    public string? PolicyNo { get; init; }
    public int? Customer { get; init; }
    public int? TypeId { get; init; }
    public float? NetPremium { get; init; }
    public float? GrossPremium { get; init; }
    public int? ProducerId { get; init; }
    public int? BranchId { get; init; }
    public string? Date { get; init; }
}

public record YakalananPoliceUpdateDto
{
    public int PolicyId { get; init; }
    public YakalananPoliceChangesDto? Changes { get; init; }
}

public record BatchUpdateYakalananPolicelerCommand(
    List<YakalananPoliceUpdateDto> Updates
) : IRequest<BatchUpdateResultDto>;

public class BatchUpdateYakalananPolicelerCommandHandler
    : IRequestHandler<BatchUpdateYakalananPolicelerCommand, BatchUpdateResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public BatchUpdateYakalananPolicelerCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<BatchUpdateResultDto> Handle(
        BatchUpdateYakalananPolicelerCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Updates == null || request.Updates.Count == 0)
        {
            return new BatchUpdateResultDto
            {
                UpdatedCount = 0,
                FailedCount = 0,
                FailedIds = new List<int>()
            };
        }

        // Güncellenecek kayıtları çek
        var ids = request.Updates.Select(u => u.PolicyId).ToList();
        var policies = await _context.YakalananPoliceler
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);

        var updatedCount = 0;
        var failedIds = new List<int>();

        // Her güncellemeyi uygula
        foreach (var update in request.Updates)
        {
            var policy = policies.FirstOrDefault(p => p.Id == update.PolicyId);
            if (policy == null)
            {
                failedIds.Add(update.PolicyId);
                continue;
            }

            if (update.Changes == null)
            {
                continue;
            }

            var changes = update.Changes;

            // Producer ID
            if (changes.ProducerId.HasValue)
            {
                policy.ProduktorId = changes.ProducerId.Value;
            }

            // Branch ID
            if (changes.BranchId.HasValue)
            {
                policy.ProduktorSubeId = changes.BranchId.Value;
            }

            // Type ID
            if (changes.TypeId.HasValue)
            {
                policy.PoliceTuru = changes.TypeId.Value;
            }

            // NetPremium
            if (changes.NetPremium.HasValue)
            {
                policy.NetPrim = changes.NetPremium.Value;
            }

            // GrossPremium
            if (changes.GrossPremium.HasValue)
            {
                policy.BrutPrim = changes.GrossPremium.Value;
            }

            // Date (TanzimTarihi - frontend'den gelen tarih)
            if (!string.IsNullOrEmpty(changes.Date))
            {
                if (DateTime.TryParse(changes.Date, out var date))
                {
                    policy.TanzimTarihi = date;
                }
            }

            // PolicyNo (MaxLength: 25)
            if (!string.IsNullOrEmpty(changes.PolicyNo))
            {
                if (changes.PolicyNo.Length <= 25)
                {
                    policy.PoliceNumarasi = changes.PolicyNo;
                }
                // Uzun değerler sessizce atlanır, mevcut değer korunur
            }

            // Customer (MusteriId)
            if (changes.Customer.HasValue)
            {
                policy.MusteriId = changes.Customer.Value;
            }

            // Audit alanları
            if (int.TryParse(_currentUserService.UserId, out var userId))
            {
                policy.GuncelleyenUyeId = userId;
            }
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
