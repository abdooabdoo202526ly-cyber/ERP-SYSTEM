using Dapper;
using ERPSystem.Modules.Identity.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Identity.Infrastructure;

public sealed class TenantRepository : ITenantRepository
{
    private readonly IDbConnectionFactory _db;

    public TenantRepository(IDbConnectionFactory db) => _db = db;

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"SELECT id, name, subdomain, is_active AS IsActive,
                                    created_at AS CreatedAt, subscription_expires_at AS SubscriptionExpiresAt
                             FROM tenants WHERE id = @Id LIMIT 1";
        return await conn.QueryFirstOrDefaultAsync<Tenant>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"SELECT id, name, subdomain, is_active AS IsActive,
                                    created_at AS CreatedAt, subscription_expires_at AS SubscriptionExpiresAt
                             FROM tenants WHERE LOWER(subdomain) = LOWER(@Subdomain) LIMIT 1";
        return await conn.QueryFirstOrDefaultAsync<Tenant>(new CommandDefinition(sql, new { Subdomain = subdomain }, cancellationToken: ct));
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = "SELECT 1 FROM tenants WHERE id = @Id LIMIT 1";
        var hit = await conn.QueryFirstOrDefaultAsync<int?>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return hit.HasValue;
    }

    public async Task InsertAsync(Tenant tenant, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"INSERT INTO tenants (id, name, subdomain, is_active, created_at, subscription_expires_at)
                             VALUES (@Id, @Name, @Subdomain, @IsActive, @CreatedAt, @SubscriptionExpiresAt)";
        await conn.ExecuteAsync(new CommandDefinition(sql, tenant, cancellationToken: ct));
    }
}
