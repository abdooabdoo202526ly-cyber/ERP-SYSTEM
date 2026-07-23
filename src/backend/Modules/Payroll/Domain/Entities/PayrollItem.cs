using System;
using System.Collections.Generic;

namespace ERPSystem.Modules.Payroll.Domain.Entities;

/// <summary>
/// حالة قسيمة الراتب (Salary Slip / PayrollItem).
/// Transitions:
/// Draft ──► Processed ──► Posted
///   │           │
///   ▼           ▼
/// Cancelled  Cancelled
/// </summary>
public enum PayrollItemStatus
{
    Draft = 1,
    Processed = 2,
    Posted = 3,
    Cancelled = 4
}

/// <summary>
/// قسيمة راتب موظف واحد ضمن PayrollRun.
///
/// Business Rules:
/// - (PayrollRunId, EmployeeId) UNIQUE — موظف واحد له قسيمة واحدة في الـ run.
/// - base_salary يُنسخ من Employee.BaseSalary وقت الـ Process (snapshot، لا live).
/// - gross_salary = base + sum(earnings)
/// - net_salary = gross - sum(deductions) - tax - social_insurance
/// - payment_days: عدد أيام العمل الفعلية (افتراضي 30، يُخصم منها أيام الإجازة غير المدفوعة).
/// - بعد Posted: لا تعديل (SOX).
/// </summary>
public class PayrollItem
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PayrollRunId { get; set; }
    public Guid EmployeeId { get; set; }

    /// <summary>الراتب الأساسي وقت الـ Process (snapshot).</summary>
    public decimal BaseSalary { get; set; }

    /// <summary>إجمالي قبل الخصومات.</summary>
    public decimal GrossSalary { get; set; }

    /// <summary>ضريبة الدخل (Libya GDT brackets: 5% / 10%).</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>التأمينات الاجتماعية (حصة الموظف: 3.75%).</summary>
    public decimal SocialInsuranceEmployee { get; set; }

    /// <summary>صافي الراتب — ما يحصل عليه الموظف.</summary>
    public decimal NetSalary { get; set; }

    public PayrollItemStatus Status { get; set; } = PayrollItemStatus.Draft;

    /// <summary>عدد أيام العمل الفعلية (0-30)، افتراضي 30.</summary>
    public int PaymentDays { get; set; } = 30;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>تفاصيل المكوّنات (غير محفوظة مباشرة — تُحمَّل منفصلة).</summary>
    public List<PayslipComponent> Components { get; set; } = new();
}

/// <summary>
/// مكوّن واحد في قسيمة الراتب (سطر تفصيلي).
///
/// Business Rules:
/// - ComponentType: earning أو deduction (validation في Application).
/// - Amount يخزّن Decimal(18,4).
/// - SortOrder يتحكم بترتيب العرض في الـ payslip view.
/// - عند حذف PayrollItem: تُحذف كل Components تلقائياً (ON DELETE CASCADE).
/// </summary>
public class PayslipComponent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PayrollItemId { get; set; }
    public SalaryComponentType ComponentType { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }
}