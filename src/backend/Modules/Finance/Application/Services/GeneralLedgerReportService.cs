using Dapper;
using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Finance.Infrastructure;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Finance.Application.Services;

public interface IGeneralLedgerReportService
{
    Task<FinanceResult<GeneralLedgerReportResponse>> GetAccountLedgerAsync(
        Guid tenantId, Guid accountId, DateTime? from, DateTime? to, CancellationToken ct);
}

/// <summary>
/// General Ledger Report (per-account).
///
/// يُرجع كل سطور القيد المحاسبي على حساب معيّن (حالة Posted) في فترة اختيارية،
/// مع رصيد جارٍ بحسب NormalBalance (Dr: +debit-credit، Cr: +credit-debit).
///
/// Opening balance = مجموع الحركات قبل `from` (لو from=null، الافتتاح = 0).
/// Closing = Opening + TotalDebit - TotalCredit (Debit-normal accounts)
/// أو Opening + TotalCredit - TotalDebit (Credit-normal).
/// </summary>
public sealed class GeneralLedgerReportService : IGeneralLedgerReportService
{
    private readonly IDbConnectionFactory _db;
    private readonly IAccountRepository _accounts;

    public GeneralLedgerReportService(IDbConnectionFactory db, IAccountRepository accounts)
    {
        _db = db; _accounts = accounts;
    }

    public async Task<FinanceResult<GeneralLedgerReportResponse>> GetAccountLedgerAsync(
        Guid tenantId, Guid accountId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var account = await _accounts.GetByIdAsync(accountId, ct);
        if (account == null || account.TenantId != tenantId)
            return FinanceResult<GeneralLedgerReportResponse>.Fail("الحساب غير موجود.", FinanceErrorCode.NotFound);

        using var conn = await _db.CreateOltpConnectionAsync(ct);

        // 1) Opening Balance — مجموع الحركات على الحساب قبل `from` (Posted only)
        decimal opening = 0m;
        if (from.HasValue)
        {
            var openingSql = @"
                SELECT COALESCE(SUM(jl.debit), 0) AS Dr, COALESCE(SUM(jl.credit), 0) AS Cr
                FROM journal_lines jl
                INNER JOIN journal_entries je ON je.id = jl.journal_entry_id
                WHERE jl.account_id = @AccountId AND je.tenant_id = @TenantId
                  AND je.status = 2 AND je.entry_date < @From";
            var opRow = await conn.QueryFirstOrDefaultAsync<(decimal Dr, decimal Cr)>(
                new CommandDefinition(openingSql, new { AccountId = accountId, TenantId = tenantId, From = from.Value }, cancellationToken: ct));
            opening = account.NormalBalance == NormalBalance.Debit ? (opRow.Dr - opRow.Cr) : (opRow.Cr - opRow.Dr);
        }

        // 2) Period lines
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        p.Add("AccountId", accountId);
        var sql = @"
            SELECT je.entry_date AS EntryDate, je.entry_number AS EntryNumber, je.id AS JournalEntryId,
                   je.reference, je.description AS EntryDescription,
                   jl.debit AS Debit, jl.credit AS Credit
            FROM journal_lines jl
            INNER JOIN journal_entries je ON je.id = jl.journal_entry_id
            WHERE je.tenant_id = @TenantId AND jl.account_id = @AccountId AND je.status = 2";
        if (from.HasValue) { sql += " AND je.entry_date >= @From"; p.Add("From", from.Value); }
        if (to.HasValue) { sql += " AND je.entry_date <= @To"; p.Add("To", to.Value); }
        sql += " ORDER BY je.entry_date, je.entry_number, jl.line_number";

        var rows = (await conn.QueryAsync<LedgerRow>(new CommandDefinition(sql, p, cancellationToken: ct))).ToList();

        decimal running = opening;
        decimal totalDr = 0m, totalCr = 0m;
        var lines = new List<GeneralLedgerLineResponse>();
        foreach (var r in rows)
        {
            totalDr += r.Debit; totalCr += r.Credit;
            var delta = account.NormalBalance == NormalBalance.Debit
                ? r.Debit - r.Credit
                : r.Credit - r.Debit;
            running += delta;
            lines.Add(new GeneralLedgerLineResponse
            {
                EntryDate = r.EntryDate,
                EntryNumber = r.EntryNumber,
                JournalEntryId = r.JournalEntryId,
                Reference = r.Reference,
                EntryDescription = r.EntryDescription,
                AccountCode = account.Code,
                AccountName = account.Name,
                Debit = r.Debit,
                Credit = r.Credit,
                RunningBalance = running
            });
        }

        return FinanceResult<GeneralLedgerReportResponse>.Ok(new GeneralLedgerReportResponse
        {
            AccountId = account.Id,
            AccountCode = account.Code,
            AccountName = account.Name,
            AccountTypeName = account.Type.ToString(),
            From = from,
            To = to,
            OpeningBalance = opening,
            TotalDebit = totalDr,
            TotalCredit = totalCr,
            ClosingBalance = running,
            Lines = lines
        });
    }

    private sealed class LedgerRow
    {
        public DateTime EntryDate { get; set; }
        public string EntryNumber { get; set; } = string.Empty;
        public Guid JournalEntryId { get; set; }
        public string? Reference { get; set; }
        public string EntryDescription { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }
}
