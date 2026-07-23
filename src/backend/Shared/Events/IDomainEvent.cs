namespace ERPSystem.Shared.Events;

/// <summary>
/// In-process domain event contract (Sprint-4.5 T-010 / DEC-057).
///
/// الفرق عن <see cref="IIntegrationEvent"/>:
/// - IIntegrationEvent: outbox + cross-process (heavyweight, async, retried)
/// - IDomainEvent:       in-process only (lightweight, sync, fire-and-forget)
///
/// استخدم IIntegrationEvent للتكامل عبر الخدمات أو cross-process.
/// استخدم IDomainEvent للتنبيهات الخفيفة داخل نفس الـ process.
/// </summary>
public interface IDomainEvent
{
    /// <summary>الـ Tenant للـ event (للـ routing / RLS)</summary>
    Guid TenantId { get; }

    /// <summary>وقت حدوث الـ event</summary>
    DateTime OccurredAt { get; }
}