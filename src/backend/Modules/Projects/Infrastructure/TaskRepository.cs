using Dapper;
using ERPSystem.Modules.Projects.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Projects.Infrastructure;

public sealed class TaskRepository : ITaskRepository
{
    private readonly IDbConnectionFactory _db;
    public TaskRepository(IDbConnectionFactory db) => _db = db;
    private const string Sel = @"id, tenant_id AS TenantId, project_id AS ProjectId, name, description, status,
        estimated_hours AS EstimatedHours, actual_hours AS ActualHours,
        start_date AS StartDate, end_date AS EndDate, progress_percent AS ProgressPercent,
        created_at AS CreatedAt, updated_at AS UpdatedAt";

    public async Task<ProjectTask?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<ProjectTask>(new CommandDefinition(
            $"SELECT {Sel} FROM project_tasks WHERE id = @Id LIMIT 1", new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ProjectTask>> ListByProjectAsync(Guid projectId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var rows = await conn.QueryAsync<ProjectTask>(new CommandDefinition(
            $"SELECT {Sel} FROM project_tasks WHERE project_id = @ProjectId ORDER BY created_at", new { ProjectId = projectId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertAsync(ProjectTask task, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO project_tasks (id, tenant_id, project_id, name, description, status, estimated_hours, actual_hours,
                                        start_date, end_date, progress_percent, created_at, updated_at)
            VALUES (@Id, @TenantId, @ProjectId, @Name, @Description, @Status, @EstimatedHours, @ActualHours,
                    @StartDate, @EndDate, @ProgressPercent, @CreatedAt, @UpdatedAt)", task, cancellationToken: ct));
    }

    public async Task UpdateAsync(ProjectTask task, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE project_tasks SET name = @Name, description = @Description, status = @Status,
                                       estimated_hours = @EstimatedHours, actual_hours = @ActualHours,
                                       start_date = @StartDate, end_date = @EndDate,
                                       progress_percent = @ProgressPercent, updated_at = @UpdatedAt
            WHERE id = @Id", task, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM project_tasks WHERE id = @Id", new { Id = id }, cancellationToken: ct));
    }
}
