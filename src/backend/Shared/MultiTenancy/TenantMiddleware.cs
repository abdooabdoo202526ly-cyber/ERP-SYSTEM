using System.Security.Claims;

namespace ERPSystem.Shared.MultiTenancy;

/// <summary>
/// Middleware يلتقط TenantId و UserId من JWT Claims ويُعبّئ الـ TenantContext.
/// لازم يأتي بعد UseAuthentication() و UseAuthorization() في الـ pipeline.
/// </summary>
public sealed class TenantMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantMiddleware> _logger;

    public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        // نقاط النهاية العامة (health, swagger, auth/register, auth/login) لا تحتاج tenant
        var path = context.Request.Path.Value ?? string.Empty;
        var isPublic = IsPublicPath(path);

        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
            var userClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? context.User.FindFirst("sub")?.Value;

            if (Guid.TryParse(tenantClaim, out var tenantId) &&
                Guid.TryParse(userClaim, out var userId))
            {
                tenantContext.Set(tenantId, userId);
                _logger.LogDebug("Tenant resolved: {TenantId}, User: {UserId}", tenantId, userId);
            }
            else if (!isPublic)
            {
                _logger.LogWarning("Authenticated request without tenant_id claim on path {Path}", path);
            }
        }

        try
        {
            await _next(context);
        }
        finally
        {
            tenantContext.Clear();
        }
    }

    private static bool IsPublicPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return true;
        return path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth/register", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth/refresh", StringComparison.OrdinalIgnoreCase);
    }
}
