using System;

namespace ERPSystem.Modules.HR.Entities;

/// <summary>
/// موظف — يمثل شخصاً يعمل في الشركة.
/// BaseSalary للعرض فقط في هذه المرحلة (Payroll في Phase 4).
/// </summary>
public class Employee
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>رقم الموظف التسلسلي (فريد داخل الـ tenant). مثال: "EMP-2026-0001".</summary>
    public string EmployeeNumber { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? NationalId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? JobTitle { get; set; }
    public DateTime HireDate { get; set; }
    public DateTime? TerminationDate { get; set; }
    public decimal BaseSalary { get; set; }     // للعرض فقط — لا payroll في هذه المرحلة
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
