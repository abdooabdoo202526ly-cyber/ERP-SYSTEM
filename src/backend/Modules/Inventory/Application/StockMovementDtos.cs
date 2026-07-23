using System;
using ERPSystem.Modules.Inventory.Entities;

namespace ERPSystem.Modules.Inventory.Application;

public sealed class ReceiveStockRequest
{
    public Guid CompanyId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime MovementDate { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? CostCenterId { get; set; }
    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public string? Notes { get; set; }
}

public sealed class IssueStockRequest
{
    public Guid CompanyId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime MovementDate { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Quantity { get; set; }      // موجب فقط (سيُحوّل لـ - في service)
    public Guid? ProjectId { get; set; }
    public Guid? CostCenterId { get; set; }
    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public string? Notes { get; set; }
}

public sealed class TransferStockRequest
{
    public Guid CompanyId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime MovementDate { get; set; }
    public Guid ItemId { get; set; }
    public Guid SourceWarehouseId { get; set; }
    public Guid DestinationWarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? CostCenterId { get; set; }
    public string? Notes { get; set; }
}

public sealed class AdjustStockRequest
{
    public Guid CompanyId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime MovementDate { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Quantity { get; set; }     // signed (+ or -)
    public decimal UnitCost { get; set; }
    public string? Notes { get; set; }
}

public sealed class StockMovementResponse
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = string.Empty;
    public StockMovementType Type { get; set; }
    public StockMovementStatus Status { get; set; }
    public DateTime MovementDate { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? DestinationWarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? CostCenterId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PostedAt { get; set; }
}

public sealed class StockLevelResponse
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal QuantityReserved { get; set; }
    public decimal QuantityAvailable { get; set; }
    public decimal AverageCost { get; set; }
    public DateTime LastMovementAt { get; set; }
    public int Version { get; set; }
}

public sealed class CreateReservationRequest
{
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public Guid ReferenceId { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public sealed class ReservationResponse
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public Guid ReferenceId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
