using System;
using System.Collections.Generic;

namespace ERPSystem.Modules.Finance.Application;

// ============== General Ledger (per-account) ==============

public sealed class GeneralLedgerLineResponse
{
    public DateTime EntryDate { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public Guid JournalEntryId { get; set; }
    public string? Reference { get; set; }
    public string EntryDescription { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    /// <summary>الرصيد الجاري بحسب NormalBalance للحساب (Dr → +debit-credit، Cr → +credit-debit).</summary>
    public decimal RunningBalance { get; set; }
}

public sealed class GeneralLedgerReportResponse
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountTypeName { get; set; } = string.Empty;
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }
    public List<GeneralLedgerLineResponse> Lines { get; set; } = new();
}

// ============== Balance Sheet ==============

public sealed class BalanceSheetSection
{
    public string Title { get; set; } = string.Empty;
    public List<BalanceSheetRow> Rows { get; set; } = new();
    public decimal Subtotal => Rows.Sum(r => r.Balance);
}

public sealed class BalanceSheetRow
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}

public sealed class BalanceSheetResponse
{
    public DateTime AsOfDate { get; set; }
    public BalanceSheetSection Assets { get; set; } = new() { Title = "الأصول" };
    public BalanceSheetSection Liabilities { get; set; } = new() { Title = "الالتزامات" };
    public BalanceSheetSection Equity { get; set; } = new() { Title = "حقوق الملكية" };
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal TotalEquity { get; set; }
    public decimal TotalLiabilitiesAndEquity => TotalLiabilities + TotalEquity;
    public bool IsBalanced => Math.Abs(TotalAssets - TotalLiabilitiesAndEquity) < 0.01m;
    public decimal Variance => TotalAssets - TotalLiabilitiesAndEquity;
}

// ============== Cash Flow (Indirect Method) ==============

public sealed class CashFlowSection
{
    public string Title { get; set; } = string.Empty;
    public List<CashFlowLine> Lines { get; set; } = new();
    public decimal Subtotal => Lines.Sum(l => l.Amount);
}

public sealed class CashFlowLine
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class CashFlowResponse
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public CashFlowSection Operating { get; set; } = new() { Title = "الأنشطة التشغيلية" };
    public CashFlowSection Investing { get; set; } = new() { Title = "الأنشطة الاستثمارية" };
    public CashFlowSection Financing { get; set; } = new() { Title = "أنشطة التمويل" };
    public decimal NetOperatingCash => Operating.Subtotal;
    public decimal NetInvestingCash => Investing.Subtotal;
    public decimal NetFinancingCash => Financing.Subtotal;
    public decimal NetChangeInCash => NetOperatingCash + NetInvestingCash + NetFinancingCash;
}

// ============== AP Aging (per-vendor) ==============

public sealed class APAgingVendorBucket
{
    public string VendorCode { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public decimal Current { get; set; }     // 0-30 days
    public decimal Days31To60 { get; set; }
    public decimal Days61To90 { get; set; }
    public decimal Days91Plus { get; set; }
    public decimal Total => Current + Days31To60 + Days61To90 + Days91Plus;
}

public sealed class APAgingReportResponse
{
    public DateTime AsOfDate { get; set; }
    public List<APAgingVendorBucket> Vendors { get; set; } = new();
    public decimal TotalCurrent => Vendors.Sum(v => v.Current);
    public decimal Total31To60 => Vendors.Sum(v => v.Days31To60);
    public decimal Total61To90 => Vendors.Sum(v => v.Days61To90);
    public decimal Total91Plus => Vendors.Sum(v => v.Days91Plus);
    public decimal GrandTotal => Vendors.Sum(v => v.Total);
}
