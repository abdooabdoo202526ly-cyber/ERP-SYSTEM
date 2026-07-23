using System;

namespace ERPSystem.Modules.Companies.Entities;

public class Company
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public Guid? ParentCompanyId { get; set; }
    public bool IsGroup { get; set; } = false;
    public string BaseCurrency { get; set; } = "LYD";
    // v1.0.10: contact details
    public string? TaxId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Company? Parent { get; set; }
}
