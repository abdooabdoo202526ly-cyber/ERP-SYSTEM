using System;

namespace ERPSystem.Modules.Inventory.Entities;

/// <summary>
/// StockLevel = CQRS Read Model (denormalized)
/// Computed on StockMovement.PostAsync
/// - For Receive: quantity_on_hand += qty; average_cost = weighted moving avg
/// - For Issue/Transfer: quantity_on_hand += qty (qty is signed)
/// - For Transfer out: 2 StockLevel updates (source decreases, dest increases)
/// - Version = optimistic concurrency (updated on every change)
/// </summary>
public class StockLevel
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }

    public decimal QuantityOnHand { get; set; }
    public decimal QuantityReserved { get; set; }       // للـ project/orders
    public decimal QuantityAvailable => QuantityOnHand - QuantityReserved;

    public decimal AverageCost { get; set; }            // moving weighted average
    public DateTime LastMovementAt { get; set; }
    public int Version { get; set; }                     // optimistic concurrency
}
