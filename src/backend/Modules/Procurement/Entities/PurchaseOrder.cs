using System;
using System.Collections.Generic;

namespace ERPSystem.Modules.Procurement.Entities;

/// <summary>حالات أمر الشراء (forward-only workflow مع بعض المرونة).</summary>
public enum PurchaseOrderStatus
{
    Draft = 1,        // قابل للتعديل
    Pending = 2,      // بانتظار الموافقة
    Approved = 3,     // تمت الموافقة — جاهز للإرسال
    Sent = 4,         // أُرسل للمورّد
    Received = 5,     // تم استلام البضاعة (GR Received) — يقفل الـ PO
    Cancelled = 6     // مُلغى
}

/// <summary>
/// أمر الشراء (PO) — رأس الـ aggregate.
/// Lines: بنود الـ PO (Item, Qty, Price, Tax, SubTotal).
/// Business Rule: GR يُنشأ فقط لو PO في Approved أو Sent.
/// </summary>
public class PurchaseOrder
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>رقم تسلسلي للـ PO (يُولّد تلقائياً، فريد داخل الـ tenant). مثال: "PO-2026-0001".</summary>
    public string PoNumber { get; set; } = string.Empty;

    public Guid VendorId { get; set; }
    /// <summary>v1.0.34: رابط اختياري بالمشروع — عند الإضافة، يتم اشتقاق مركز التكلفة منه.</summary>
    public Guid? ProjectId { get; set; }
    /// <summary>v1.0.34: مركز التكلفة — مطلوب (ولو من المشروع).</summary>
    public Guid? CostCenterId { get; set; }
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDate { get; set; }

    public string Currency { get; set; } = "LYD";
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }

    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? SentAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>بنود الـ PO (لا تُحفظ في جدول الـ PO نفسه، تُجلب عند الحاجة).</summary>
    public List<PurchaseOrderLine> Lines { get; set; } = new();
}

/// <summary>بند في أمر الشراء — صنف + كمية + سعر + ضريبة.</summary>
public class PurchaseOrderLine
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }       // 0.15 = 15%
    public decimal SubTotal { get; set; }      // = Quantity * UnitPrice (قبل الضريبة)
    public int LineOrder { get; set; }         // للترتيب في الـ UI
}
