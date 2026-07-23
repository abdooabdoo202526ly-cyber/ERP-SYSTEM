using System;

namespace ERPSystem.Modules.Notifications.Entities;

/// <summary>
/// In-app notification (in-DB, not push/email).
/// PR #6: LowStock alerts created on StockMovement.PostAsync.
/// Future: PR #7+ may add JournalPosted, HighVariance, etc.
/// </summary>
public class Notification
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }                  // target user (broad: tenant-wide if userId = tenant.Admin)
    public string Type { get; set; } = string.Empty; // "LowStock", "JournalPosted", "HighVariance", etc.
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }       // "Item", "Project", "JournalEntry"
    public Guid? ReferenceId { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
