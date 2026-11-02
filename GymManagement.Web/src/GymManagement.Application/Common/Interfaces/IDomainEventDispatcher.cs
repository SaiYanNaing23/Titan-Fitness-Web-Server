using GymManagement.Domain.Common;

namespace GymManagement.Application.Common.Interfaces;

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}

public interface IDomainEventDispatcher
{
    Task DispatchEventsAsync(IEnumerable<BaseEntity> entitiesWithEvents, CancellationToken cancellationToken = default);
}
