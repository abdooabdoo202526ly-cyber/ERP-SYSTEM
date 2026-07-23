using System.Security.Claims;
using ERPSystem.Modules.Identity.Entities;

namespace ERPSystem.Modules.Identity.Application.Auth;

/// <summary>خدمة توليد والتحقق من JWT و Refresh Tokens</summary>
public interface IJwtTokenService
{
    /// <summary>توليد Access Token قصير العمر لمستخدم وأدواره</summary>
    (string token, DateTime expiresAt) GenerateAccessToken(User user, IEnumerable<string> roles);

    /// <summary>توليد Refresh Token عشوائي آمن (256-bit)</summary>
    (string token, string tokenHash, DateTime expiresAt) GenerateRefreshToken();

    /// <summary>هاش للـ refresh token (SHA-256 base64)</summary>
    string HashRefreshToken(string token);

    /// <summary>قراءة الـ principal من access token منتهي الصلاحية (للـ refresh flow)</summary>
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
