using System;

namespace ERPSystem.Modules.Finance.Entities;

public enum AccountType { Asset = 1, Liability = 2, Equity = 3, Revenue = 4, Expense = 5 }
public enum NormalBalance { Debit = 1, Credit = 2 }

public class Account
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AccountType Type { get; set; }
    public NormalBalance NormalBalance { get; set; }
    public Guid? ParentAccountId { get; set; }
    public bool IsPostable { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public bool IsIntercompany { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
