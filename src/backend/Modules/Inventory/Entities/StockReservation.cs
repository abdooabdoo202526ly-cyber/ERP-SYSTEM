using System;

namespace ERPSystem.Modules.Inventory.Entities;

/// <summary>
/// StockReservation = holds quantity for projects/orders
/// Prevents over-issue by reducing QuantityAvailable (not OnHand)
/// </summary>
public class StockReservation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public string ReferenceType { get; set; } = string.Empty;  // "Project", "SalesOrder"
    public Guid ReferenceId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
}
