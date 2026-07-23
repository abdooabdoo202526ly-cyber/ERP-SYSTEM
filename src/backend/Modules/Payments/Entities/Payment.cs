using System;

namespace ERPSystem.Modules.Payments.Entities;

/// <summary>
/// نوع الطرف المدفوع لـ/منه.
/// - Customer: دفعة من/إلى عميل (AR — مستقبلي، الـ Customer entity غير موجود بعد)
/// - Vendor:   دفعة لـ/من مورّد (AP — مدعوم حالياً عبر Vendor من Procurement)
/// يُخزَّن كـ string في DB لتجنّب كسر التطابق مع SalesInvoices المستقبلي.
/// </summary>
public static class PaymentPartyTypes
{
    public const string Customer = "Customer";
    public const string Vendor = "Vendor";
    public static readonly string[] All = { Customer, Vendor };
}

/// <summary>
/// طرق الدفع المعتمدة في MVP.
/// Cash   — نقدي (سند قبض/صرف نقدي)
/// Bank   — تحويل بنكي
/// Check  — شيك
/// Transfer — تحويل مصرفي (مرادف لـ Bank)
/// </summary>
public static class PaymentMethods
{
    public const string Cash = "Cash";
    public const string Bank = "Bank";
    public const string Transfer = "Transfer";
    public const string Check = "Check";
    public static readonly string[] All = { Cash, Bank, Transfer, Check };
}

/// <summary>
/// حالات الـ Payment.
/// - Draft:    تم إنشاؤه لكن لم يُرحَّل
/// - Posted:   تم الترحيل — JournalEntry نُشر + allocations مُحسوبة
/// - Cancelled: ملغي (يُستخدم لاسترداد المدفوعات المُدخلة بالخطأ)
/// </summary>
public enum PaymentStatus
{
    Draft = 1,
    Posted = 2,
    Cancelled = 3,
}

/// <summary>
/// نوع مرجع التخصيص (PaymentAllocation).
/// يُخزَّن كـ string في DB.
/// - SalesInvoice: فاتورة عميل (AR — مستقبلي)
/// - VendorBill:   فاتورة مورّد (AP — مدعوم حالياً)
/// </summary>
public static class PaymentRefTypes
{
    public const string SalesInvoice = "SalesInvoice";
    public const string VendorBill = "VendorBill";
    public static readonly string[] All = { SalesInvoice, VendorBill };
}

/// <summary>
/// سند دفع/قبض (Payment) — يُستخدم لحالتين:
/// 1. AP (دفع مورّد): Dr 2210 (AP) / Cr 1210 (Cash) عند الترحيل
/// 2. AR (استرداد عميل/قبض): Dr 1210 / Cr 1230 — مرن لتغطية أي اتجاه
///
/// يدعم "On Account" semantics: لو sum(allocations) < amount، الباقي
/// يُسجَّل كـ "دفعة مقدمة" (سالب الرصيد أو Prepayment حسب الـ type).
/// </summary>
public class Payment
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CompanyId { get; set; }

    /// <summary>نوع الطرف: "Customer" أو "Vendor".</summary>
    public string PartyType { get; set; } = string.Empty;
    public Guid PartyId { get; set; }

    /// <summary>رقم تسلسلي داخل الـ tenant. مثال: "PAY-2026-0001".</summary>
    public string PaymentNumber { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "LYD";

    /// <summary>طريقة الدفع: "Cash" | "Bank" | "Transfer" | "Check".</summary>
    public string PaymentMethod { get; set; } = PaymentMethods.Cash;

    /// <summary>حساب بنكي (مستقبلي — الآن nullable، يُترك null في MVP).</summary>
    public Guid? BankAccountId { get; set; }

    public string? Notes { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Draft;
    public DateTime? PostedAt { get; set; }
    public Guid? PostedBy { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>JournalEntry المُنشأ عند الترحيل (Dr/Cr).</summary>
    public Guid? JournalEntryId { get; set; }

    // Navigation
    public List<PaymentAllocation> Allocations { get; set; } = new();
}

/// <summary>
/// تخصيص دفعة على فاتورة (سند قبض/صرف).
/// كل PaymentAllocation يخصم من ref_id (VendorBill أو SalesInvoice).
/// sum(amountApplied) ≤ Payment.Amount. الفرق = "On Account" (دفعة مقدمة).
/// </summary>
public class PaymentAllocation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PaymentId { get; set; }

    /// <summary>"SalesInvoice" أو "VendorBill".</summary>
    public string RefType { get; set; } = string.Empty;
    public Guid RefId { get; set; }
    public decimal AmountApplied { get; set; }
}
