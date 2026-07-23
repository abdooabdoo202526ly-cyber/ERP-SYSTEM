namespace ERPSystem.Modules.Identity.Application.Auth;

public sealed class RegisterRequest
{
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? TenantName { get; set; }
    public string BaseCurrency { get; set; } = "LYD";
}
public sealed class LoginRequest { public string Email { get; set; } = string.Empty; public string Password { get; set; } = string.Empty; public Guid? TenantId { get; set; } }
public sealed class RefreshTokenRequest { public string AccessToken { get; set; } = string.Empty; public string RefreshToken { get; set; } = string.Empty; }
public sealed class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public DateTime RefreshTokenExpiresAt { get; set; }
    public UserInfo User { get; set; } = null!;
    public Guid HoldingCompanyId { get; set; }
}
public sealed class UserInfo
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}
