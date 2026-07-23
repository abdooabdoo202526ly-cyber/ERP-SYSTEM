using Dapper;
using ERPSystem.Modules.AccountsReceivable.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.AccountsReceivable.Infrastructure;

/// <summary>تنفيذ ICustomerRepository عبر Dapper.</summary>
public sealed class CustomerRepository : ICustomerRepository
{
    private readonly IDbConnectionFactory _db;
    public CustomerRepository(IDbConnectionFactory db) => _db = db;

    private const string Sel = @"id, tenant_id AS TenantId, company_id AS CompanyId, code, name, name_en AS NameEn,
        tax_id AS TaxId, email, phone, address, credit_limit AS CreditLimit, payment_terms_days AS PaymentTermsDays,
        is_active AS IsActive, created_at AS CreatedAt, created_by AS CreatedBy, updated_at AS UpdatedAt, updated_by AS UpdatedBy";

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Customer>(new CommandDefinition(
            $"SELECT {Sel} FROM customers WHERE id = @Id LIMIT 1",
            new { Id = id }, cancellationToken: ct));
    }

    public async Task<Customer?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Customer>(new CommandDefinition(
            $"SELECT {Sel} FROM customers WHERE tenant_id = @TenantId AND LOWER(code) = LOWER(@Code) LIMIT 1",
            new { TenantId = tenantId, Code = code }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Customer>> ListAsync(Guid tenantId, bool includeInactive, int skip, int take, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {Sel} FROM customers WHERE tenant_id = @TenantId";
        if (!includeInactive) sql += " AND is_active = true";
        sql += " ORDER BY code OFFSET @Skip LIMIT @Take";
        var rows = await conn.QueryAsync<Customer>(new CommandDefinition(sql,
            new { TenantId = tenantId, Skip = skip, Take = take }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertAsync(Customer c, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO customers (id, tenant_id, company_id, code, name, name_en, tax_id, email, phone, address,
                                  credit_limit, payment_terms_days, is_active,
                                  created_at, created_by, updated_at, updated_by)
            VALUES (@Id, @TenantId, @CompanyId, @Code, @Name, @NameEn, @TaxId, @Email, @Phone, @Address,
                    @CreditLimit, @PaymentTermsDays, @IsActive,
                    @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy)", c, cancellationToken: ct));
    }

    public async Task UpdateAsync(Customer c, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE customers SET name = @Name, name_en = @NameEn, tax_id = @TaxId, email = @Email, phone = @Phone,
                                address = @Address, credit_limit = @CreditLimit, payment_terms_days = @PaymentTermsDays,
                                is_active = @IsActive, updated_at = @UpdatedAt, updated_by = @UpdatedBy
            WHERE id = @Id", c, cancellationToken: ct));
    }
}
