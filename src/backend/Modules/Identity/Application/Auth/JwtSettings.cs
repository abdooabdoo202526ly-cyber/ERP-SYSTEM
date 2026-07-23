namespace ERPSystem.Modules.Identity.Application.Auth;

/// <summary>إعدادات JWT — تُقرأ من appsettings.json (JwtSettings section)</summary>
public sealed class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "ERP-SYSTEM";
    public string Audience { get; set; } = "ERP-SYSTEM-Users";
    public int AccessTokenExpiryMinutes { get; set; } = 60;
    public int RefreshTokenExpiryDays { get; set; } = 14;
}
