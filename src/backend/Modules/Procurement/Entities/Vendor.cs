using System;

namespace ERPSystem.Modules.Procurement.Entities;

/// <summary>
/// مورّد — يدعم دورة المشتريات الكاملة (PO → GR → Bill).
/// كل مورّد مملوك لـ tenant (multi-tenant) ومرتبط بـ vendor bills.
/// </summary>
public class Vendor
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>كود فريد داخل الـ tenant — يُستخدم في الـ PO و Bill.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }

    /// <summary>الرقم الضريبي (اختياري) — يُستخدم في الفاتورة الإلكترونية (ZATCA/ETA مستقبلاً).</summary>
    public string? TaxNumber { get; set; }

    /// <summary>الموقع الإلكتروني (اختياري) — أُضيف في DEC-081b.</summary>
    public string? Website { get; set; }

    public string Currency { get; set; } = "LYD";
    public string PaymentTerms { get; set; } = "Net30";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>شروط الدفع المدعومة في MVP.</summary>
public static class PaymentTerms
{
    public const string Cash = "Cash";
    public const string Net15 = "Net15";
    public const string Net30 = "Net30";
    public const string Net60 = "Net60";
    public const string Net90 = "Net90";
    public static readonly string[] All = { Cash, Net15, Net30, Net60, Net90 };
}
