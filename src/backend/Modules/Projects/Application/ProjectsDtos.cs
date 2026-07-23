using System;
using ERPSystem.Modules.Projects.Entities;
using TaskStatus = ERPSystem.Modules.Projects.Entities.TaskStatus;

namespace ERPSystem.Modules.Projects.Application;

public sealed class CreateProjectRequest
{
    public Guid? CompanyId { get; set; }  // v1.0.22: nullable — many tenants use single-company mode
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? CostCenterId { get; set; }  // v1.0.22: accept cost center from form
    public int Status { get; set; } = 1;     // v1.0.22: Planning default
    public decimal Budget { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public sealed class UpdateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CustomerId { get; set; }
    public decimal Budget { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public sealed class ProjectResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid CostCenterId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CustomerId { get; set; }
    public ProjectStatus Status { get; set; }
    public decimal Budget { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }
}

// ===== Tasks =====

public sealed class CreateTaskRequest
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal EstimatedHours { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public sealed class UpdateTaskRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskStatus Status { get; set; }
    public decimal EstimatedHours { get; set; }
    public decimal ActualHours { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int ProgressPercent { get; set; }
}

public sealed class TaskResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskStatus Status { get; set; }
    public decimal EstimatedHours { get; set; }
    public decimal ActualHours { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int ProgressPercent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// ===== Resources =====

public sealed class CreateResourceRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ResourceType Type { get; set; }
    public decimal HourlyRate { get; set; }
}

public sealed class UpdateResourceRequest
{
    public string Name { get; set; } = string.Empty;
    public ResourceType Type { get; set; }
    public decimal HourlyRate { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ResourceResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ResourceType Type { get; set; }
    public decimal HourlyRate { get; set; }
    public bool IsActive { get; set; }
}

// ===== Budget =====

public sealed class ProjectBudgetResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid CostCenterId { get; set; }
    public Guid? AccountId { get; set; }
    public decimal BudgetAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal CommittedAmount { get; set; }
    public decimal AvailableAmount { get; set; }
    public decimal UtilizationPercent { get; set; }
    public DateTime? LastRecalculatedAt { get; set; }
}

// ===== Assignments =====

public sealed class CreateAssignmentRequest
{
    public Guid ProjectId { get; set; }
    public Guid TaskId { get; set; }
    public Guid ResourceId { get; set; }
    public Guid UserId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}

public sealed class AssignmentResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid TaskId { get; set; }
    public Guid ResourceId { get; set; }
    public Guid UserId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal EstimatedHours { get; set; }
    public decimal EstimatedCost { get; set; }
    public DateTime CreatedAt { get; set; }
}
