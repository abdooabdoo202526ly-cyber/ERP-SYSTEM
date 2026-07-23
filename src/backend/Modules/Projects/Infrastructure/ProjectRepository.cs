using Dapper;
using ERPSystem.Modules.Projects.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Projects.Infrastructure;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly IDbConnectionFactory _db;
    public ProjectRepository(IDbConnectionFactory db) => _db = db;
    private const string Sel = @"id, tenant_id AS TenantId, company_id AS CompanyId, cost_center_id AS CostCenterId,
        code, name, description, customer_id AS CustomerId, status, budget, start_date AS StartDate, end_date AS EndDate,
        created_at AS CreatedAt, created_by AS CreatedBy, updated_at AS UpdatedAt, updated_by AS UpdatedBy, is_active AS IsActive";

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Project>(new CommandDefinition(
            $"SELECT {Sel} FROM projects WHERE id = @Id LIMIT 1", new { Id = id }, cancellationToken: ct));
    }

    public async Task<Project?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Project>(new CommandDefinition(
            $"SELECT {Sel} FROM projects WHERE tenant_id = @TenantId AND LOWER(code) = LOWER(@Code) LIMIT 1",
            new { TenantId = tenantId, Code = code }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Project>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var idArr = ids.ToArray();
        if (idArr.Length == 0) return Array.Empty<Project>();
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return (await conn.QueryAsync<Project>(new CommandDefinition(
            $"SELECT {Sel} FROM projects WHERE id = ANY(@Ids)",
            new { Ids = idArr }, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<Project>> ListAsync(Guid tenantId, Guid? companyId, ProjectStatus? status, bool includeInactive, int skip, int take, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {Sel} FROM projects WHERE tenant_id = @TenantId";
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (companyId.HasValue) { sql += " AND company_id = @CompanyId"; p.Add("CompanyId", companyId.Value); }
        if (status.HasValue) { sql += " AND status = @Status"; p.Add("Status", (int)status.Value); }
        if (!includeInactive) sql += " AND is_active = true";
        sql += " ORDER BY start_date DESC OFFSET @Skip LIMIT @Take";
        p.Add("Skip", skip); p.Add("Take", take);
        var rows = await conn.QueryAsync<Project>(new CommandDefinition(sql, p, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertAsync(Project project, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO projects (id, tenant_id, company_id, cost_center_id, code, name, description, customer_id,
                                  status, budget, start_date, end_date, created_at, created_by, updated_at, updated_by, is_active)
            VALUES (@Id, @TenantId, @CompanyId, @CostCenterId, @Code, @Name, @Description, @CustomerId,
                    @Status, @Budget, @StartDate, @EndDate, @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy, @IsActive)",
            project, cancellationToken: ct));
    }

    public async Task UpdateAsync(Project project, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE projects SET name = @Name, description = @Description, customer_id = @CustomerId,
                                 budget = @Budget, start_date = @StartDate, end_date = @EndDate,
                                 status = @Status, updated_at = @UpdatedAt, updated_by = @UpdatedBy,
                                 is_active = @IsActive
            WHERE id = @Id", project, cancellationToken: ct));
    }
}
