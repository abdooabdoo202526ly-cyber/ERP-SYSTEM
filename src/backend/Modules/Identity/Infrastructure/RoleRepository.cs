using Dapper;
using ERPSystem.Modules.Identity.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Identity.Infrastructure;

public sealed class RoleRepository : IRoleRepository
{
    private readonly IDbConnectionFactory _db;

    public RoleRepository(IDbConnectionFactory db) => _db = db;

    public async Task<Role?> GetByNameAsync(Guid tenantId, string name, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"SELECT id, tenant_id AS TenantId, name, description, created_at AS CreatedAt
                             FROM roles WHERE tenant_id = @TenantId AND LOWER(name) = LOWER(@Name) LIMIT 1";
        return await conn.QueryFirstOrDefaultAsync<Role>(new CommandDefinition(sql, new { TenantId = tenantId, Name = name }, cancellationToken: ct));
    }

    public async Task<Role?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"SELECT id, tenant_id AS TenantId, name, description, created_at AS CreatedAt
                             FROM roles WHERE id = @Id LIMIT 1";
        return await conn.QueryFirstOrDefaultAsync<Role>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task InsertAsync(Role role, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"INSERT INTO roles (id, tenant_id, name, description, created_at)
                             VALUES (@Id, @TenantId, @Name, @Description, @CreatedAt)";
        await conn.ExecuteAsync(new CommandDefinition(sql, role, cancellationToken: ct));
    }

    public async Task EnsureDefaultRolesAsync(Guid tenantId, CancellationToken ct)
    {
        // الأدوار الافتراضية لأي مستأجر جديد
        var defaults = new (string Name, string Description)[]
        {
            ("Admin", "مدير النظام — صلاحيات كاملة داخل المستأجر"),
            ("Accountant", "محاسب — يدير القيود والفواتير"),
            ("ProjectManager", "مدير مشاريع"),
            ("Viewer", "صلاحيات قراءة فقط"),
        };

        foreach (var (name, desc) in defaults)
        {
            var existing = await GetByNameAsync(tenantId, name, ct);
            if (existing == null)
            {
                await InsertAsync(new Role
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = name,
                    Description = desc,
                    CreatedAt = DateTime.UtcNow
                }, ct);
            }
        }
    }
}
