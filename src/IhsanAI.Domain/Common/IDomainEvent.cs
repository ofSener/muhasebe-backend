using MediatR;

namespace IhsanAI.Domain.Common;

public interface IDomainEvent : INotification
{
    DateTime OccurredOn { get; }
}
