using System;
using System.Collections.Generic;

namespace ERPSystem.Modules.Procurement.Entities;

/// <summary>حالات فاتورة المورّد (Vendor Bill).</summary>
public enum VendorBillStatus
{
    Draft = 1,        // مُدخلة لكن غير مُرحّلة
    Posted = 2,       // مُرحّلة — JournalEntry تم إنشاؤه
    Paid = 3,         // مدفوعة (مستقبلي — Phase 4)
    Cancelled = 4
}

/// <summary>
/// فاتورة المورّد (VB) — تُنشأ على GR في حالة Received.
/// عند Post → JournalEntry (Dr Inventory / Cr Accounts Payable).
/// Business Rule: Bill يُنشأ فقط لـ GR في حالة Received.
/// </summary>
public class VendorBill
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>رقم فاتورة المورّد (فريد داخل الـ tenant). مثال: "BILL-2026-0001".</summary>
    public string BillNumber { get; set; } = string.Empty;

    public Guid GoodsReceiptId { get; set; }
    public Guid VendorId { get; set; }
    public VendorBillStatus Status { get; set; } = VendorBillStatus.Draft;

    public DateTime BillDate { get; set; }
    public DateTime? DueDate { get; set; }

    public string Currency { get; set; } = "LYD";
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    /// <summary>معرّف القيد المحاسبي عند الترحيل (Dr Inventory / Cr A/P).</summary>
    public Guid? JournalEntryId { get; set; }
    public DateTime? PostedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public List<VendorBillLine> Lines { get; set; } = new();
}

/// <summary>بند فاتورة المورّد — صنف + كمية + سعر + ضريبة.</summary>
public class VendorBillLine
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid VendorId { get; set; }     // denormalized للـ queries السريعة
    public Guid VendorBillId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal SubTotal { get; set; }
    public int LineOrder { get; set; }
}
