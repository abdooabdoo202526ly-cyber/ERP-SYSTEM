using System;

namespace ERPSystem.Modules.Identity.Entities;

/// <summary>
/// RefreshToken entity - used for JWT refresh flow
/// Phase 0: Auth flows
///
/// Token Rotation Strategy:
/// - كل refresh token له token hash مخزّن (لا نخزن النص الصريح)
/// - عند الاستعمال، يتم استبداله (rotated) ويُولّد token جديد
/// - عند الاستعمال المتكرر لنفس token → يُعتبر هجوم → نُلغي كل tokens للمستخدم
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? RevokedReason { get; set; }
    public string? CreatedByIp { get; set; }
    public string? RevokedByIp { get; set; }

    public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;

    // Navigation
    public User? User { get; set; }
}
