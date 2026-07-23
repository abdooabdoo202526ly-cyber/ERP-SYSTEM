using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Shared.Events;

/// <summary>
/// Default in-process publisher (Sprint-4.5 T-010 / DEC-057).
/// Uses IServiceProvider to resolve all IDomainEventHandler&lt;T&gt; implementations.
/// Handler failures are caught + logged — does not throw.
/// </summary>
public sealed class DomainEventPublisher : IDomainEventPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainEventPublisher> _logger;

    public DomainEventPublisher(IServiceProvider serviceProvider, ILogger<DomainEventPublisher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        if (@event == null)
        {
            _logger.LogWarning("DomainEventPublisher: skipped null event");
            return;
        }

        var eventTypeName = typeof(TEvent).Name;
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(typeof(TEvent));

        using var scope = _serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices(handlerType);

        var invoked = 0;
        foreach (var handler in handlers)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync));
                if (handleMethod == null) continue;

                var result = handleMethod.Invoke(handler, new object?[] { @event, ct });
                if (result is Task task)
                {
                    await task;
                }
                invoked++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "DomainEventPublisher: handler {Handler} failed for {Event}",
                    handler?.GetType().FullName ?? "?", eventTypeName);
            }
        }

        _logger.LogDebug("DomainEventPublisher: published {Event} to {Count} handler(s)", eventTypeName, invoked);
    }
}