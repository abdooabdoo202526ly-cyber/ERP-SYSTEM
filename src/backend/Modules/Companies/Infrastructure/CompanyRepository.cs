using Dapper;
using ERPSystem.Modules.Companies.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Companies.Infrastructure;

public sealed class CompanyRepository : ICompanyRepository
{
    private readonly IDbConnectionFactory _db;
    public CompanyRepository(IDbConnectionFactory db) => _db = db;

    private const string SelectColumns = @"id, tenant_id AS TenantId, code, name, legal_name AS LegalName,
        parent_company_id AS ParentCompanyId, is_group AS IsGroup,
        base_currency AS BaseCurrency, is_active AS IsActive,
        created_at AS CreatedAt, updated_at AS UpdatedAt";

    public async Task<Company?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Company>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM companies WHERE id = @Id LIMIT 1",
            new { Id = id }, cancellationToken: ct));
    }

    public async Task<Company?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Company>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM companies WHERE tenant_id = @TenantId AND LOWER(code) = LOWER(@Code) LIMIT 1",
            new { TenantId = tenantId, Code = code }, cancellationToken: ct));
    }

    public async Task<Guid?> GetHoldingCompanyIdAsync(Guid tenantId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM companies WHERE tenant_id = @TenantId AND is_group = true LIMIT 1",
            new { TenantId = tenantId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Company>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {SelectColumns} FROM companies WHERE tenant_id = @TenantId"
            + (includeInactive ? "" : " AND is_active = true") + " ORDER BY code";
        var rows = await conn.QueryAsync<Company>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<Company>> ListSubsidiariesAsync(Guid parentCompanyId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var rows = await conn.QueryAsync<Company>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM companies WHERE parent_company_id = @ParentId ORDER BY code",
            new { ParentId = parentCompanyId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertAsync(Company company, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO companies (id, tenant_id, code, name, legal_name, parent_company_id,
                                   is_group, base_currency, is_active, created_at, updated_at)
            VALUES (@Id, @TenantId, @Code, @Name, @LegalName, @ParentCompanyId,
                    @IsGroup, @BaseCurrency, @IsActive, @CreatedAt, @UpdatedAt)",
            company, cancellationToken: ct));
    }

    public async Task UpdateAsync(Company company, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE companies SET name = @Name, legal_name = @LegalName,
                                 base_currency = @BaseCurrency, is_active = @IsActive,
                                 updated_at = @UpdatedAt
            WHERE id = @Id", company, cancellationToken: ct));
    }
}
