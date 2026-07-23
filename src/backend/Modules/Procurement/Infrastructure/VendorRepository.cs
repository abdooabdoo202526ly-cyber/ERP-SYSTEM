using Dapper;
using ERPSystem.Modules.Procurement.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Procurement.Infrastructure;

/// <summary>تنفيذ IVendorRepository عبر Dapper.</summary>
public sealed class VendorRepository : IVendorRepository
{
    private readonly IDbConnectionFactory _db;
    public VendorRepository(IDbConnectionFactory db) => _db = db;

    private const string Sel = @"id, tenant_id AS TenantId, code, name, email, phone, address, tax_number AS TaxNumber,
        website,  -- DEC-081b (added via JSON migration DEC-081a)
        currency, payment_terms AS PaymentTerms, is_active AS IsActive,
        created_at AS CreatedAt, created_by AS CreatedBy, updated_at AS UpdatedAt, updated_by AS UpdatedBy";

    public async Task<Vendor?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Vendor>(new CommandDefinition(
            $"SELECT {Sel} FROM vendors WHERE id = @Id LIMIT 1",
            new { Id = id }, cancellationToken: ct));
    }

    public async Task<Vendor?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Vendor>(new CommandDefinition(
            $"SELECT {Sel} FROM vendors WHERE tenant_id = @TenantId AND LOWER(code) = LOWER(@Code) LIMIT 1",
            new { TenantId = tenantId, Code = code }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Vendor>> ListAsync(Guid tenantId, bool includeInactive, int skip, int take, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {Sel} FROM vendors WHERE tenant_id = @TenantId";
        if (!includeInactive) sql += " AND is_active = true";
        sql += " ORDER BY code OFFSET @Skip LIMIT @Take";
        var rows = await conn.QueryAsync<Vendor>(new CommandDefinition(sql,
            new { TenantId = tenantId, Skip = skip, Take = take }, cancellationToken: ct));
        return rows.AsList();
    }
    public async Task<IReadOnlyList<Vendor>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return new List<Vendor>();
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var rows = await conn.QueryAsync<Vendor>(new CommandDefinition(
            $"SELECT {Sel} FROM vendors WHERE id = ANY(@Ids)",
            new { Ids = idList.ToArray() }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertAsync(Vendor v, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO vendors (id, tenant_id, code, name, email, phone, address, tax_number, website,
                                 currency, payment_terms, is_active,
                                 created_at, created_by, updated_at, updated_by)
            VALUES (@Id, @TenantId, @Code, @Name, @Email, @Phone, @Address, @TaxNumber, @Website,
                    @Currency, @PaymentTerms, @IsActive,
                    @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy)",
            v, cancellationToken: ct));
    }

    public async Task UpdateAsync(Vendor v, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE vendors SET name = @Name, email = @Email, phone = @Phone, address = @Address,
                              tax_number = @TaxNumber, website = @Website,
                              currency = @Currency, payment_terms = @PaymentTerms,
                              is_active = @IsActive, updated_at = @UpdatedAt, updated_by = @UpdatedBy
            WHERE id = @Id", v, cancellationToken: ct));
    }
}
