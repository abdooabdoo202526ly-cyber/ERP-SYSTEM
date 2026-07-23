using System;

namespace ERPSystem.Modules.Identity.Entities;

/// <summary>
/// Tenant entity - represents an isolated organization in a multi-tenant system
/// Phase 0: Foundation + Identity Module
/// </summary>
public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? SubscriptionExpiresAt { get; set; }
}
