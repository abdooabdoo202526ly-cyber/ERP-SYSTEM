using System.Security.Claims;
using System.Security.Cryptography;
using Dapper;
using ERPSystem.Modules.Identity.Application.Auth;
using ERPSystem.Modules.Identity.Infrastructure;
using ERPSystem.Shared.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RefreshTokenRequest> _refreshValidator;
    private readonly IUserRepository _users;
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<RefreshTokenRequest> refreshValidator,
        IUserRepository users,
        IDbConnectionFactory db,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _refreshValidator = refreshValidator;
        _users = users;
        _db = db;
        _logger = logger;
    }

    /// <summary>تسجيل مستخدم جديد (داخل tenant موجود أو إنشاء tenant جديد)</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var validation = await _registerValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Status = StatusCodes.Status400BadRequest,
                Detail = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))
            });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.RegisterAsync(request, ip, ct);

        if (!result.Succeeded)
        {
            _logger.LogWarning("فشل تسجيل المستخدم {Email}: {Error}", request.Email, result.Error);
            return BadRequest(new ProblemDetails
            {
                Title = "Registration Failed",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.Error,
            });
        }

        return Ok(result.Response);
    }

    /// <summary>تسجيل دخول</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var validation = await _loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Status = StatusCodes.Status400BadRequest,
                Detail = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))
            });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.LoginAsync(request, ip, ct);

        if (!result.Succeeded)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Authentication Failed",
                Status = StatusCodes.Status401Unauthorized,
                Detail = result.Error,
            });
        }

        return Ok(result.Response);
    }

    /// <summary>تجديد Access Token</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var validation = await _refreshValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Status = StatusCodes.Status400BadRequest,
                Detail = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))
            });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.RefreshAsync(request, ip, ct);

        if (!result.Succeeded)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Refresh Failed",
                Status = StatusCodes.Status401Unauthorized,
                Detail = result.Error,
            });
        }

        return Ok(result.Response);
    }

    /// <summary>إلغاء Refresh Token (logout)</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _authService.RevokeAsync(userId, request.RefreshToken, ip, ct);
        }
        return NoContent();
    }

    /// <summary>معلومات المستخدم الحالي (للـ client)</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserInfo), StatusCodes.Status200OK)]
    public IActionResult Me()
    {
        return Ok(new UserInfo
        {
            Id = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst("sub")!.Value),
            TenantId = Guid.Parse(User.FindFirst("tenant_id")!.Value),
            Email = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value ?? string.Empty,
            FullName = User.FindFirst("full_name")?.Value ?? string.Empty,
            Roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
        });
    }

    // ============ DL 70: Password Reset ============

    /// <summary>طلب رابط إعادة تعيين كلمة المرور (يُرسل بالبريد الإلكتروني في الإنتاج، يُعرض للـ demo هنا).</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new ProblemDetails { Title = "Validation Error", Detail = "البريد الإلكتروني مطلوب." });
        }

        // للأمان: نُرجع 200 دائماً حتى لو البريد غير موجود (لا نكشف المستخدمين)
        var user = await _users.GetByEmailAsync(request.Email, ct);
        if (user == null)
        {
            _logger.LogInformation("Password reset requested for non-existent email: {Email}", request.Email);
            return Ok(new { message = "إذا كان البريد موجوداً، فستصلك رسالة إعادة التعيين." });
        }

        // Generate secure token (32 bytes random, base64)
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(token, workFactor: 10);
        var expiresAt = DateTime.UtcNow.AddHours(1);

        // Store in DB
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(@"
            INSERT INTO password_reset_tokens (id, tenant_id, user_id, token_hash, expires_at, created_at)
            VALUES (@Id, @TenantId, @UserId, @TokenHash, @ExpiresAt, NOW())",
            new
            {
                Id = Guid.NewGuid(),
                TenantId = user.TenantId,
                UserId = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt,
            });

        // في الإنتاج: نُرسل البريد. للـ demo: نُرجع الـ token في الـ response
        _logger.LogInformation("Password reset token generated for {Email}: {Token}", request.Email, token);

        return Ok(new
        {
            message = "تم إنشاء رمز إعادة التعيين.",
            // للـ demo فقط — في الإنتاج يُحذف ويُرسل بالبريد
            devToken = token,
            resetUrl = $"/login/reset/{token}",
            expiresAt = expiresAt,
        });
    }

    /// <summary>تأكيد إعادة التعيين وتحديث كلمة المرور.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new ProblemDetails { Title = "Validation Error", Detail = "الرمز وكلمة المرور الجديدة مطلوبان." });
        }

        if (request.NewPassword.Length < 8)
        {
            return BadRequest(new ProblemDetails { Title = "Weak Password", Detail = "كلمة المرور يجب أن تكون 8 أحرف على الأقل." });
        }

        using var conn = await _db.CreateOltpConnectionAsync(ct);
        // ابحث عن كل الـ tokens النشطة (للمقارنة مع BCrypt)
        var tokens = (await conn.QueryAsync<dynamic>(@"
            SELECT id, user_id, tenant_id, token_hash, expires_at, used_at
            FROM password_reset_tokens
            WHERE expires_at > NOW() AND used_at IS NULL
            ORDER BY created_at DESC LIMIT 20"
        )).AsList();

        Guid? matchedId = null;
        Guid? matchedUserId = null;
        foreach (var t in tokens)
        {
            if (BCrypt.Net.BCrypt.Verify(request.Token, (string)t.token_hash))
            {
                matchedId = (Guid)t.id;
                matchedUserId = (Guid)t.user_id;
                break;
            }
        }

        if (matchedId == null)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid Token", Detail = "رمز إعادة التعيين غير صالح أو منتهي الصلاحية." });
        }

        // حدّث كلمة المرور
        var newHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 11);
        await conn.ExecuteAsync(
            "UPDATE users SET password_hash = @Hash, updated_at = NOW() WHERE id = @UserId",
            new { Hash = newHash, UserId = matchedUserId.Value });

        // ضع علامة "مستخدم" على الـ token
        await conn.ExecuteAsync(
            "UPDATE password_reset_tokens SET used_at = NOW() WHERE id = @Id",
            new { Id = matchedId.Value });

        _logger.LogInformation("Password reset completed for user {UserId}", matchedUserId);
        return Ok(new { message = "تم تحديث كلمة المرور بنجاح." });
    }
}

public sealed class ForgotPasswordRequest { public string Email { get; set; } = string.Empty; }
public sealed class ResetPasswordRequest { public string Token { get; set; } = string.Empty; public string NewPassword { get; set; } = string.Empty; }