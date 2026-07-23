using System;

namespace ERPSystem.Shared.Events;

/// <summary>
/// Base contract for integration events published via IEventBus.
/// - EventId: idempotency key (consumer dedupes via processed_events)
/// - TenantId: routes event to the right tenant's data
/// - OccurredAt: for ordering / audit
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    Guid TenantId { get; }
    DateTime OccurredAt { get; }
}

/// <summary>Stock received (purchase, adjustment +, return) — Finance should Dr Inventory / Cr A/P</summary>
public record StockReceivedEvent(
    Guid EventId, Guid TenantId, Guid StockMovementId,
    Guid ItemId, Guid WarehouseId, decimal Quantity, decimal UnitCost,
    string? PurchaseOrderRef, DateTime OccurredAt) : IIntegrationEvent;

/// <summary>Stock issued (sale, project, issue) — Finance should Dr COGS / Cr Inventory</summary>
public record StockIssuedEvent(
    Guid EventId, Guid TenantId, Guid StockMovementId,
    Guid ItemId, Guid WarehouseId, decimal Quantity,
    string? ReferenceType, Guid? ReferenceId, DateTime OccurredAt) : IIntegrationEvent;

/// <summary>Journal entry posted — for future cross-module notifications (PR #8 Reports)</summary>
public record JournalEntryPostedEvent(
    Guid EventId, Guid TenantId, Guid JournalEntryId,
    string Reference, DateTime OccurredAt) : IIntegrationEvent;

/// <summary>Stock transferred between two warehouses (inter-warehouse, no P&L effect) — for audit / multi-warehouse analytics</summary>
public record StockTransferredEvent(
    Guid EventId, Guid TenantId, Guid StockMovementId,
    Guid ItemId, Guid FromWarehouseId, Guid ToWarehouseId,
    decimal Quantity, decimal UnitCost, DateTime OccurredAt) : IIntegrationEvent;

/// <summary>Stock adjusted (manual correction) — for variance analytics</summary>
public record StockAdjustedEvent(
    Guid EventId, Guid TenantId, Guid StockMovementId,
    Guid ItemId, Guid WarehouseId, decimal QuantityDelta,
    decimal UnitCost, string? Reason, DateTime OccurredAt) : IIntegrationEvent;
