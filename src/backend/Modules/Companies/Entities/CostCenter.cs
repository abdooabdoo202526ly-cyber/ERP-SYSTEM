using System;

namespace ERPSystem.Modules.Companies.Entities;

public enum CostCenterType { Project = 1, Department = 2, Branch = 3, ProductLine = 4, Activity = 5, Other = 99 }

public class CostCenter
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CostCenterType Type { get; set; } = CostCenterType.Other;
    public Guid? ParentId { get; set; }
    public decimal? BudgetAmount { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Sku { get; set; }
    public string? Location { get; set; }
    public string? ActivityCategory { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Company? Company { get; set; }
    public CostCenter? Parent { get; set; }
}
