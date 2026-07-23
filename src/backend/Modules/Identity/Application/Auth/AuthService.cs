using System.Security.Claims;
using ERPSystem.Modules.Identity.Entities;
using ERPSystem.Modules.Identity.Infrastructure;

namespace ERPSystem.Modules.Identity.Application.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly ITenantRepository _tenants;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IJwtTokenService _jwt;
    private readonly ITenantBootstrap _bootstrap;
    private readonly ILogger<AuthService> _logger;
    public AuthService(IUserRepository u, IRoleRepository r, ITenantRepository t, IRefreshTokenRepository rt, IJwtTokenService j, ITenantBootstrap b, ILogger<AuthService> l)
    { _users = u; _roles = r; _tenants = t; _refreshTokens = rt; _jwt = j; _bootstrap = b; _logger = l; }

    public async Task<AuthResult> RegisterAsync(RegisterRequest req, string? ip, CancellationToken ct)
    {
        Tenant tenant;
        var isNewTenant = false;
        Guid holdingId;
        if (req.TenantId != Guid.Empty)
        {
            tenant = (await _tenants.GetByIdAsync(req.TenantId, ct))!;
            if (tenant == null) return AuthResult.Fail("المستأجر غير موجود.", AuthErrorCode.TenantNotFound);
            if (!tenant.IsActive) return AuthResult.Fail("المستأجر موقوف.", AuthErrorCode.TenantInactive);
            holdingId = await _bootstrap.OnTenantCreatedAsync(tenant.Id, tenant.Name, "LYD", ct);
        }
        else
        {
            tenant = new Tenant { Id = Guid.NewGuid(), Name = req.TenantName!, Subdomain = Slugify(req.TenantName!), IsActive = true, CreatedAt = DateTime.UtcNow };
            await _tenants.InsertAsync(tenant, ct);
            isNewTenant = true;
            holdingId = await _bootstrap.OnTenantCreatedAsync(tenant.Id, tenant.Name, req.BaseCurrency, ct);
        }
        if (await _users.GetByEmailAndTenantAsync(req.Email, tenant.Id, ct) != null)
            return AuthResult.Fail("البريد مستخدم.", AuthErrorCode.UserAlreadyExists);
        var now = DateTime.UtcNow;
        var user = new User { Id = Guid.NewGuid(), TenantId = tenant.Id, Email = req.Email.Trim().ToLowerInvariant(), PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, 12), FullName = req.FullName.Trim(), IsActive = true, CreatedAt = now, UpdatedAt = now };
        await _users.InsertAsync(user, ct);
        await _roles.EnsureDefaultRolesAsync(tenant.Id, ct);
        if (isNewTenant)
        {
            var admin = await _roles.GetByNameAsync(tenant.Id, "Admin", ct);
            if (admin != null) await _users.AssignRoleAsync(user.Id, admin.Id, ct);
        }
        var roles = await _users.GetRoleNamesAsync(user.Id, ct);
        return AuthResult.Ok(await BuildAsync(user, roles, holdingId, ip, ct));
    }

    public async Task<AuthResult> LoginAsync(LoginRequest req, string? ip, CancellationToken ct)
    {
        User? user = null;
        if (req.TenantId.HasValue && req.TenantId.Value != Guid.Empty)
        {
            var tenant = await _tenants.GetByIdAsync(req.TenantId.Value, ct);
            if (tenant == null) return AuthResult.Fail("المستأجر غير موجود.", AuthErrorCode.TenantNotFound);
            if (!tenant.IsActive) return AuthResult.Fail("المستأجر موقوف.", AuthErrorCode.TenantInactive);
            user = await _users.GetByEmailAndTenantAsync(req.Email, req.TenantId.Value, ct);
        }
        else user = await _users.GetByEmailAsync(req.Email, ct);
        if (user == null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return AuthResult.Fail("بيانات الدخول غير صحيحة.", AuthErrorCode.InvalidCredentials);
        var now = DateTime.UtcNow;
        await _users.UpdateLastLoginAsync(user.Id, now, ct);
        var holdingId = await _bootstrap.OnTenantCreatedAsync(user.TenantId, "", "LYD", ct);
        var roles = await _users.GetRoleNamesAsync(user.Id, ct);
        return AuthResult.Ok(await BuildAsync(user, roles, holdingId, ip, ct));
    }

    public async Task<AuthResult> RefreshAsync(RefreshTokenRequest req, string? ip, CancellationToken ct)
    {
        var principal = _jwt.GetPrincipalFromExpiredToken(req.AccessToken);
        if (principal == null) return AuthResult.Fail("Access Token غير صالح.", AuthErrorCode.InvalidRefreshToken);
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return AuthResult.Fail("بيانات التوكن غير مكتملة.", AuthErrorCode.InvalidRefreshToken);
        var stored = await _refreshTokens.GetByHashAsync(_jwt.HashRefreshToken(req.RefreshToken), ct);
        if (stored == null || stored.UserId != userId) return AuthResult.Fail("Refresh Token غير صالح.", AuthErrorCode.InvalidRefreshToken);
        if (stored.IsRevoked) { await _refreshTokens.RevokeAllForUserAsync(userId, "Reuse of revoked", ip, ct); return AuthResult.Fail("تم اكتشاف محاولة اختراق.", AuthErrorCode.RefreshTokenRevoked); }
        if (stored.IsExpired) return AuthResult.Fail("Refresh Token منتهي.", AuthErrorCode.RefreshTokenExpired);
        var user = await _users.GetByIdAsync(userId, ct);
        if (user == null || !user.IsActive) return AuthResult.Fail("المستخدم غير مفعّل.", AuthErrorCode.UserInactive);
        var (newRt, newRtHash, newRtExp) = _jwt.GenerateRefreshToken();
        await _refreshTokens.RevokeAsync(stored, "Rotated", newRtHash, ip, ct);
        var (at, atExp) = _jwt.GenerateAccessToken(user, await _users.GetRoleNamesAsync(user.Id, ct));
        await _refreshTokens.InsertAsync(new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id, TokenHash = newRtHash, ExpiresAt = newRtExp, CreatedAt = DateTime.UtcNow, CreatedByIp = ip }, ct);
        var holdingId = await _bootstrap.OnTenantCreatedAsync(user.TenantId, "", "LYD", ct);
        return AuthResult.Ok(new AuthResponse { AccessToken = at, RefreshToken = newRt, AccessTokenExpiresAt = atExp, RefreshTokenExpiresAt = newRtExp, User = new UserInfo { Id = user.Id, TenantId = user.TenantId, Email = user.Email, FullName = user.FullName, Roles = await _users.GetRoleNamesAsync(user.Id, ct) }, HoldingCompanyId = holdingId });
    }

    public async Task RevokeAsync(Guid userId, string refreshToken, string? ip, CancellationToken ct)
    {
        var stored = await _refreshTokens.GetByHashAsync(_jwt.HashRefreshToken(refreshToken), ct);
        if (stored != null && stored.UserId == userId && stored.IsActive)
            await _refreshTokens.RevokeAsync(stored, "User logout", null, ip, ct);
    }

    private async Task<AuthResponse> BuildAsync(User user, IReadOnlyList<string> roles, Guid holdingId, string? ip, CancellationToken ct)
    {
        var (at, atExp) = _jwt.GenerateAccessToken(user, roles);
        var (rt, rtHash, rtExp) = _jwt.GenerateRefreshToken();
        await _refreshTokens.InsertAsync(new RefreshToken { Id = Guid.NewGuid(), UserId = user.Id, TokenHash = rtHash, ExpiresAt = rtExp, CreatedAt = DateTime.UtcNow, CreatedByIp = ip }, ct);
        return new AuthResponse { AccessToken = at, RefreshToken = rt, AccessTokenExpiresAt = atExp, RefreshTokenExpiresAt = rtExp, User = new UserInfo { Id = user.Id, TenantId = user.TenantId, Email = user.Email, FullName = user.FullName, Roles = roles }, HoldingCompanyId = holdingId };
    }

    private static string Slugify(string s) { var arr = s.ToLowerInvariant().ToCharArray(); for (var i = 0; i < arr.Length; i++) if (!char.IsLetterOrDigit(arr[i])) arr[i] = '-'; return new string(arr).Trim('-'); }
}
