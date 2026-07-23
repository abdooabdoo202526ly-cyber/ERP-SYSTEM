using ERPSystem.Modules.Identity.Entities;

namespace ERPSystem.Modules.Identity.Infrastructure;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken ct);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct);
    Task InsertAsync(User user, CancellationToken ct);
    Task UpdateLastLoginAsync(Guid userId, DateTime at, CancellationToken ct);
    Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken ct);
    Task AssignRoleAsync(Guid userId, Guid roleId, CancellationToken ct);
    Task<List<User>> ListByTenantAsync(Guid tenantId, CancellationToken ct);
}

public interface IRoleRepository
{
    Task<Role?> GetByNameAsync(Guid tenantId, string name, CancellationToken ct);
    Task<Role?> GetByIdAsync(Guid id, CancellationToken ct);
    Task InsertAsync(Role role, CancellationToken ct);
    Task EnsureDefaultRolesAsync(Guid tenantId, CancellationToken ct);
}

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);
    Task InsertAsync(Tenant tenant, CancellationToken ct);
}

public interface IRefreshTokenRepository
{
    Task InsertAsync(RefreshToken token, CancellationToken ct);
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct);
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, CancellationToken ct);
    Task RevokeAsync(RefreshToken token, string reason, string? replacedByHash, string? ip, CancellationToken ct);
    Task RevokeAllForUserAsync(Guid userId, string reason, string? ip, CancellationToken ct);
}
