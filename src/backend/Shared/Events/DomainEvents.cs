namespace ERPSystem.Shared.Events;

/// <summary>
/// Domain event raised when a new Invoice is created (Sprint-4.5 T-010 / DEC-057).
/// In-process only — used by Projects module to update actual cost automatically.
/// (Named InvoiceCreatedDomainEvent to avoid clash with existing IIntegrationEvent types.)
/// </summary>
public record InvoiceCreatedDomainEvent(
    Guid TenantId,
    Guid InvoiceId,
    Guid? ProjectId,
    decimal Amount,
    DateTime OccurredAt) : IDomainEvent;

/// <summary>
/// Domain event raised when a Journal Entry is posted (Sprint-4.5 T-010 / DEC-057).
/// In-process only — used by Reports module for cache invalidation.
/// </summary>
public record JournalEntryPostedDomainEvent(
    Guid TenantId,
    Guid JournalEntryId,
    string Reference,
    decimal TotalDebit,
    decimal TotalCredit,
    DateTime OccurredAt) : IDomainEvent;

/// <summary>
/// Domain event raised when a Project's actual cost changes (Sprint-4.5 T-010 / DEC-057).
/// In-process only — used by Reports module to refresh project cost report.
/// </summary>
public record ProjectCostUpdatedDomainEvent(
    Guid TenantId,
    Guid ProjectId,
    decimal NewActualCost,
    DateTime OccurredAt) : IDomainEvent;