using System;

namespace ERPSystem.Modules.Inventory.Entities;

public enum StockMovementType
{
    Receive = 1,   // استلام من مورد (+)
    Issue = 2,     // صرف (-)
    Transfer = 3,  // نقل بين مخازن (0 net effect)
    Adjust = 4,    // تسوية جرد (+/-)
    Return = 5     // إرجاع من عميل (+)
}

public enum StockMovementStatus
{
    Draft = 1,
    Posted = 2,
    Reversed = 3
}

/// <summary>
/// StockMovement = Aggregate Root
/// - Insert as Draft
/// - PostAsync: status → Posted + UPSERT stock_levels + publish event (future)
/// - ReverseAsync: creates opposite movement
/// </summary>
public class StockMovement
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public string Reference { get; set; } = string.Empty;   // "STOCK-RECV-2026-001"
    public StockMovementType Type { get; set; }
    public DateTime MovementDate { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Quantity { get; set; }                   // signed (+/-)
    public decimal UnitCost { get; set; }
    public decimal TotalCost => Quantity * UnitCost;

    public Guid? ProjectId { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? DestinationWarehouseId { get; set; }      // for Transfer only

    public string? SourceType { get; set; }                // "PurchaseOrder", "ProjectMaterial"
    public Guid? SourceId { get; set; }
    public string? Notes { get; set; }

    public StockMovementStatus Status { get; set; } = StockMovementStatus.Draft;

    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? PostedAt { get; set; }
    public Guid? ReversedByMovementId { get; set; }         // for reversal link
}
