using Dapper;
using ERPSystem.Modules.Companies.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Companies.Infrastructure;

public sealed class CostCenterRepository : ICostCenterRepository
{
    private readonly IDbConnectionFactory _db;
    public CostCenterRepository(IDbConnectionFactory db) => _db = db;

    private const string SelectColumns = @"id, tenant_id AS TenantId, company_id AS CompanyId, code, name, type, parent_id AS ParentId,
        budget_amount AS BudgetAmount, start_date AS StartDate, end_date AS EndDate,
        sku, location, activity_category AS ActivityCategory,
        is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt";

    public async Task<CostCenter?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<CostCenter>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM cost_centers WHERE id = @Id LIMIT 1",
            new { Id = id }, cancellationToken: ct));
    }

    public async Task<CostCenter?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<CostCenter>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM cost_centers WHERE tenant_id = @TenantId AND LOWER(code) = LOWER(@Code) LIMIT 1",
            new { TenantId = tenantId, Code = code }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<CostCenter>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var idArr = ids.ToArray();
        if (idArr.Length == 0) return Array.Empty<CostCenter>();
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return (await conn.QueryAsync<CostCenter>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM cost_centers WHERE id = ANY(@Ids)",
            new { Ids = idArr }, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<CostCenter>> ListAsync(Guid tenantId, Guid? companyId, CostCenterType? type, bool includeInactive, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {SelectColumns} FROM cost_centers WHERE tenant_id = @TenantId";
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (companyId.HasValue) { sql += " AND company_id = @CompanyId"; p.Add("CompanyId", companyId.Value); }
        if (type.HasValue) { sql += " AND type = @Type"; p.Add("Type", (int)type.Value); }
        if (!includeInactive) sql += " AND is_active = true";
        sql += " ORDER BY code";
        var rows = await conn.QueryAsync<CostCenter>(new CommandDefinition(sql, p, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<CostCenter>> ListChildrenAsync(Guid parentId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var rows = await conn.QueryAsync<CostCenter>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM cost_centers WHERE parent_id = @ParentId ORDER BY code",
            new { ParentId = parentId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertAsync(CostCenter cc, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO cost_centers (id, tenant_id, company_id, code, name, type, parent_id,
                                      budget_amount, start_date, end_date, sku, location, activity_category,
                                      is_active, created_at, updated_at)
            VALUES (@Id, @TenantId, @CompanyId, @Code, @Name, @Type, @ParentId,
                    @BudgetAmount, @StartDate, @EndDate, @Sku, @Location, @ActivityCategory,
                    @IsActive, @CreatedAt, @UpdatedAt)", cc, cancellationToken: ct));
    }

    public async Task UpdateAsync(CostCenter cc, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE cost_centers SET name = @Name, type = @Type, parent_id = @ParentId,
                                    budget_amount = @BudgetAmount, start_date = @StartDate,
                                    end_date = @EndDate, sku = @Sku, location = @Location,
                                    activity_category = @ActivityCategory,
                                    is_active = @IsActive, updated_at = @UpdatedAt
            WHERE id = @Id", cc, cancellationToken: ct));
    }
}
