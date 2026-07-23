using System;

namespace ERPSystem.Modules.Inventory.Entities;

/// <summary>
/// وحدة قياس (UoM) — pcs, kg, m, m², m³, liter, إلخ.
/// </summary>
public class UnitOfMeasure
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;    // "kg", "pcs"
    public string Name { get; set; } = string.Empty;    // "كيلوغرام", "قطعة"
    public string? Symbol { get; set; }                 // اختياري: "kg", "قط"
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }             // v1.0.10: for UpdateAsync
}
