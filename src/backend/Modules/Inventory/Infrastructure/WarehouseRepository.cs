using Dapper;
using ERPSystem.Modules.Inventory.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Inventory.Infrastructure;

public sealed class WarehouseRepository : IWarehouseRepository
{
    private readonly IDbConnectionFactory _db;
    public WarehouseRepository(IDbConnectionFactory db) => _db = db;
    private const string Sel = @"id, tenant_id AS TenantId, company_id AS CompanyId, code, name, location,
        manager_user_id AS ManagerUserId, is_active AS IsActive, created_at AS CreatedAt,
        created_by AS CreatedBy, updated_at AS UpdatedAt, updated_by AS UpdatedBy";

    public async Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Warehouse>(new CommandDefinition(
            $"SELECT {Sel} FROM warehouses WHERE id = @Id LIMIT 1", new { Id = id }, cancellationToken: ct));
    }
    public async Task<Warehouse?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Warehouse>(new CommandDefinition(
            $"SELECT {Sel} FROM warehouses WHERE tenant_id = @TenantId AND LOWER(code) = LOWER(@Code) LIMIT 1",
            new { TenantId = tenantId, Code = code }, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<Warehouse>> ListAsync(Guid tenantId, Guid? companyId, bool includeInactive, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {Sel} FROM warehouses WHERE tenant_id = @TenantId";
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (companyId.HasValue) { sql += " AND company_id = @CompanyId"; p.Add("CompanyId", companyId.Value); }
        if (!includeInactive) sql += " AND is_active = true";
        sql += " ORDER BY code";
        var rows = await conn.QueryAsync<Warehouse>(new CommandDefinition(sql, p, cancellationToken: ct));
        return rows.AsList();
    }
    public async Task<IReadOnlyList<Warehouse>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return new List<Warehouse>();
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var rows = await conn.QueryAsync<Warehouse>(new CommandDefinition(
            $"SELECT {Sel} FROM warehouses WHERE id = ANY(@Ids)",
            new { Ids = idList.ToArray() }, cancellationToken: ct));
        return rows.AsList();
    }
    public async Task InsertAsync(Warehouse w, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO warehouses (id, tenant_id, company_id, code, name, location, manager_user_id, is_active, created_at, created_by, updated_at, updated_by)
            VALUES (@Id, @TenantId, @CompanyId, @Code, @Name, @Location, @ManagerUserId, @IsActive, @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy)",
            w, cancellationToken: ct));
    }
    public async Task UpdateAsync(Warehouse w, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE warehouses SET name = @Name, location = @Location, manager_user_id = @ManagerUserId,
                                 is_active = @IsActive, updated_at = @UpdatedAt, updated_by = @UpdatedBy
            WHERE id = @Id", w, cancellationToken: ct));
    }
}
