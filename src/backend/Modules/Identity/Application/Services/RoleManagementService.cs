// v1.0.32: Role Management Service — قائمة roles + assign/remove users + permissions

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using ERPSystem.Modules.Identity.Entities;
using ERPSystem.Modules.Identity.Infrastructure;
using ERPSystem.Shared.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Modules.Identity.Application.Services;

public interface IRoleManagementService
{
    Task<List<RoleDto>> ListRolesAsync(Guid tenantId, CancellationToken ct);
    Task<RoleDto> GetRoleAsync(Guid id, CancellationToken ct);
    Task<RoleDto> CreateRoleAsync(Guid tenantId, string name, string description, CancellationToken ct);
    Task<RoleDto> UpdateRoleAsync(Guid id, string name, string description, CancellationToken ct);
    Task DeleteRoleAsync(Guid id, CancellationToken ct);
    Task<List<UserRoleDto>> ListUserRolesAsync(Guid userId, CancellationToken ct);
    Task AssignRoleAsync(Guid userId, Guid roleId, CancellationToken ct);
    Task RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken ct);
    Task<List<PermissionDto>> ListAvailablePermissionsAsync(CancellationToken ct);
    Task<List<UserListItem>> ListUsersAsync(Guid tenantId, CancellationToken ct);
}

public record RoleDto(Guid Id, Guid TenantId, string Name, string Description, int UserCount, DateTime CreatedAt);
public record UserRoleDto(Guid UserId, string UserName, string UserEmail, Guid RoleId, string RoleName, DateTime AssignedAt);
public record PermissionDto(string Code, string Category, string NameAr, string NameEn);
public record UserListItem(Guid Id, string Email, string FullName, bool IsActive, DateTime? LastLoginAt, DateTime CreatedAt, List<string> Roles);

public sealed class RoleManagementService : IRoleManagementService
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<RoleManagementService> _logger;

    // مجموعة الـ permissions المتاحة في النظام (معرّفة كـ constants)
    public static readonly List<PermissionDto> AllPermissions = new()
    {
        // Finance
        new("finance.view", "Finance", "عرض المالية", "View Finance"),
        new("finance.post", "Finance", "ترحيل القيود", "Post Entries"),
        new("finance.reports", "Finance", "تقارير المالية", "Finance Reports"),
        // Sales
        new("sales.view", "Sales", "عرض المبيعات", "View Sales"),
        new("sales.create", "Sales", "إنشاء فاتورة", "Create Invoice"),
        new("sales.post", "Sales", "ترحيل فاتورة", "Post Invoice"),
        new("sales.delete", "Sales", "حذف فاتورة", "Delete Invoice"),
        // Procurement
        new("procurement.view", "Procurement", "عرض المشتريات", "View Procurement"),
        new("procurement.approve", "Procurement", "اعتماد طلب شراء", "Approve PO"),
        new("procurement.delete", "Procurement", "حذف طلب شراء", "Delete PO"),
        // Inventory
        new("inventory.view", "Inventory", "عرض المخزون", "View Inventory"),
        new("inventory.adjust", "Inventory", "تعديل المخزون", "Adjust Stock"),
        new("inventory.transfer", "Inventory", "تحويل بين المستودعات", "Transfer Stock"),
        // HR
        new("hr.view", "HR", "عرض HR", "View HR"),
        new("hr.manage_employees", "HR", "إدارة الموظفين", "Manage Employees"),
        new("hr.payroll", "HR", "الرواتب", "Run Payroll"),
        new("hr.approve_leave", "HR", "اعتماد إجازة", "Approve Leave"),
        // Admin
        new("admin.users", "Admin", "إدارة المستخدمين", "Manage Users"),
        new("admin.roles", "Admin", "إدارة الأدوار", "Manage Roles"),
        new("admin.settings", "Admin", "إعدادات النظام", "System Settings"),
        new("admin.audit", "Admin", "سجل التدقيق", "Audit Log"),
    };

    public RoleManagementService(IDbConnectionFactory db, ILogger<RoleManagementService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<RoleDto>> ListRolesAsync(Guid tenantId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            SELECT r.id, r.tenant_id, r.name, r.description, r.created_at,
                   (SELECT COUNT(*) FROM user_roles ur WHERE ur.role_id = r.id) AS user_count
            FROM roles r
            WHERE r.tenant_id = @TenantId
            ORDER BY r.name";
        var rows = await conn.QueryAsync<(Guid Id, Guid TenantId, string Name, string Description, DateTime CreatedAt, int UserCount)>(
            sql, new { TenantId = tenantId });
        return rows.Select(r => new RoleDto(r.Id, r.TenantId, r.Name, r.Description, r.UserCount, r.CreatedAt)).ToList();
    }

    public async Task<RoleDto> GetRoleAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            SELECT r.id, r.tenant_id, r.name, r.description, r.created_at,
                   (SELECT COUNT(*) FROM user_roles ur WHERE ur.role_id = r.id) AS user_count
            FROM roles r WHERE r.id = @Id";
        var r = await conn.QuerySingleOrDefaultAsync<(Guid Id, Guid TenantId, string Name, string Description, DateTime CreatedAt, int UserCount)>(
            sql, new { Id = id });
        if (r.Id == Guid.Empty) throw new KeyNotFoundException($"Role {id} not found");
        return new RoleDto(r.Id, r.TenantId, r.Name, r.Description, r.UserCount, r.CreatedAt);
    }

    public async Task<RoleDto> CreateRoleAsync(Guid tenantId, string name, string description, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Role name is required");
        var role = new Role { Id = Guid.NewGuid(), TenantId = tenantId, Name = name.Trim(), Description = description?.Trim() ?? "", CreatedAt = DateTime.UtcNow };
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            INSERT INTO roles (id, tenant_id, name, description, created_at)
            VALUES (@Id, @TenantId, @Name, @Description, @CreatedAt)";
        await conn.ExecuteAsync(sql, new { role.Id, role.TenantId, role.Name, role.Description, role.CreatedAt });
        _logger.LogInformation("Role {Name} created for tenant {TenantId}", role.Name, tenantId);
        return new RoleDto(role.Id, role.TenantId, role.Name, role.Description, 0, role.CreatedAt);
    }

    public async Task<RoleDto> UpdateRoleAsync(Guid id, string name, string description, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"UPDATE roles SET name = @Name, description = @Description WHERE id = @Id";
        var affected = await conn.ExecuteAsync(sql, new { Id = id, Name = name.Trim(), Description = description?.Trim() ?? "" });
        if (affected == 0) throw new KeyNotFoundException($"Role {id} not found");
        return await GetRoleAsync(id, ct);
    }

    public async Task DeleteRoleAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        // Unassign all users first
        await conn.ExecuteAsync("DELETE FROM user_roles WHERE role_id = @Id", new { Id = id });
        const string sql = "DELETE FROM roles WHERE id = @Id";
        var affected = await conn.ExecuteAsync(sql, new { Id = id });
        if (affected == 0) throw new KeyNotFoundException($"Role {id} not found");
        _logger.LogInformation("Role {Id} deleted", id);
    }

    public async Task<List<UserRoleDto>> ListUserRolesAsync(Guid userId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            SELECT u.id AS UserId, u.user_name AS UserName, u.email AS UserEmail,
                   r.id AS RoleId, r.name AS RoleName, ur.assigned_at AS AssignedAt
            FROM user_roles ur
            JOIN users u ON u.id = ur.user_id
            JOIN roles r ON r.id = ur.role_id
            WHERE u.id = @UserId
            ORDER BY r.name";
        var rows = await conn.QueryAsync<UserRoleDto>(sql, new { UserId = userId });
        return rows.ToList();
    }

    public async Task AssignRoleAsync(Guid userId, Guid roleId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            INSERT INTO user_roles (user_id, role_id, assigned_at)
            VALUES (@UserId, @RoleId, @AssignedAt)
            ON CONFLICT (user_id, role_id) DO NOTHING";
        await conn.ExecuteAsync(sql, new { UserId = userId, RoleId = roleId, AssignedAt = DateTime.UtcNow });
        _logger.LogInformation("User {UserId} assigned to role {RoleId}", userId, roleId);
    }

    public async Task RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(
            "DELETE FROM user_roles WHERE user_id = @UserId AND role_id = @RoleId",
            new { UserId = userId, RoleId = roleId });
        _logger.LogInformation("User {UserId} removed from role {RoleId}", userId, roleId);
    }

    public Task<List<PermissionDto>> ListAvailablePermissionsAsync(CancellationToken ct)
        => Task.FromResult(AllPermissions);

    public async Task<List<UserListItem>> ListUsersAsync(Guid tenantId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string usersSql = @"
            SELECT id, email, full_name AS FullName, is_active AS IsActive,
                   last_login_at AS LastLoginAt, created_at AS CreatedAt
            FROM users WHERE tenant_id = @TenantId ORDER BY full_name";
        var users = (await conn.QueryAsync<(Guid Id, string Email, string FullName, bool IsActive, DateTime? LastLoginAt, DateTime CreatedAt)>(
            new CommandDefinition(usersSql, new { TenantId = tenantId }, cancellationToken: ct))).AsList();

        // اجلب أدوار كل المستخدمين في استعلام واحد
        const string rolesSql = @"
            SELECT ur.user_id AS UserId, r.name AS RoleName
            FROM user_roles ur JOIN roles r ON r.id = ur.role_id
            WHERE ur.user_id = ANY(@UserIds)";
        var ids = users.Select(u => u.Id).ToArray();
        var userRoles = ids.Length == 0
            ? new List<(Guid UserId, string RoleName)>()
            : (await conn.QueryAsync<(Guid UserId, string RoleName)>(
                new CommandDefinition(rolesSql, new { UserIds = ids }, cancellationToken: ct))).AsList();

        return users.Select(u => new UserListItem(
            u.Id, u.Email, u.FullName, u.IsActive, u.LastLoginAt, u.CreatedAt,
            userRoles.Where(ur => ur.UserId == u.Id).Select(ur => ur.RoleName).ToList()
        )).ToList();
    }
}
