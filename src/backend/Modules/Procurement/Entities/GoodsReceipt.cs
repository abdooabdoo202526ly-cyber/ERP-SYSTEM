using System;
using System.Collections.Generic;

namespace ERPSystem.Modules.Procurement.Entities;

/// <summary>حالات سند الاستلام (GR).</summary>
public enum GoodsReceiptStatus
{
    Draft = 1,        // مُدخل لكن غير مُرحّل
    Received = 2,     // تم استلام البضاعة فعلياً — تم تحديث الـ StockLevel
    Cancelled = 3
}

/// <summary>
/// سند استلام البضاعة (GR) — يُنشأ على PO في حالة Approved أو Sent.
/// عند Post (Received) → يُنشأ StockMovement تلقائياً (نوع Receive).
/// Business Rule: Vendor Bill يُنشأ فقط لـ GR في حالة Received.
/// </summary>
public class GoodsReceipt
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>رقم تسلسلي لسند الاستلام. مثال: "GR-2026-0001".</summary>
    public string GrNumber { get; set; } = string.Empty;

    public Guid PurchaseOrderId { get; set; }
    public GoodsReceiptStatus Status { get; set; } = GoodsReceiptStatus.Draft;

    public DateTime ReceivedDate { get; set; }
    public Guid WarehouseId { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public List<GoodsReceiptLine> Lines { get; set; } = new();
}

/// <summary>بند سند استلام — صنف + كمية + تكلفة وحدة (snapshot).</summary>
public class GoodsReceiptLine
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid GoodsReceiptId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }      // snapshot من PO
    public string? Notes { get; set; }
    public int LineOrder { get; set; }
}
