using Dapper;
using ERPSystem.Modules.Projects.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Projects.Infrastructure;

public sealed class ResourceRepository : IResourceRepository
{
    private readonly IDbConnectionFactory _db;
    public ResourceRepository(IDbConnectionFactory db) => _db = db;
    private const string Sel = @"id, tenant_id AS TenantId, code, name, type, hourly_rate AS HourlyRate,
        is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt";

    public async Task<Resource?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Resource>(new CommandDefinition(
            $"SELECT {Sel} FROM resources WHERE id = @Id LIMIT 1", new { Id = id }, cancellationToken: ct));
    }
    public async Task<Resource?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Resource>(new CommandDefinition(
            $"SELECT {Sel} FROM resources WHERE tenant_id = @TenantId AND LOWER(code) = LOWER(@Code) LIMIT 1",
            new { TenantId = tenantId, Code = code }, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<Resource>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {Sel} FROM resources WHERE tenant_id = @TenantId"
            + (includeInactive ? "" : " AND is_active = true") + " ORDER BY code";
        var rows = await conn.QueryAsync<Resource>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        return rows.AsList();
    }
    public async Task InsertAsync(Resource resource, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO resources (id, tenant_id, code, name, type, hourly_rate, is_active, created_at, updated_at)
            VALUES (@Id, @TenantId, @Code, @Name, @Type, @HourlyRate, @IsActive, @CreatedAt, @UpdatedAt)",
            resource, cancellationToken: ct));
    }
    public async Task UpdateAsync(Resource resource, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE resources SET name = @Name, type = @Type, hourly_rate = @HourlyRate,
                                is_active = @IsActive, updated_at = @UpdatedAt
            WHERE id = @Id", resource, cancellationToken: ct));
    }
}
