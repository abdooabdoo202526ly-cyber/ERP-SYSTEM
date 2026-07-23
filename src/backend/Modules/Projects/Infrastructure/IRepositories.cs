using ERPSystem.Modules.Projects.Entities;

namespace ERPSystem.Modules.Projects.Infrastructure;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Project?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct);
    Task<IReadOnlyList<Project>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct); // v1.0.34
    Task<IReadOnlyList<Project>> ListAsync(Guid tenantId, Guid? companyId, ProjectStatus? status, bool includeInactive, int skip, int take, CancellationToken ct);
    Task InsertAsync(Project project, CancellationToken ct);
    Task UpdateAsync(Project project, CancellationToken ct);
}

public interface ITaskRepository
{
    Task<ProjectTask?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ProjectTask>> ListByProjectAsync(Guid projectId, CancellationToken ct);
    Task InsertAsync(ProjectTask task, CancellationToken ct);
    Task UpdateAsync(ProjectTask task, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public interface IResourceRepository
{
    Task<Resource?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Resource?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct);
    Task<IReadOnlyList<Resource>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct);
    Task InsertAsync(Resource resource, CancellationToken ct);
    Task UpdateAsync(Resource resource, CancellationToken ct);
}

public interface IProjectBudgetRepository
{
    Task<ProjectBudget?> GetByProjectAsync(Guid projectId, CancellationToken ct);
    Task<ProjectBudget?> GetByIdAsync(Guid id, CancellationToken ct);
    Task InsertAsync(ProjectBudget budget, CancellationToken ct);
    Task UpdateAsync(ProjectBudget budget, CancellationToken ct);
    /// <summary>إعادة حساب SpentAmount من journal_lines (JOIN على cost_center_id)</summary>
    Task<decimal> RecalculateSpentAsync(Guid projectId, Guid costCenterId, CancellationToken ct);
}

public interface IResourceAssignmentRepository
{
    Task<ResourceAssignment?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ResourceAssignment>> ListByProjectAsync(Guid projectId, CancellationToken ct);
    Task InsertAsync(ResourceAssignment assignment, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
