using MediatR;

namespace IhsanAI.Application.Features.Policeler.Commands;

/// <summary>
/// Birden fazla poliçeye toplu TC/VKN ataması yapar.
/// </summary>
public record BatchAssignTcCommand : IRequest<BatchAssignTcResult>
{
    public List<AssignTcItem> Items { get; init; } = new();
}

public record AssignTcItem
{
    public int PolicyId { get; init; }
    public string? TcKimlikNo { get; init; }
    public string? VergiNo { get; init; }
}

public record BatchAssignTcResult
{
    public int SuccessCount { get; init; }
    public int FailedCount { get; init; }
    public int TotalCascadeUpdated { get; init; }
    public List<string> Errors { get; init; } = new();
}

public class BatchAssignTcCommandHandler : IRequestHandler<BatchAssignTcCommand, BatchAssignTcResult>
{
    private readonly IMediator _mediator;

    public BatchAssignTcCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<BatchAssignTcResult> Handle(BatchAssignTcCommand request, CancellationToken cancellationToken)
    {
        var successCount = 0;
        var failedCount = 0;
        var totalCascade = 0;
        var errors = new List<string>();

        foreach (var item in request.Items)
        {
            var result = await _mediator.Send(new AssignTcToPolicyCommand
            {
                PolicyId = item.PolicyId,
                TcKimlikNo = item.TcKimlikNo,
                VergiNo = item.VergiNo
            }, cancellationToken);

            if (result.Success)
            {
                successCount++;
                totalCascade += result.CascadeUpdated;
            }
            else
            {
                failedCount++;
                errors.Add($"ID {item.PolicyId}: {result.ErrorMessage}");
            }
        }

        return new BatchAssignTcResult
        {
            SuccessCount = successCount,
            FailedCount = failedCount,
            TotalCascadeUpdated = totalCascade,
            Errors = errors
        };
    }
}
