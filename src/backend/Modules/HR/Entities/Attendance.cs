using System;

namespace ERPSystem.Modules.HR.Entities;

public enum AttendanceType
{
    CheckIn = 1,
    CheckOut = 2
}

/// <summary>
/// سجل حضور — كل CheckIn/CheckOut صف منفصل.
/// الـ business logic: لا تكرار CheckIn متتالي بدون CheckOut (يُتحقق في الـ service).
/// </summary>
public class Attendance
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public AttendanceType Type { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Notes { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
