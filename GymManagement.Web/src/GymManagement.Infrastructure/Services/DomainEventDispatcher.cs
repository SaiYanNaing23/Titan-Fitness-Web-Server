using GymManagement.Application.Common.Interfaces;
using GymManagement.Domain.Common;
using GymManagement.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GymManagement.Infrastructure.Services;

public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(IServiceProvider serviceProvider, ILogger<DomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchEventsAsync(IEnumerable<BaseEntity> entitiesWithEvents, CancellationToken cancellationToken = default)
    {
        var entities = entitiesWithEvents.ToList();
        var domainEvents = entities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        foreach (var entity in entities)
        {
            entity.ClearDomainEvents();
        }

        foreach (var domainEvent in domainEvents)
        {
            _logger.LogInformation("Dispatching domain event: {EventName} occurred at {OccurredOn}", 
                domainEvent.GetType().Name, domainEvent.OccurredOn);

            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handlers = _serviceProvider.GetServices(handlerType);

            foreach (var handler in handlers)
            {
                if (handler != null)
                {
                    var method = handlerType.GetMethod("HandleAsync");
                    if (method != null)
                    {
                        var task = (Task?)method.Invoke(handler, new object[] { domainEvent, cancellationToken });
                        if (task != null)
                        {
                            await task;
                        }
                    }
                }
            }
        }
    }
}

public class LowStockAlertEventHandler : IDomainEventHandler<LowStockAlertEvent>
{
    private readonly ILogger<LowStockAlertEventHandler> _logger;

    public LowStockAlertEventHandler(ILogger<LowStockAlertEventHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(LowStockAlertEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("LOW STOCK ALERT: Product '{ProductName}' (ID: {ProductId}) at Branch #{BranchId} is at {CurrentStock} units (Threshold: {Threshold}).",
            domainEvent.ProductName, domainEvent.ProductId, domainEvent.BranchId, domainEvent.CurrentStock, domainEvent.AlertThreshold);
        return Task.CompletedTask;
    }
}
