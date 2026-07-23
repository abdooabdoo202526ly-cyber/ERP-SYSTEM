using System;
using System.Collections.Generic;
using ERPSystem.Modules.Finance.Entities;

namespace ERPSystem.Modules.Finance.Application;

// ============== Accounts (CoA) ==============

public sealed class CreateAccountRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AccountType Type { get; set; }
    public Guid? ParentAccountId { get; set; }
    public bool IsPostable { get; set; } = true;
    // v1.0.21: accept NormalBalance from frontend; if not provided, server
    // derives it from Type (Asset/Expense -> Debit, others -> Credit)
    public NormalBalance? NormalBalance { get; set; }
}

// v1.0.10: added UpdateAccountRequest for edit support
// v1.0.24: added Type, NormalBalance, ParentAccountId (so they can be edited)
public sealed class UpdateAccountRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public AccountType? Type { get; set; }
    public NormalBalance? NormalBalance { get; set; }
    public Guid? ParentAccountId { get; set; }
    public bool? IsPostable { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class AccountResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AccountType Type { get; set; }
    public NormalBalance NormalBalance { get; set; }
    public Guid? ParentAccountId { get; set; }
    public bool IsPostable { get; set; }
    public bool IsActive { get; set; }
}

// ============== Journal Entries ==============

public sealed class PostJournalEntryRequest
{
    public DateTime EntryDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public List<PostJournalLineRequest> Lines { get; set; } = new();
}

public sealed class PostJournalLineRequest
{
    public Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }
}

public sealed class JournalEntryResponse
{
    public Guid Id { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public JournalEntryStatus Status { get; set; }
    public DateTime? PostedAt { get; set; }
    public List<JournalLineResponse> Lines { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
}

public sealed class JournalLineResponse
{
    public int LineNumber { get; set; }
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }
}

// ============== General Ledger ==============

public sealed class LedgerLineResponse
{
    public DateTime EntryDate { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public string JournalEntryId { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string Description { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}

public sealed class AccountBalanceResponse
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public NormalBalance NormalBalance { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal Balance { get; set; }
}

// ============== Posting Rules ==============

public sealed class CreatePostingRuleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TriggeringEvent EventType { get; set; }
    public bool? IsActive { get; set; } = true;
    /// <summary>v1.0.30: raw JSON template (الـ frontend يبعث كـ string). اختياري — fallback للـ Template object.</summary>
    public string? TemplateJson { get; set; }
    public PostingRuleTemplate Template { get; set; } = new();
}
