using System;
using System.Collections.Generic;

namespace ERPSystem.Modules.Payroll.Domain.Entities;

/// <summary>
/// نوع مكوّن الراتب (earning أو deduction).
/// مخزّن كـ نص في DB للقراءة السهلة.
/// </summary>
public enum SalaryComponentType
{
    Earning = 1,
    Deduction = 2
}

/// <summary>
/// هيكل الرواتب — قالب لتعريف مكونات الراتب (الأساسي + البدلات + الخصومات).
/// يُستخدم كأساس لتوليد PayrollItem لكل موظف عند Process run.
/// Business Rules:
/// - Code فريد داخل الـ Tenant (UNIQUE INDEX).
/// - عند IsActive = false: لا يُستخدم في runs جديدة (يبقى للـ runs القديمة).
/// - Lines (SalaryStructureLine) تُحفظ كـ aggregate مع الهيكل.
/// </summary>
public class SalaryStructure
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>الاسم المعروض للـ HR (مثال: "هيكل موظف بدوام كامل").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>الكود الفريد داخل الـ tenant (مثال: "FT-LYD").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>عملة الهيكل (ISO 4217: LYD / USD / EUR). افتراضياً LYD.</summary>
    public string Currency { get; set; } = "LYD";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>مكوّنات الهيكل (غير محفوظة مباشرة — تُحمَّل منفصلة).</summary>
    public List<SalaryStructureLine> Lines { get; set; } = new();
}

/// <summary>
/// مكوّن من مكونات هيكل الراتب (سطر واحد).
/// Business Rules:
/// - Type لا يكون إلا Earning أو Deduction (validation في الـ Application layer).
/// - Amount يخزّن Decimal(18,4) ليدعم الكسور والعملات الصغيرة.
/// - Formula (nullable) مستقبلي: تعبير حسابي يُقيَّم عند الـ Process.
/// </summary>
public class SalaryStructureLine
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SalaryStructureId { get; set; }
    public SalaryComponentType Type { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>صيغة حسابية مستقبلية (مثال: "base * 0.10"). حالياً اختيارية.</summary>
    public string? Formula { get; set; }

    public decimal Amount { get; set; }

    /// <summary>ترتيب العرض داخل الـ payslip (0, 1, 2...).</summary>
    public int SortOrder { get; set; }
}