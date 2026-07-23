using System;

namespace ERPSystem.Modules.Projects.Entities;

public enum TaskStatus
{
    NotStarted = 1,
    InProgress = 2,
    Blocked = 3,
    Completed = 4,
    Cancelled = 5
}

/// <summary>
/// مهمة داخل مشروع — تتابع التقدم (0-100%)
/// </summary>
public class ProjectTask
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.NotStarted;
    public decimal EstimatedHours { get; set; }
    public decimal ActualHours { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int ProgressPercent { get; set; }   // 0-100
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
