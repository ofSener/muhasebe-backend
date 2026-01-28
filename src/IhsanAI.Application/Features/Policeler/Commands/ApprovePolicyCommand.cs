using MediatR;
using Microsoft.EntityFrameworkCore;
using IhsanAI.Application.Common.Interfaces;
using IhsanAI.Application.Common.Extensions;

namespace IhsanAI.Application.Features.Policeler.Commands;

/// <summary>
/// Yakalanan poliçeyi onaylar (OnayDurumu = 1 yapar)
/// </summary>
public record ApprovePolicyCommand(int PolicyId) : IRequest<ApprovePolicyResult>;

public record ApprovePolicyResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

public class ApprovePolicyCommandHandler : IRequestHandler<ApprovePolicyCommand, ApprovePolicyResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ApprovePolicyCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ApprovePolicyResult> Handle(ApprovePolicyCommand request, CancellationToken cancellationToken)
    {
        var query = _context.Policeler.AsQueryable();

        // Firma filtresi uygula
        query = query.ApplyFirmaFilter(_currentUserService, x => x.IsOrtagiFirmaId);

        var policy = await query.FirstOrDefaultAsync(x => x.Id == request.PolicyId, cancellationToken);

        if (policy == null)
        {
            return new ApprovePolicyResult
            {
                Success = false,
                ErrorMessage = "Poliçe bulunamadı veya erişim yetkiniz yok"
            };
        }

        if (policy.OnayDurumu == 1)
        {
            return new ApprovePolicyResult
            {
                Success = false,
                ErrorMessage = "Bu poliçe zaten onaylanmış"
            };
        }

        policy.OnayDurumu = 1;
        policy.GuncellenmeTarihi = DateTime.UtcNow;
        policy.GuncelleyenKullaniciId = _currentUserService.UyeId ?? 0;

        await _context.SaveChangesAsync(cancellationToken);

        return new ApprovePolicyResult { Success = true };
    }
}

/// <summary>
/// Birden fazla poliçeyi toplu onaylama
/// </summary>
public record BatchApprovePoliciesCommand(List<int> PolicyIds) : IRequest<BatchApprovePoliciesResult>;

public record BatchApprovePoliciesResult
{
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public List<string> Errors { get; init; } = new();
}

public class BatchApprovePoliciesCommandHandler : IRequestHandler<BatchApprovePoliciesCommand, BatchApprovePoliciesResult>
{
    private readonly IMediator _mediator;

    public BatchApprovePoliciesCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<BatchApprovePoliciesResult> Handle(BatchApprovePoliciesCommand request, CancellationToken cancellationToken)
    {
        int successCount = 0;
        int failedCount = 0;
        var errors = new List<string>();

        foreach (var policyId in request.PolicyIds)
        {
            var result = await _mediator.Send(new ApprovePolicyCommand(policyId), cancellationToken);

            if (result.Success)
            {
                successCount++;
            }
            else
            {
                failedCount++;
                errors.Add($"ID {policyId}: {result.ErrorMessage}");
            }
        }

        return new BatchApprovePoliciesResult
        {
            SuccessCount = successCount,
            FailedCount = failedCount,
            Errors = errors
        };
    }
}
