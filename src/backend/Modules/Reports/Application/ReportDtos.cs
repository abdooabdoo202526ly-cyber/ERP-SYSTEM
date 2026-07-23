using System;
using System.Collections.Generic;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Inventory.Entities;
using ERPSystem.Modules.Projects.Entities;

namespace ERPSystem.Modules.Reports.Application;

// ===== Project Reports =====

public sealed class ProjectPnL
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal Revenue { get; set; }
    public decimal MaterialCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal SubcontractorCost { get; set; }
    public decimal AllocatedOverhead { get; set; }
    public decimal DirectCosts => MaterialCost + LaborCost + SubcontractorCost;
    public decimal NetProfit => Revenue - DirectCosts - AllocatedOverhead;
    public decimal MarginPercent => Revenue > 0 ? (NetProfit / Revenue) * 100 : 0;
}

public sealed class ProjectBudgetVsActual
{
    public Guid ProjectId { get; set; }
    public string ProjectCode { get; set; } = string.Empty;
    public decimal BudgetAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal CommittedAmount { get; set; }
    public decimal AvailableAmount => BudgetAmount - SpentAmount - CommittedAmount;
    public decimal Variance => BudgetAmount - SpentAmount;
    public decimal VariancePercent => BudgetAmount > 0 ? (Variance / BudgetAmount) * 100 : 0;
    public decimal UtilizationPercent => BudgetAmount > 0 ? (SpentAmount / BudgetAmount) * 100 : 0;
    public DateTime? LastRecalculatedAt { get; set; }
}

public sealed class ProjectSummary
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ProjectStatus Status { get; set; }
    public decimal Budget { get; set; }
    public decimal Spent { get; set; }
    public decimal MarginPercent { get; set; }
    public DateTime? LastActivity { get; set; }
}

// ===== Inventory Reports =====

public sealed class StockValuation
{
    public Guid ItemId { get; set; }
    public string ItemSku { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal AverageCost { get; set; }
    public decimal TotalValue => QuantityOnHand * AverageCost;
}

public sealed class StockMovementHistory
{
    public Guid MovementId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public StockMovementType Type { get; set; }
    public DateTime MovementDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class LowStockItem
{
    public Guid ItemId { get; set; }
    public string ItemSku { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal QuantityReserved { get; set; }
    public decimal QuantityAvailable => QuantityOnHand - QuantityReserved;
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQuantity { get; set; }
    public decimal Shortfall => ReorderLevel - QuantityAvailable;
    public string Status => QuantityOnHand == 0
        ? "Critical"
        : (QuantityOnHand < ReorderLevel / 2 ? "Warning" : "Low");
}

public sealed class StockAging
{
    public Guid ItemId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public DateTime? LastMovementAt { get; set; }
    public int? DaysInStock { get; set; }
    public string AgeBucket => DaysInStock switch
    {
        null => string.Empty,
        int d when d <= 30 => "0-30",
        int d when d <= 60 => "31-60",
        int d when d <= 90 => "61-90",
        _ => "90+"
    };
}

// ===== Finance Reports =====

public sealed class TrialBalanceRow
{
    public Guid AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal NetDebit => Debit - Credit;
    public decimal NetCredit => Credit - Debit;
}

public sealed class TrialBalanceReport
{
    public DateTime AsOfDate { get; set; }
    public List<TrialBalanceRow> Rows { get; set; } = new();
    public decimal TotalDebit => Rows.Sum(r => r.Debit);
    public decimal TotalCredit => Rows.Sum(r => r.Credit);
    public bool IsBalanced => Math.Abs(TotalDebit - TotalCredit) < 0.01m;
    public decimal Variance => TotalDebit - TotalCredit;
}

public sealed class IncomeStatement
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cogs { get; set; }
    public decimal GrossProfit => Revenue - Cogs;
    public decimal OperatingExpenses { get; set; }
    public decimal OtherIncome { get; set; }
    public decimal OtherExpenses { get; set; }
    public decimal NetIncome => GrossProfit - OperatingExpenses + OtherIncome - OtherExpenses;
}

public sealed class BalanceSheet
{
    public DateTime AsOfDate { get; set; }
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal TotalEquity { get; set; }
    public decimal TotalLiabilitiesAndEquity => TotalLiabilities + TotalEquity;
    public bool IsBalanced => Math.Abs(TotalAssets - TotalLiabilitiesAndEquity) < 0.01m;
    public decimal Variance => TotalAssets - TotalLiabilitiesAndEquity;
}
