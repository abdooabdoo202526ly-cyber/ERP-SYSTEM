using System;

namespace ERPSystem.Modules.Projects.Entities;

public enum ResourceType
{
    Labor = 1,
    Equipment = 2,
    Material = 3,
    Service = 4
}

/// <summary>
/// مورد (عامل / معدة / مادة / خدمة) — له تكلفة بالساعة لاستخدامها في ResourceAssignment
/// </summary>
public class Resource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ResourceType Type { get; set; } = ResourceType.Labor;
    public decimal HourlyRate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
