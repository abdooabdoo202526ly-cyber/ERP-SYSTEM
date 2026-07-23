using System;

namespace ERPSystem.Modules.HR.Entities;

/// <summary>
/// قسم تنظيمي — يدعم hierarchy (parent_id) وحد أقصى 5 مستويات.
/// ManagerId: FK إلى Employee (nullable — ليس كل قسم له مدير).
/// </summary>
public class Department
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Guid? ManagerId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
