using Dapper;
using ERPSystem.Modules.Projects.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Projects.Infrastructure;

public sealed class ResourceAssignmentRepository : IResourceAssignmentRepository
{
    private readonly IDbConnectionFactory _db;
    public ResourceAssignmentRepository(IDbConnectionFactory db) => _db = db;
    private const string Sel = @"id, tenant_id AS TenantId, project_id AS ProjectId, task_id AS TaskId,
        resource_id AS ResourceId, user_id AS UserId, from, to,
        hourly_rate AS HourlyRate, created_at AS CreatedAt";

    public async Task<ResourceAssignment?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<ResourceAssignment>(new CommandDefinition(
            $"SELECT {Sel} FROM resource_assignments WHERE id = @Id LIMIT 1", new { Id = id }, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<ResourceAssignment>> ListByProjectAsync(Guid projectId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var rows = await conn.QueryAsync<ResourceAssignment>(new CommandDefinition(
            $"SELECT {Sel} FROM resource_assignments WHERE project_id = @ProjectId ORDER BY from",
            new { ProjectId = projectId }, cancellationToken: ct));
        return rows.AsList();
    }
    public async Task InsertAsync(ResourceAssignment assignment, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO resource_assignments (id, tenant_id, project_id, task_id, resource_id, user_id, from, to, hourly_rate, created_at)
            VALUES (@Id, @TenantId, @ProjectId, @TaskId, @ResourceId, @UserId, @From, @To, @HourlyRate, @CreatedAt)",
            assignment, cancellationToken: ct));
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM resource_assignments WHERE id = @Id", new { Id = id }, cancellationToken: ct));
    }
}
