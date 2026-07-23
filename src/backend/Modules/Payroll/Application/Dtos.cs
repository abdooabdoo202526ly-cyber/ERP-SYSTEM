using System;
using System.Collections.Generic;
using ERPSystem.Modules.Payroll.Domain.Entities;

namespace ERPSystem.Modules.Payroll.Application;

// ============== Requests ==============

/// <summary>طلب إنشاء دورة رواتب جديدة (Draft).</summary>
public sealed class CreatePayrollRunRequest
{
    /// <summary>تاريخ بداية فترة الرواتب (شامل).</summary>
    public DateTime PeriodStart { get; set; }
    /// <summary>تاريخ نهاية فترة الرواتب (شامل). يجب أن يكون >= PeriodStart.</summary>
    public DateTime PeriodEnd { get; set; }
    /// <summary>ملاحظات اختيارية (مثل "رواتب شهر يونيو").</summary>
    public string? Notes { get; set; }
}

// ============== Responses ==============

/// <summary>استجابة دورة الرواتب (للـ list/get/create).</summary>
public sealed class PayrollRunResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public PayrollRunStatus Status { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalNet { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? PostedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ItemsCount { get; set; }
}

/// <summary>استجابة قسيمة راتب (payslip) لموظف ضمن دورة.</summary>
public sealed class PayslipResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PayrollRunId { get; set; }
    public Guid EmployeeId { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }

    public decimal BaseSalary { get; set; }
    public decimal GrossSalary { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal SocialInsuranceEmployee { get; set; }
    public decimal NetSalary { get; set; }

    public PayrollItemStatus Status { get; set; }
    public int PaymentDays { get; set; }
    public string? Notes { get; set; }

    public List<PayslipComponentResponse> Components { get; set; } = new();
}

/// <summary>سطر مكوّن في قسيمة الراتب.</summary>
public sealed class PayslipComponentResponse
{
    public Guid Id { get; set; }
    public SalaryComponentType ComponentType { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>استجابة حساب نهاية الخدمة (EOS).</summary>
public sealed class EosResponse
{
    public Guid EmployeeId { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime HireDate { get; set; }
    public DateTime TerminationDate { get; set; }
    public decimal YearsOfService { get; set; }
    public decimal MonthlySalary { get; set; }
    public decimal EosAmount { get; set; }

    /// <summary>تفصيل الحساب (للتوثيق).</summary>
    public string Formula { get; set; } = string.Empty;
}