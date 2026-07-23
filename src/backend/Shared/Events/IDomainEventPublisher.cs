namespace ERPSystem.Shared.Events;

/// <summary>
/// In-process domain event publisher (Sprint-4.5 T-010 / DEC-057).
///
/// يستدعي handlers المسجّلة في نفس الـ process — لا DB، لا outbox، لا retry.
/// مناسب للحالات التي لا تحتاج persistence أو cross-process communication.
/// </summary>
public interface IDomainEventPublisher
{
    /// <summary>
    /// Publish a domain event to all registered handlers synchronously (in-process).
    /// Handler exceptions are caught + logged — the original caller is not affected.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : IDomainEvent;
}