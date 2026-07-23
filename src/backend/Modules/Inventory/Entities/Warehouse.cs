using System;

namespace ERPSystem.Modules.Inventory.Entities;

/// <summary>
/// مخزن — مملوك لشركة. المخازن تنقسم حسب النوع (رئيسي، فرعي، في الموقع، إلخ).
/// </summary>
public class Warehouse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public Guid? ManagerUserId { get; set; }      // المستخدم المسؤول عن المخزن

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
