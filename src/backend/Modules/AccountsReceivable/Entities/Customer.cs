using System;

namespace ERPSystem.Modules.AccountsReceivable.Entities;

/// <summary>
/// عميل (Customer) — يدعم دورة الذمم المدينة الكاملة (SalesInvoice → Receipt).
/// كل عميل مملوك لـ tenant (multi-tenant) ومرتبط بـ sales_invoices.
/// </summary>
public class Customer
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CompanyId { get; set; }

    /// <summary>كود فريد داخل الـ tenant — يُستخدم في الـ SalesInvoice.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>اسم العميل بالعربية (إلزامي).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>اسم العميل بالإنجليزية (اختياري — للفواتير الدولية).</summary>
    public string? NameEn { get; set; }

    /// <summary>الرقم الضريبي (اختياري).</summary>
    public string? TaxId { get; set; }

    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }

    /// <summary>حد الائتمان المسموح (decimal 18,4) — null يعني بدون حد.</summary>
    public decimal? CreditLimit { get; set; }

    /// <summary>شروط الدفع بالأيام (افتراضي 30 يوم).</summary>
    public int PaymentTermsDays { get; set; } = 30;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
