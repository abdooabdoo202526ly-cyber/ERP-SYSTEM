using System;
using System.Collections.Generic;
using ERPSystem.Modules.Payroll.Domain.Entities;

namespace ERPSystem.Modules.Payroll.Application;

// ============== SalaryStructure DTOs ==============

/// <summary>
/// طلب إنشاء هيكل رواتب جديد (مع مكوّناته Lines).
/// الـ Lines تُحفظ كـ aggregate مع الهيكل — full replace عند التحديث.
/// </summary>
public sealed class CreateSalaryStructureRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Currency { get; set; }
    public bool? IsActive { get; set; } = true;
    public List<CreateSalaryStructureLineRequest> Lines { get; set; } = new();
}

/// <summary>سطر مكوّن (earning / deduction) لإنشاء هيكل راتب.</summary>
public sealed class CreateSalaryStructureLineRequest
{
    public SalaryComponentType Type { get; set; } = SalaryComponentType.Earning;
    public string Name { get; set; } = string.Empty;
    public string? Formula { get; set; }
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>استجابة هيكل راتب (تحوي الـ lines كاملة).</summary>
public sealed class SalaryStructureResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Currency { get; set; } = "LYD";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<SalaryStructureLineResponse> Lines { get; set; } = new();

    // Totals (محسوبة في الـ Service — مفيدة للـ FE).
    public decimal TotalEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
}

/// <summary>سطر مكوّن في استجابة هيكل الراتب.</summary>
public sealed class SalaryStructureLineResponse
{
    public Guid Id { get; set; }
    public SalaryComponentType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Formula { get; set; }
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }
}
