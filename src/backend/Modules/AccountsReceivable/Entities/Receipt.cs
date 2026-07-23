using System;
using System.Collections.Generic;

namespace ERPSystem.Modules.AccountsReceivable.Entities;

/// <summary>
/// سند قبض (Receipt) — يُستخدم لتسجيل دفعات العملاء.
/// كل سند قبض يخصّ عميلاً واحداً، ويمكن تخصيصه لعدة فواتير (عبر ReceiptAllocation).
/// عند Post → JournalEntry (Dr 1210 Cash / Cr 1230 AR).
/// </summary>
public class Receipt
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid CustomerId { get; set; }

    /// <summary>رقم تسلسلي للسند (يُولّد تلقائياً، فريد داخل الـ tenant). مثال: "RC-2026-0001".</summary>
    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime ReceiptDate { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "LYD";

    /// <summary>طريقة الدفع: Cash | Bank | Transfer | Check.</summary>
    public string? PaymentMethod { get; set; }

    public string? Notes { get; set; }

    public DateTime? PostedAt { get; set; }
    public Guid? PostedBy { get; set; }

    /// <summary>معرّف القيد المحاسبي (عند Post).</summary>
    public Guid? JournalEntryId { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>تخصيصات السند على الفواتير.</summary>
    public List<ReceiptAllocation> Allocations { get; set; } = new();
}

/// <summary>
/// تخصيص سند قبض على فاتورة — يدعم التخصيص الجزئي والمتعدد.
/// مجموع amountApplied على فواتير سند واحد يجب أن يساوي Receipt.Amount (للحالة المثالية).
/// </summary>
public class ReceiptAllocation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ReceiptId { get; set; }
    public Guid SalesInvoiceId { get; set; }
    public decimal AmountApplied { get; set; }
}
