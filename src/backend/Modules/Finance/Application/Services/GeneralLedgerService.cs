using Dapper;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Finance.Infrastructure;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Finance.Application.Services;

public sealed class GeneralLedgerService : IGeneralLedgerService
{
    private readonly IDbConnectionFactory _db;
    private readonly IAccountRepository _accounts;

    public GeneralLedgerService(IDbConnectionFactory db, IAccountRepository accounts)
    {
        _db = db;
        _accounts = accounts;
    }

    public async Task<FinanceResult<IReadOnlyList<AccountBalanceResponse>>> GetAccountBalancesAsync(Guid tenantId, DateTime? asOf, CancellationToken ct)
    {
        // استعلام واحد: نجمع المدين/الدائن لكل حساب من القيود المُرحّلة
        var asOfDate = asOf ?? DateTime.UtcNow;
        using var conn = await _db.CreateOltpConnectionAsync(ct);

        const string sql = @"
            SELECT a.id AS AccountId, a.code AS AccountCode, a.name AS AccountName,
                   a.type AS Type, a.normal_balance AS NormalBalance,
                   COALESCE(SUM(jl.debit), 0) AS TotalDebit,
                   COALESCE(SUM(jl.credit), 0) AS TotalCredit
            FROM accounts a
            LEFT JOIN journal_lines jl ON jl.account_id = a.id
            LEFT JOIN journal_entries je ON je.id = jl.journal_entry_id
                AND je.status = 2  -- Posted
                AND je.entry_date <= @AsOf
            WHERE a.tenant_id = @TenantId
              AND a.is_postable = true
            GROUP BY a.id, a.code, a.name, a.type, a.normal_balance, a.normal_balance
            ORDER BY a.code";

        var rows = await conn.QueryAsync<AccountBalanceRow>(new CommandDefinition(sql,
            new { TenantId = tenantId, AsOf = asOfDate }, cancellationToken: ct));

        var result = rows.Select(r => new AccountBalanceResponse
        {
            AccountId = r.AccountId,
            AccountCode = r.AccountCode,
            AccountName = r.AccountName,
            Type = (AccountType)r.Type,
            NormalBalance = (NormalBalance)r.NormalBalance,
            TotalDebit = r.TotalDebit,
            TotalCredit = r.TotalCredit,
            Balance = ComputeBalance((AccountType)r.Type, (NormalBalance)r.NormalBalance, r.TotalDebit, r.TotalCredit)
        }).ToList();

        return FinanceResult<IReadOnlyList<AccountBalanceResponse>>.Ok(result);
    }

    public async Task<FinanceResult<IReadOnlyList<LedgerLineResponse>>> GetAccountLedgerAsync(Guid tenantId, Guid accountId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(accountId, ct);
        if (account == null || account.TenantId != tenantId)
        {
            return FinanceResult<IReadOnlyList<LedgerLineResponse>>.Fail("الحساب غير موجود.", FinanceErrorCode.NotFound);
        }

        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = @"
            SELECT je.entry_date AS EntryDate, je.entry_number AS EntryNumber, je.id AS JournalEntryId,
                   je.reference, je.description AS EntryDescription,
                   a.code AS AccountCode, a.name AS AccountName,
                   jl.debit AS Debit, jl.credit AS Credit
            FROM journal_lines jl
            INNER JOIN journal_entries je ON je.id = jl.journal_entry_id
            INNER JOIN accounts a ON a.id = jl.account_id
            WHERE a.tenant_id = @TenantId
              AND jl.account_id = @AccountId
              AND je.status = 2";
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("AccountId", accountId);
        if (from.HasValue) { sql += " AND je.entry_date >= @From"; p.Add("From", from.Value); }
        if (to.HasValue) { sql += " AND je.entry_date <= @To"; p.Add("To", to.Value); }
        sql += " ORDER BY je.entry_date, je.entry_number";

        var rows = await conn.QueryAsync<LedgerRow>(new CommandDefinition(sql, p, cancellationToken: ct));

        // الرصيد الجاري: بحسب NormalBalance
        decimal running = 0;
        var lines = new List<LedgerLineResponse>();
        foreach (var r in rows)
        {
            var delta = account.NormalBalance == NormalBalance.Debit
                ? r.Debit - r.Credit
                : r.Credit - r.Debit;
            running += delta;
            lines.Add(new LedgerLineResponse
            {
                EntryDate = r.EntryDate,
                EntryNumber = r.EntryNumber,
                JournalEntryId = r.JournalEntryId.ToString(),
                Reference = r.Reference,
                Description = r.EntryDescription,
                AccountCode = r.AccountCode,
                AccountName = r.AccountName,
                Debit = r.Debit,
                Credit = r.Credit,
                RunningBalance = running
            });
        }

        return FinanceResult<IReadOnlyList<LedgerLineResponse>>.Ok(lines);
    }

    public Task<FinanceResult<IReadOnlyList<AccountBalanceResponse>>> GetTrialBalanceAsync(Guid tenantId, DateTime? asOf, CancellationToken ct)
    {
        // Trial Balance = نفس الأرصدة، مقتصرة على postable accounts
        return GetAccountBalancesAsync(tenantId, asOf, ct);
    }

    private static decimal ComputeBalance(AccountType type, NormalBalance normal, decimal debit, decimal credit)
    {
        // الرصيد بحسب طبيعته (طبيعية الحساب)
        var delta = normal == NormalBalance.Debit
            ? debit - credit
            : credit - debit;
        return delta;
    }

    private sealed class AccountBalanceRow
    {
        public Guid AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public int Type { get; set; }
        public int NormalBalance { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
    }

    private sealed class LedgerRow
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
    }
}
