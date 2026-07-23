using System;

namespace ERPSystem.Modules.Inventory.Entities;

/// <summary>
/// تصنيف الأصناف — يدعم الهيراركية (parent_id) وأنواع متعددة.
/// </summary>
public class ItemCategory
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }       // self-FK للـ hierarchy
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
