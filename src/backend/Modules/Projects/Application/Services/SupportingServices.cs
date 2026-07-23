using ERPSystem.Modules.Projects.Application;
using ERPSystem.Modules.Projects.Entities;
using TaskStatus = ERPSystem.Modules.Projects.Entities.TaskStatus;
using ERPSystem.Modules.Projects.Infrastructure;

namespace ERPSystem.Modules.Projects.Application.Services;

public interface ITaskService
{
    Task<ProjectResult<TaskResponse>> CreateAsync(Guid tenantId, CreateTaskRequest req, CancellationToken ct);
    Task<ProjectResult<TaskResponse>> UpdateAsync(Guid tenantId, Guid id, UpdateTaskRequest req, CancellationToken ct);
    Task<ProjectResult<TaskResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<ProjectResult<IReadOnlyList<TaskResponse>>> ListByProjectAsync(Guid tenantId, Guid projectId, CancellationToken ct);
    Task<ProjectResult<bool>> DeleteAsync(Guid tenantId, Guid id, CancellationToken ct);
}

public sealed class TaskService : ITaskService
{
    private readonly ITaskRepository _repo;
    public TaskService(ITaskRepository repo) => _repo = repo;

    public async Task<ProjectResult<TaskResponse>> CreateAsync(Guid tenantId, CreateTaskRequest req, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var t = new ProjectTask
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = req.ProjectId,
            Name = req.Name.Trim(), Description = req.Description,
            Status = TaskStatus.NotStarted, EstimatedHours = req.EstimatedHours, ActualHours = 0,
            StartDate = req.StartDate, EndDate = req.EndDate, ProgressPercent = 0,
            CreatedAt = now, UpdatedAt = now
        };
        await _repo.InsertAsync(t, ct);
        return ProjectResult<TaskResponse>.Ok(MapToResponse(t));
    }

    public async Task<ProjectResult<TaskResponse>> UpdateAsync(Guid tenantId, Guid id, UpdateTaskRequest req, CancellationToken ct)
    {
        var t = await _repo.GetByIdAsync(id, ct);
        if (t == null || t.TenantId != tenantId) return ProjectResult<TaskResponse>.Fail("غير موجود.", ProjectErrorCode.NotFound);
        t.Name = req.Name.Trim();
        t.Description = req.Description;
        t.Status = req.Status;
        t.EstimatedHours = req.EstimatedHours;
        t.ActualHours = req.ActualHours;
        t.StartDate = req.StartDate;
        t.EndDate = req.EndDate;
        t.ProgressPercent = req.ProgressPercent;
        t.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(t, ct);
        return ProjectResult<TaskResponse>.Ok(MapToResponse(t));
    }

    public async Task<ProjectResult<TaskResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var t = await _repo.GetByIdAsync(id, ct);
        if (t == null || t.TenantId != tenantId) return ProjectResult<TaskResponse>.Fail("غير موجود.", ProjectErrorCode.NotFound);
        return ProjectResult<TaskResponse>.Ok(MapToResponse(t));
    }

    public async Task<ProjectResult<IReadOnlyList<TaskResponse>>> ListByProjectAsync(Guid tenantId, Guid projectId, CancellationToken ct)
    {
        var list = await _repo.ListByProjectAsync(projectId, ct);
        return ProjectResult<IReadOnlyList<TaskResponse>>.Ok(list.Select(MapToResponse).ToList());
    }

    public async Task<ProjectResult<bool>> DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var t = await _repo.GetByIdAsync(id, ct);
        if (t == null || t.TenantId != tenantId) return ProjectResult<bool>.Fail("غير موجود.", ProjectErrorCode.NotFound);
        await _repo.DeleteAsync(id, ct);
        return ProjectResult<bool>.Ok(true);
    }

    private static TaskResponse MapToResponse(ProjectTask t) => new()
    {
        Id = t.Id, ProjectId = t.ProjectId, Name = t.Name, Description = t.Description,
        Status = t.Status, EstimatedHours = t.EstimatedHours, ActualHours = t.ActualHours,
        StartDate = t.StartDate, EndDate = t.EndDate, ProgressPercent = t.ProgressPercent,
        CreatedAt = t.CreatedAt, UpdatedAt = t.UpdatedAt
    };
}

public interface IResourceService
{
    Task<ProjectResult<ResourceResponse>> CreateAsync(Guid tenantId, CreateResourceRequest req, CancellationToken ct);
    Task<ProjectResult<ResourceResponse>> UpdateAsync(Guid tenantId, Guid id, UpdateResourceRequest req, CancellationToken ct);
    Task<ProjectResult<ResourceResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<ProjectResult<IReadOnlyList<ResourceResponse>>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct);
}

public sealed class ResourceService : IResourceService
{
    private readonly IResourceRepository _repo;
    public ResourceService(IResourceRepository r) => _repo = r;

    public async Task<ProjectResult<ResourceResponse>> CreateAsync(Guid tenantId, CreateResourceRequest req, CancellationToken ct)
    {
        if (await _repo.GetByCodeAsync(tenantId, req.Code, ct) != null)
            return ProjectResult<ResourceResponse>.Fail("كود المورد مستخدم.", ProjectErrorCode.AlreadyExists);
        var now = DateTime.UtcNow;
        var r = new Resource
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = req.Code.Trim(), Name = req.Name.Trim(),
            Type = req.Type, HourlyRate = req.HourlyRate, IsActive = true, CreatedAt = now, UpdatedAt = now
        };
        await _repo.InsertAsync(r, ct);
        return ProjectResult<ResourceResponse>.Ok(MapToResponse(r));
    }
    public async Task<ProjectResult<ResourceResponse>> UpdateAsync(Guid tenantId, Guid id, UpdateResourceRequest req, CancellationToken ct)
    {
        var r = await _repo.GetByIdAsync(id, ct);
        if (r == null || r.TenantId != tenantId) return ProjectResult<ResourceResponse>.Fail("غير موجود.", ProjectErrorCode.NotFound);
        r.Name = req.Name.Trim();
        r.Type = req.Type;
        r.HourlyRate = req.HourlyRate;
        r.IsActive = req.IsActive;
        r.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(r, ct);
        return ProjectResult<ResourceResponse>.Ok(MapToResponse(r));
    }
    public async Task<ProjectResult<ResourceResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var r = await _repo.GetByIdAsync(id, ct);
        if (r == null || r.TenantId != tenantId) return ProjectResult<ResourceResponse>.Fail("غير موجود.", ProjectErrorCode.NotFound);
        return ProjectResult<ResourceResponse>.Ok(MapToResponse(r));
    }
    public async Task<ProjectResult<IReadOnlyList<ResourceResponse>>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct)
    {
        var list = await _repo.ListAsync(tenantId, includeInactive, ct);
        return ProjectResult<IReadOnlyList<ResourceResponse>>.Ok(list.Select(MapToResponse).ToList());
    }
    private static ResourceResponse MapToResponse(Resource r) => new()
    {
        Id = r.Id, Code = r.Code, Name = r.Name, Type = r.Type, HourlyRate = r.HourlyRate, IsActive = r.IsActive
    };
}

public interface IBudgetService
{
    Task<ProjectResult<ProjectBudgetResponse>> GetByProjectAsync(Guid tenantId, Guid projectId, CancellationToken ct);
    Task<ProjectResult<ProjectBudgetResponse>> RecalculateSpentAsync(Guid tenantId, Guid projectId, CancellationToken ct);
}

public sealed class BudgetService : IBudgetService
{
    private readonly IProjectBudgetRepository _repo;
    public BudgetService(IProjectBudgetRepository r) => _repo = r;

    public async Task<ProjectResult<ProjectBudgetResponse>> GetByProjectAsync(Guid tenantId, Guid projectId, CancellationToken ct)
    {
        var b = await _repo.GetByProjectAsync(projectId, ct);
        if (b == null || b.TenantId != tenantId) return ProjectResult<ProjectBudgetResponse>.Fail("غير موجود.", ProjectErrorCode.NotFound);
        return ProjectResult<ProjectBudgetResponse>.Ok(MapToResponse(b));
    }

    public async Task<ProjectResult<ProjectBudgetResponse>> RecalculateSpentAsync(Guid tenantId, Guid projectId, CancellationToken ct)
    {
        var b = await _repo.GetByProjectAsync(projectId, ct);
        if (b == null || b.TenantId != tenantId) return ProjectResult<ProjectBudgetResponse>.Fail("غير موجود.", ProjectErrorCode.NotFound);
        await _repo.RecalculateSpentAsync(projectId, b.CostCenterId, ct);
        var updated = await _repo.GetByProjectAsync(projectId, ct);
        return ProjectResult<ProjectBudgetResponse>.Ok(MapToResponse(updated!));
    }

    private static ProjectBudgetResponse MapToResponse(ProjectBudget b) => new()
    {
        Id = b.Id, ProjectId = b.ProjectId, CostCenterId = b.CostCenterId, AccountId = b.AccountId,
        BudgetAmount = b.BudgetAmount, SpentAmount = b.SpentAmount, CommittedAmount = b.CommittedAmount,
        AvailableAmount = b.AvailableAmount, UtilizationPercent = b.UtilizationPercent,
        LastRecalculatedAt = b.LastRecalculatedAt
    };
}

public interface IResourceAssignmentService
{
    Task<ProjectResult<AssignmentResponse>> CreateAsync(Guid tenantId, CreateAssignmentRequest req, CancellationToken ct);
    Task<ProjectResult<IReadOnlyList<AssignmentResponse>>> ListByProjectAsync(Guid tenantId, Guid projectId, CancellationToken ct);
    Task<ProjectResult<bool>> DeleteAsync(Guid tenantId, Guid id, CancellationToken ct);
}

public sealed class ResourceAssignmentService : IResourceAssignmentService
{
    private readonly IResourceAssignmentRepository _repo;
    private readonly IResourceRepository _resources;
    public ResourceAssignmentService(IResourceAssignmentRepository r, IResourceRepository res) { _repo = r; _resources = res; }

    public async Task<ProjectResult<AssignmentResponse>> CreateAsync(Guid tenantId, CreateAssignmentRequest req, CancellationToken ct)
    {
        var resource = await _resources.GetByIdAsync(req.ResourceId, ct);
        if (resource == null || resource.TenantId != tenantId)
            return ProjectResult<AssignmentResponse>.Fail("المورد غير موجود.", ProjectErrorCode.NotFound);
        var a = new ResourceAssignment
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProjectId = req.ProjectId, TaskId = req.TaskId,
            ResourceId = req.ResourceId, UserId = req.UserId, From = req.From, To = req.To,
            HourlyRate = resource.HourlyRate,  // snapshot
            CreatedAt = DateTime.UtcNow
        };
        await _repo.InsertAsync(a, ct);
        return ProjectResult<AssignmentResponse>.Ok(MapToResponse(a));
    }
    public async Task<ProjectResult<IReadOnlyList<AssignmentResponse>>> ListByProjectAsync(Guid tenantId, Guid projectId, CancellationToken ct)
    {
        var list = await _repo.ListByProjectAsync(projectId, ct);
        return ProjectResult<IReadOnlyList<AssignmentResponse>>.Ok(list.Select(MapToResponse).ToList());
    }
    public async Task<ProjectResult<bool>> DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var a = await _repo.GetByIdAsync(id, ct);
        if (a == null || a.TenantId != tenantId) return ProjectResult<bool>.Fail("غير موجود.", ProjectErrorCode.NotFound);
        await _repo.DeleteAsync(id, ct);
        return ProjectResult<bool>.Ok(true);
    }
    private static AssignmentResponse MapToResponse(ResourceAssignment a) => new()
    {
        Id = a.Id, ProjectId = a.ProjectId, TaskId = a.TaskId, ResourceId = a.ResourceId, UserId = a.UserId,
        From = a.From, To = a.To, HourlyRate = a.HourlyRate,
        EstimatedHours = a.EstimatedHours, EstimatedCost = a.EstimatedCost,
        CreatedAt = a.CreatedAt
    };
}
