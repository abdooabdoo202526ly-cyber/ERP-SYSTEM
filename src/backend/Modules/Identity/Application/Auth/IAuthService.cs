namespace ERPSystem.Modules.Identity.Application.Auth;

public interface ITenantBootstrap { Task<Guid> OnTenantCreatedAsync(Guid tenantId, string tenantName, string baseCurrency, CancellationToken ct); }

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest req, string? ip, CancellationToken ct);
    Task<AuthResult> LoginAsync(LoginRequest req, string? ip, CancellationToken ct);
    Task<AuthResult> RefreshAsync(RefreshTokenRequest req, string? ip, CancellationToken ct);
    Task RevokeAsync(Guid userId, string refreshToken, string? ip, CancellationToken ct);
}

public sealed class AuthResult { public bool Succeeded { get; init; } public AuthResponse? Response { get; init; } public string? Error { get; init; } public AuthErrorCode? ErrorCode { get; init; } public static AuthResult Ok(AuthResponse r) => new() { Succeeded = true, Response = r }; public static AuthResult Fail(string e, AuthErrorCode c) => new() { Succeeded = false, Error = e, ErrorCode = c }; }
public enum AuthErrorCode { InvalidCredentials, UserAlreadyExists, TenantNotFound, TenantInactive, UserInactive, InvalidRefreshToken, RefreshTokenExpired, RefreshTokenRevoked, ValidationError, InternalError }
