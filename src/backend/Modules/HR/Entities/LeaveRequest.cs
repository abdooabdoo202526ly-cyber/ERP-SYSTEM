using System;

namespace ERPSystem.Modules.HR.Entities;

public enum LeaveType
{
    Annual = 1,
    Sick = 2,
    Emergency = 3,
    Unpaid = 4
}

public enum LeaveStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

/// <summary>
/// طلب إجازة — workflow بسيط: Pending → Approved | Rejected.
/// Business Rule: لا يتعارض مع إجازة Approved أخرى لنفس الموظف.
/// </summary>
public class LeaveRequest
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public LeaveType LeaveType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalDays { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public string? Reason { get; set; }
    public Guid? ApproverId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}
