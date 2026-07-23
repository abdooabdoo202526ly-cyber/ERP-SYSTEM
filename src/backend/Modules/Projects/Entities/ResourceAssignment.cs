using System;

namespace ERPSystem.Modules.Projects.Entities;

/// <summary>
/// تعيين مورد على مهمة — يلتقط HourlyRate snapshot وقت التعيين.
/// </summary>
public class ResourceAssignment
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid TaskId { get; set; }
    public Guid ResourceId { get; set; }
    public Guid UserId { get; set; }                  // المستخدم المعيَّن (إذا كان Resource.Labor)
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal HourlyRate { get; set; }          // snapshot
    public DateTime CreatedAt { get; set; }

    public decimal EstimatedHours => Math.Max(0, (decimal)(To - From).TotalHours);
    public decimal EstimatedCost => EstimatedHours * HourlyRate;
}
