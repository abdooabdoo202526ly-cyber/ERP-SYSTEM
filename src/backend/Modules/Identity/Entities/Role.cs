using System;
using System.Collections.Generic;

namespace ERPSystem.Modules.Identity.Entities;

/// <summary>
/// Role entity - represents a role that can be assigned to users
/// Phase 0: Foundation + Identity Module
/// </summary>
public class Role
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Tenant? Tenant { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

/// <summary>
/// Join table for User-Role many-to-many relationship
/// </summary>
public class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public DateTime AssignedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
