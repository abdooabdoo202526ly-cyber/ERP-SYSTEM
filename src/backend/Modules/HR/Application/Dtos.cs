using System;
using System.Collections.Generic;
using ERPSystem.Modules.HR.Entities;

namespace ERPSystem.Modules.HR.Application;

// ============== Department DTOs ==============

public sealed class CreateDepartmentRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Guid? ManagerId { get; set; }
}

public sealed class UpdateDepartmentRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Guid? ManagerId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class DepartmentResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Guid? ManagerId { get; set; }
    public bool IsActive { get; set; }
}

// ============== Employee DTOs ==============

public sealed class CreateEmployeeRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? NationalId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? JobTitle { get; set; }
    public DateTime HireDate { get; set; } = DateTime.UtcNow;
    public decimal BaseSalary { get; set; }
}

public sealed class UpdateEmployeeRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? NationalId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? JobTitle { get; set; }
    public DateTime? TerminationDate { get; set; }
    public decimal BaseSalary { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class EmployeeResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? NationalId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? JobTitle { get; set; }
    public DateTime HireDate { get; set; }
    public DateTime? TerminationDate { get; set; }
    public decimal BaseSalary { get; set; }
    public bool IsActive { get; set; }
}

// ============== Attendance DTOs ==============

public sealed class CheckInOutRequest
{
    public Guid EmployeeId { get; set; }
    public AttendanceType Type { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// T4-HR-Details: تحديث سجل حضور قابل للتعديل في نفس اليوم فقط.
/// لا نسمح بتعديل Type/EmployeeId/Timestamp (حقائق لا تتغير) — فقط Notes.
/// </summary>
public sealed class UpdateAttendanceRequest
{
    public string? Notes { get; set; }
}

public sealed class AttendanceResponse
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public AttendanceType Type { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Notes { get; set; }
    public string? IpAddress { get; set; }
}

// ============== LeaveRequest DTOs ==============

public sealed class CreateLeaveRequestDto
{
    public Guid EmployeeId { get; set; }
    public LeaveType LeaveType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// T4-HR-Details: تحديث طلب إجازة قابل للتعديل فقط إذا الحالة Pending.
/// نسمح بتعديل كل الحقول ما عدا الحالة (التي تتغير عبر approve/reject).
/// </summary>
public sealed class UpdateLeaveRequestDto
{
    public LeaveType LeaveType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}

public sealed class LeaveRequestResponse
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public LeaveType LeaveType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalDays { get; set; }
    public LeaveStatus Status { get; set; }
    public string? Reason { get; set; }
    public Guid? ApproverId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Notes { get; set; }
}
