using Dapper;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Shared.Infrastructure;
using ERPSystem.Modules.Reports.Application;

namespace ERPSystem.Modules.Reports.Application.Services;

public interface IFinanceReportService
{
    Task<TrialBalanceReport> GetTrialBalanceAsync(Guid tenantId, Guid? companyId, DateTime asOfDate, CancellationToken ct);
    Task<IncomeStatement> GetIncomeStatementAsync(Guid tenantId, Guid? companyId, DateTime from, DateTime to, CancellationToken ct);
    Task<BalanceSheet> GetBalanceSheetAsync(Guid tenantId, Guid? companyId, DateTime asOfDate, CancellationToken ct);
}

public sealed class FinanceReportService : IFinanceReportService
{
    private readonly IDbConnectionFactory _db;
    public FinanceReportService(IDbConnectionFactory db) => _db = db;

    /// <summary>Trial Balance: per account, total Debit/Credit from Posted entries up to asOfDate.</summary>
    public async Task<TrialBalanceReport> GetTrialBalanceAsync(Guid tenantId, Guid? companyId, DateTime asOfDate, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = @"
            SELECT a.id AS AccountId, a.code AS AccountCode, a.name AS AccountName, a.type AS AccountType,
                   COALESCE(SUM(jl.debit), 0) AS TotalDebit,
                   COALESCE(SUM(jl.credit), 0) AS TotalCredit
            FROM accounts a
            LEFT JOIN journal_lines jl ON jl.account_id = a.id
            LEFT JOIN journal_entries je ON je.id = jl.journal_entry_id
                AND je.status = 2
                AND je.entry_date <= @AsOfDate
                AND je.tenant_id = @TenantId
            WHERE a.tenant_id = @TenantId"
            + (companyId.HasValue ? @"
                AND (jl.id IS NULL OR EXISTS (
                    SELECT 1 FROM journal_entries je2
                    INNER JOIN journal_lines jl2 ON jl2.journal_entry_id = je2.id
                    WHERE je2.id = je.id AND jl2.company_id = @CompanyId
                ))" : "")
            + @"
            GROUP BY a.id, a.code, a.name, a.type
            HAVING COALESCE(SUM(jl.debit), 0) > 0 OR COALESCE(SUM(jl.credit), 0) > 0
            ORDER BY a.code";
        var rows = await conn.QueryAsync<TrialBalanceRow>(new CommandDefinition(sql,
            new { TenantId = tenantId, CompanyId = companyId, AsOfDate = asOfDate }, cancellationToken: ct));
        return new TrialBalanceReport
        {
            AsOfDate = asOfDate,
            Rows = rows.AsList()
        };
    }

    /// <summary>Income Statement: revenue - cogs - opex + other.</summary>
    public async Task<IncomeStatement> GetIncomeStatementAsync(Guid tenantId, Guid? companyId, DateTime from, DateTime to, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = @"
            SELECT a.type AS AccountType, a.code AS AccountCode,
                   COALESCE(SUM(jl.debit), 0) AS TotalDebit,
                   COALESCE(SUM(jl.credit), 0) AS TotalCredit
            FROM journal_lines jl
            INNER JOIN journal_entries je ON je.id = jl.journal_entry_id
            INNER JOIN accounts a ON a.id = jl.account_id
            WHERE je.status = 2
              AND je.tenant_id = @TenantId
              AND je.entry_date >= @From
              AND je.entry_date <= @To
              AND a.type IN (3, 4, 5)
            GROUP BY a.type, a.code";
        var rows = (await conn.QueryAsync<FinanceRow>(new CommandDefinition(sql,
            new { TenantId = tenantId, From = from, To = to }, cancellationToken: ct))).ToList();

        decimal revenue = 0, cogs = 0, opex = 0, otherIncome = 0, otherExp = 0;
        foreach (var r in rows)
        {
            var (type, code, debit, credit) = ((AccountType)r.AccountType, r.AccountCode, r.TotalDebit, r.TotalCredit);
            if (type == AccountType.Revenue) revenue += credit - debit;
            else if (type == AccountType.Expense)
            {
                var net = debit - credit;
                if (code.StartsWith("51")) cogs += net;          // 51xx = COGS
                else if (code.StartsWith("52") || code.StartsWith("53") || code.StartsWith("54")) opex += net; // 52-54 = OpEx
                else otherExp += net;
            }
        }
        return new IncomeStatement
        {
            From = from, To = to, Revenue = revenue, Cogs = cogs,
            OperatingExpenses = opex, OtherIncome = otherIncome, OtherExpenses = otherExp
        };
    }

    /// <summary>Balance Sheet: Σ Assets = Σ Liabilities + Equity (as of asOfDate).</summary>
    public async Task<BalanceSheet> GetBalanceSheetAsync(Guid tenantId, Guid? companyId, DateTime asOfDate, CancellationToken ct)
    {
        var tb = await GetTrialBalanceAsync(tenantId, companyId, asOfDate, ct);
        decimal assets = 0, liabilities = 0, equity = 0;
        foreach (var r in tb.Rows)
        {
            var net = r.Debit - r.Credit;
            if (r.AccountType == AccountType.Asset) assets += net;
            else if (r.AccountType == AccountType.Liability) liabilities += -net;
            else if (r.AccountType == AccountType.Equity) equity += -net;
        }
        return new BalanceSheet
        {
            AsOfDate = asOfDate,
            TotalAssets = assets, TotalLiabilities = liabilities, TotalEquity = equity
        };
    }

    private sealed class FinanceRow
    {
        public int AccountType { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
    }
}
