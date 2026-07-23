using Dapper;
using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Finance.Application.Services;

public interface ICashFlowService
{
    Task<CashFlowResponse> GetAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct);
}

/// <summary>
/// Cash Flow Statement — الطريقة غير المباشرة (Indirect Method) — الفترة [from, to].
///
/// Operating: Net Profit + non-cash adjustments + changes in working capital
///   - Net Profit = (Revenue credit - Revenue debit) - (Expense debit - Expense credit)
///   - Δ A/R: -(increase in 1230 balance)
///   - Δ A/P: +(increase in 2210 balance)
///   - Δ Inventory: -(increase in 1240 balance)
///
/// Investing: Δ Fixed Assets (1101/1102/1103)
/// Financing: Δ Loans (2100) + Δ Capital (3100) + Δ Retained Earnings (3200/3300) - Dividends
///
/// ملاحظة: صفر أرباح موزعة حالياً — الـ dividend في Phase 6+.
/// </summary>
public sealed class CashFlowService : ICashFlowService
{
    private readonly IDbConnectionFactory _db;
    public CashFlowService(IDbConnectionFactory db) => _db = db;

    public async Task<CashFlowResponse> GetAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);

        // 1) Net Profit (Revenue - Expense) للفترة
        const string periodSumSql = @"
            SELECT a.type AS AccountType, a.code AS AccountCode,
                   COALESCE(SUM(jl.debit), 0) AS Dr, COALESCE(SUM(jl.credit), 0) AS Cr
            FROM journal_lines jl
            INNER JOIN journal_entries je ON je.id = jl.journal_entry_id
            INNER JOIN accounts a ON a.id = jl.account_id
            WHERE je.tenant_id = @TenantId AND je.status = 2
              AND je.entry_date >= @From AND je.entry_date <= @To
            GROUP BY a.type, a.code";
        var periodRows = (await conn.QueryAsync<(int AccountType, string AccountCode, decimal Dr, decimal Cr)>(
            new CommandDefinition(periodSumSql, new { TenantId = tenantId, From = from, To = to }, cancellationToken: ct))).ToList();

        decimal revenue = 0m, expense = 0m;
        foreach (var (type, code, dr, cr) in periodRows)
        {
            if ((AccountType)type == AccountType.Revenue) revenue += (cr - dr);
            else if ((AccountType)type == AccountType.Expense) expense += (dr - cr);
        }
        var netProfit = revenue - expense;

        // 2) Working Capital Δ — مقارنة مجموع رصيد الحساب نهاية الفترة وبداية الفترة
        //    Customer AR (1230), Vendor AP (2210), Inventory (1240)
        //    Δ = balance_to - balance_from (signs: زيادة في الأصل = استخدام نقدية سالب)
        var wcDelta = await GetNetChangeAsync(conn, tenantId, from, to, "1230", ct)
                    - await GetNetChangeAsync(conn, tenantId, from, to, "2210", ct)
                    - await GetNetChangeAsync(conn, tenantId, from, to, "1240", ct);

        var operating = new CashFlowSection();
        operating.Lines.Add(new CashFlowLine { Description = "صافي الربح", Amount = netProfit });
        operating.Lines.Add(new CashFlowLine { Description = "Δ الذمم المدينة (1230)", Amount = -await GetNetChangeAsync(conn, tenantId, from, to, "1230", ct) });
        operating.Lines.Add(new CashFlowLine { Description = "Δ الذمم الدائنة (2210)", Amount = await GetNetChangeAsync(conn, tenantId, from, to, "2210", ct) });
        operating.Lines.Add(new CashFlowLine { Description = "Δ المخزون (1240)", Amount = -await GetNetChangeAsync(conn, tenantId, from, to, "1240", ct) });

        // 3) Investing: Δ في حسابات الأصول الثابتة (1101, 1102, 1103) — شراء أصل = -cash
        var investing = new CashFlowSection();
        foreach (var faCode in new[] { "1101", "1102", "1103" })
        {
            var change = await GetNetChangeAsync(conn, tenantId, from, to, faCode, ct);
            if (change != 0)
                investing.Lines.Add(new CashFlowLine { Description = $"Δ {faCode}", Amount = -change });
        }

        // 4) Financing: Δ في القروض (2100) + رأس المال (3100) + الأرباح المحتجزة (3200/3300)
        var financing = new CashFlowSection();
        foreach (var code in new[] { "2100", "3100", "3200", "3300" })
        {
            var change = await GetNetChangeAsync(conn, tenantId, from, to, code, ct);
            if (change != 0)
                financing.Lines.Add(new CashFlowLine { Description = $"Δ {code}", Amount = change });
        }
        // Dividends — Phase 6+

        return new CashFlowResponse
        {
            From = from,
            To = to,
            Operating = operating,
            Investing = investing,
            Financing = financing
        };
    }

    /// <summary>
    /// رصيد حساب = مجموع Debit - مجموع Credit للقيود Posted في الفترة.
    /// (للقيم الموجبة في الأصول، السالبة في الالتزامات/حقوق الملكية — بحسب NormalBalance).
    /// </summary>
    private static async Task<decimal> GetNetChangeAsync(
        System.Data.IDbConnection conn, Guid tenantId, DateTime from, DateTime to, string accountCode, CancellationToken ct)
    {
        const string sql = @"
            SELECT COALESCE(SUM(jl.debit), 0) - COALESCE(SUM(jl.credit), 0) AS Delta
            FROM journal_lines jl
            INNER JOIN journal_entries je ON je.id = jl.journal_entry_id
            INNER JOIN accounts a ON a.id = jl.account_id
            WHERE je.tenant_id = @TenantId
              AND je.status = 2
              AND a.code = @Code
              AND je.entry_date >= @From AND je.entry_date <= @To";
        var delta = await conn.QueryFirstOrDefaultAsync<decimal?>(new CommandDefinition(sql,
            new { TenantId = tenantId, Code = accountCode, From = from, To = to }, cancellationToken: ct));
        return delta ?? 0m;
    }
}
