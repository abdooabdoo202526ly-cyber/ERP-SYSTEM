using System;

namespace ERPSystem.Modules.Projects.Entities;

public enum ProjectStatus
{
    Planning = 1,
    Active = 2,
    OnHold = 3,
    Completed = 4,
    Cancelled = 5
}

/// <summary>
/// مشروع — مرتبط بشركة (CompanyId) وبـ CostCenter (auto-created)
/// </summary>
public class Project
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid CostCenterId { get; set; }     // يُنشأ تلقائياً عند إنشاء المشروع

    public string Code { get; set; } = string.Empty;   // "PRJ-2026-001"
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CustomerId { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Planning;

    public decimal Budget { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;
}
