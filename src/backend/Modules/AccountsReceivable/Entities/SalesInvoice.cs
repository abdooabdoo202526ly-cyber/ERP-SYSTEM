using System;
using System.Collections.Generic;

namespace ERPSystem.Modules.AccountsReceivable.Entities;

/// <summary>
/// حالات فاتورة المبيعات (forward-only workflow مع بعض المرونة).
/// نخزّنها كـ string في الـ DB (مثل Procurement) ليبقى الـ schema مرناً.
/// </summary>
public enum SalesInvoiceStatus
{
    Draft = 1,           // قابل للتعديل
    Sent = 2,            // مُرسل للعميل
    PartiallyPaid = 3,   // مدفوع جزئياً
    Paid = 4,            // مدفوع بالكامل
    Overdue = 5,         // متأخر السداد
    Cancelled = 6        // ملغى
}

/// <summary>
/// طرق الدفع لسند القبض.
/// نخزّنها كـ string ليبقى الـ schema مرناً.
/// </summary>
public static class PaymentMethod
{
    public const string Cash = "Cash";
    public const string Bank = "Bank";
    public const string Transfer = "Transfer";
    public const string Check = "Check";
    public static readonly string[] All = { Cash, Bank, Transfer, Check };
}

/// <summary>
/// فاتورة مبيعات (Sales Invoice) — رأس الـ aggregate.
/// Lines: بنود الفاتورة (Description, Qty, Price, Tax, LineTotal).
/// Business Rule: Post → JournalEntry (Dr 1230 AR / Cr 5110 Revenue).
/// </summary>
public class SalesInvoice
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid CustomerId { get; set; }

    /// <summary>رقم تسلسلي للفاتورة (يُولّد تلقائياً، فريد داخل الـ tenant). مثال: "SI-2026-0001".</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }

    public string CurrencyCode { get; set; } = "LYD";
    public decimal ExchangeRate { get; set; } = 1m;

    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }

    /// <summary>المتبقي — يُحسب في الـ repository: TotalAmount - PaidAmount.</summary>
    public decimal Outstanding { get; set; }

    public SalesInvoiceStatus Status { get; set; } = SalesInvoiceStatus.Draft;

    public string? Notes { get; set; }

    /// <summary>اختياري — ربط بمشروع (Project module). المستقبل (Phase 5.B).</summary>
    public Guid? ProjectId { get; set; }

    public DateTime? PostedAt { get; set; }
    public Guid? PostedBy { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>معرّف القيد المحاسبي (عند Post).</summary>
    public Guid? JournalEntryId { get; set; }

    /// <summary>بنود الفاتورة (لا تُحفظ في جدول الـ invoice نفسه، تُجلب عند الحاجة).</summary>
    public List<SalesInvoiceLine> Lines { get; set; } = new();

    /// <summary>تخصيصات القبض على هذه الفاتورة.</summary>
    public List<ReceiptAllocation> Allocations { get; set; } = new();
}

/// <summary>
/// بند في فاتورة المبيعات — وصف + كمية + سعر + ضريبة.
/// نستخدم itemId كـ optional (للمستقبل عندما يربط المخزون بالمبيعات).
/// </summary>
public class SalesInvoiceLine
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SalesInvoiceId { get; set; }

    /// <summary>اختياري — ربط بصنف من المخزون (المستقبل).</summary>
    public Guid? ItemId { get; set; }

    public string Description { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }       // 0.15 = 15%
    public decimal LineTotal { get; set; }      // = Quantity * UnitPrice (قبل الضريبة)
}
