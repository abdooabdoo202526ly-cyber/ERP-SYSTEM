namespace ERPSystem.Shared.Events;

/// <summary>
/// Handler for an in-process domain event (Sprint-4.5 T-010 / DEC-057).
///
/// Implement this interface for each event type you want to react to.
/// Handlers are resolved from DI (Transient or Scoped) and called synchronously.
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}