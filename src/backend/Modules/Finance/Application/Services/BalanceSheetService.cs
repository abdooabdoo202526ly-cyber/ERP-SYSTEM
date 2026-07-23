using Dapper;
using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Finance.Application.Services;

public interface IBalanceSheetService
{
    Task<BalanceSheetResponse> GetAsync(Guid tenantId, DateTime asOfDate, CancellationToken ct);
}

/// <summary>
/// Balance Sheet — تقرير الميزانية العمومية (asOfDate).
///
/// لكل حساب postable في tenant: مجموع Debit - مجموع Credit من القيود Posted حتى asOfDate.
/// - Assets (1): الرصيد الموجب = مدين - دائن
/// - Liabilities (2): الرصيد = -(مدين - دائن) (طبيعتها دائن)
/// - Equity (3): نفس القاعدة
///
/// التقرير متوازن إذا Assets = Liabilities + Equity.
/// </summary>
public sealed class BalanceSheetService : IBalanceSheetService
{
    private readonly IDbConnectionFactory _db;
    public BalanceSheetService(IDbConnectionFactory db) => _db = db;

    public async Task<BalanceSheetResponse> GetAsync(Guid tenantId, DateTime asOfDate, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);

        // إجمالي لكل حساب (postable فقط — لا نعرض الحسابات التجميعية كصفوف منفصلة)
        const string sql = @"
            SELECT a.code AS AccountCode, a.name AS AccountName, a.type AS AccountType,
                   COALESCE(SUM(jl.debit), 0) AS TotalDebit,
                   COALESCE(SUM(jl.credit), 0) AS TotalCredit
            FROM accounts a
            LEFT JOIN journal_lines jl ON jl.account_id = a.id
            LEFT JOIN journal_entries je
                ON je.id = jl.journal_entry_id
                AND je.status = 2
                AND je.entry_date <= @AsOfDate
                AND je.tenant_id = @TenantId
            WHERE a.tenant_id = @TenantId
              AND a.is_postable = true
              AND a.is_active = true
            GROUP BY a.id, a.code, a.name, a.type
            HAVING COALESCE(SUM(jl.debit), 0) > 0 OR COALESCE(SUM(jl.credit), 0) > 0
            ORDER BY a.code";

        var rows = (await conn.QueryAsync<BsRow>(new CommandDefinition(sql,
            new { TenantId = tenantId, AsOfDate = asOfDate }, cancellationToken: ct))).ToList();

        var response = new BalanceSheetResponse { AsOfDate = asOfDate };
        var assets = new Dictionary<string, BalanceSheetRow>();
        var liabs = new Dictionary<string, BalanceSheetRow>();
        var equity = new Dictionary<string, BalanceSheetRow>();

        foreach (var r in rows)
        {
            var net = r.TotalDebit - r.TotalCredit;
            // For Liabilities/Equity: نُخزّن المبلغ كقيمة موجبة (|net|)
            var display = (AccountType)r.AccountType switch
            {
                AccountType.Asset => net,
                _ => -net
            };
            var row = new BalanceSheetRow
            {
                AccountCode = r.AccountCode,
                AccountName = r.AccountName,
                Balance = display
            };
            switch ((AccountType)r.AccountType)
            {
                case AccountType.Asset: assets[r.AccountCode] = row; break;
                case AccountType.Liability: liabs[r.AccountCode] = row; break;
                case AccountType.Equity: equity[r.AccountCode] = row; break;
            }
        }

        response.Assets.Rows = assets.Values.OrderBy(r => r.AccountCode).ToList();
        response.Liabilities.Rows = liabs.Values.OrderBy(r => r.AccountCode).ToList();
        response.Equity.Rows = equity.Values.OrderBy(r => r.AccountCode).ToList();

        response.TotalAssets = response.Assets.Subtotal;
        response.TotalLiabilities = response.Liabilities.Subtotal;
        response.TotalEquity = response.Equity.Subtotal;

        return response;
    }

    private sealed class BsRow
    {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public int AccountType { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
    }
}
