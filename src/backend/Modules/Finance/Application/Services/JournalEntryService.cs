using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Finance.Infrastructure;

namespace ERPSystem.Modules.Finance.Application.Services;

public sealed class JournalEntryService : IJournalEntryService
{
    private readonly IJournalEntryRepository _entries;
    private readonly IAccountRepository _accounts;
    private readonly ILogger<JournalEntryService> _logger;

    public JournalEntryService(
        IJournalEntryRepository entries,
        IAccountRepository accounts,
        ILogger<JournalEntryService> logger)
    {
        _entries = entries;
        _accounts = accounts;
        _logger = logger;
    }

    public async Task<FinanceResult<JournalEntryResponse>> CreateDraftAsync(Guid tenantId, Guid userId, PostJournalEntryRequest request, CancellationToken ct)
    {
        // 1) التحقق من وجود وصحة كل حساب
        var accountIds = request.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = new Dictionary<Guid, Account>();
        foreach (var aid in accountIds)
        {
            var acc = await _accounts.GetByIdAsync(aid, ct);
            if (acc == null || acc.TenantId != tenantId)
            {
                return FinanceResult<JournalEntryResponse>.Fail($"الحساب {aid} غير موجود.", FinanceErrorCode.NotFound);
            }
            if (!acc.IsActive)
            {
                return FinanceResult<JournalEntryResponse>.Fail($"الحساب '{acc.Code}' موقوف.", FinanceErrorCode.InvalidAccount);
            }
            if (!acc.IsPostable)
            {
                return FinanceResult<JournalEntryResponse>.Fail(
                    $"الحساب '{acc.Code}' حساب تجميعي — لا يقبل قيود مباشرة. استخدم حساباً فرعياً.",
                    FinanceErrorCode.InvalidAccount);
            }
            accounts[aid] = acc;
        }

        // 2) Double-Entry validation
        var totalDebit = request.Lines.Sum(l => l.Debit);
        var totalCredit = request.Lines.Sum(l => l.Credit);
        if (totalDebit != totalCredit)
        {
            return FinanceResult<JournalEntryResponse>.Fail(
                $"القيد غير متوازن: مجموع المدين {totalDebit:N4} ≠ مجموع الدائن {totalCredit:N4}.",
                FinanceErrorCode.Unbalanced);
        }

        if (totalDebit == 0)
        {
            return FinanceResult<JournalEntryResponse>.Fail("القيد بمبلغ صفر غير مسموح.", FinanceErrorCode.ValidationError);
        }

        // 3) توليد رقم القيد
        var entryNumber = await _entries.GetNextEntryNumberAsync(tenantId, ct);

        // 4) بناء الـ aggregate
        var now = DateTime.UtcNow;
        var entryId = Guid.NewGuid();
        var entry = new JournalEntry
        {
            Id = entryId,
            TenantId = tenantId,
            EntryNumber = entryNumber,
            EntryDate = request.EntryDate,
            Description = request.Description.Trim(),
            Reference = request.Reference,
            Status = JournalEntryStatus.Draft,
            CreatedByUserId = userId,
            PostedAt = null,
            CreatedAt = now,
            UpdatedAt = now,
            Lines = request.Lines.Select((l, idx) => new JournalLine
            {
                Id = Guid.NewGuid(),
                JournalEntryId = entryId,
                AccountId = l.AccountId,
                Debit = l.Debit,
                Credit = l.Credit,
                Description = l.Description,
                LineNumber = idx + 1
            }).ToList()
        };

        await _entries.InsertAsync(entry, ct);
        _logger.LogInformation("تم إنشاء قيد مسودة {Number} ({Lines} سطور، total={Total})",
            entryNumber, entry.Lines.Count, totalDebit);

        return FinanceResult<JournalEntryResponse>.Ok(await BuildResponseAsync(entry, accounts.Values, ct));
    }

    public async Task<FinanceResult<JournalEntryResponse>> PostAsync(Guid tenantId, Guid userId, Guid entryId, CancellationToken ct)
    {
        var entry = await _entries.GetWithLinesAsync(entryId, ct);
        if (entry == null || entry.TenantId != tenantId)
        {
            return FinanceResult<JournalEntryResponse>.Fail("القيد غير موجود.", FinanceErrorCode.NotFound);
        }
        if (entry.Status == JournalEntryStatus.Posted)
        {
            return FinanceResult<JournalEntryResponse>.Fail("القيد مُرحّل بالفعل.", FinanceErrorCode.ValidationError);
        }
        if (entry.Status == JournalEntryStatus.Reversed)
        {
            return FinanceResult<JournalEntryResponse>.Fail("القيد مُعكّس — لا يمكن ترحيله.", FinanceErrorCode.ValidationError);
        }

        // Double-Entry check مرة ثانية (defense in depth)
        var totalDebit = entry.Lines.Sum(l => l.Debit);
        var totalCredit = entry.Lines.Sum(l => l.Credit);
        if (totalDebit != totalCredit)
        {
            return FinanceResult<JournalEntryResponse>.Fail("القيد غير متوازن — لا يمكن ترحيله.", FinanceErrorCode.Unbalanced);
        }

        entry.Status = JournalEntryStatus.Posted;
        entry.PostedAt = DateTime.UtcNow;
        entry.UpdatedAt = DateTime.UtcNow;
        await _entries.UpdateAsync(entry, ct);
        _logger.LogInformation("تم ترحيل القيد {Number} بواسطة {UserId}", entry.EntryNumber, userId);

        var accounts = new List<Account>();
        foreach (var line in entry.Lines)
        {
            var acc = await _accounts.GetByIdAsync(line.AccountId, ct);
            if (acc != null) accounts.Add(acc);
        }
        return FinanceResult<JournalEntryResponse>.Ok(await BuildResponseAsync(entry, accounts, ct));
    }

    public async Task<FinanceResult<JournalEntryResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var entry = await _entries.GetWithLinesAsync(id, ct);
        if (entry == null || entry.TenantId != tenantId)
        {
            return FinanceResult<JournalEntryResponse>.Fail("القيد غير موجود.", FinanceErrorCode.NotFound);
        }
        var accounts = new List<Account>();
        foreach (var line in entry.Lines)
        {
            var acc = await _accounts.GetByIdAsync(line.AccountId, ct);
            if (acc != null) accounts.Add(acc);
        }
        return FinanceResult<JournalEntryResponse>.Ok(await BuildResponseAsync(entry, accounts, ct));
    }

    public async Task<FinanceResult<IReadOnlyList<JournalEntryResponse>>> ListAsync(Guid tenantId, DateTime? from, DateTime? to, JournalEntryStatus? status, int skip, int take, CancellationToken ct)
    {
        var rows = await _entries.ListAsync(tenantId, from, to, status, skip, take, ct);
        var result = new List<JournalEntryResponse>();
        foreach (var e in rows)
        {
            // نحمل entry كاملاً بالسطور لعرض totals
            var full = await _entries.GetWithLinesAsync(e.Id, ct);
            if (full == null) continue;
            var accounts = new List<Account>();
            foreach (var line in full.Lines)
            {
                var acc = await _accounts.GetByIdAsync(line.AccountId, ct);
                if (acc != null) accounts.Add(acc);
            }
            result.Add(await BuildResponseAsync(full, accounts, ct));
        }
        return FinanceResult<IReadOnlyList<JournalEntryResponse>>.Ok(result);
    }

    private static Task<JournalEntryResponse> BuildResponseAsync(JournalEntry e, IEnumerable<Account> accounts, CancellationToken ct)
    {
        var accountMap = accounts.ToDictionary(a => a.Id);
        return Task.FromResult(new JournalEntryResponse
        {
            Id = e.Id,
            EntryNumber = e.EntryNumber,
            EntryDate = e.EntryDate,
            Description = e.Description,
            Reference = e.Reference,
            Status = e.Status,
            PostedAt = e.PostedAt,
            Lines = e.Lines.OrderBy(l => l.LineNumber).Select(l =>
            {
                accountMap.TryGetValue(l.AccountId, out var acc);
                return new JournalLineResponse
                {
                    LineNumber = l.LineNumber,
                    AccountId = l.AccountId,
                    AccountCode = acc?.Code ?? string.Empty,
                    AccountName = acc?.Name ?? string.Empty,
                    Debit = l.Debit,
                    Credit = l.Credit,
                    Description = l.Description
                };
            }).ToList(),
            TotalDebit = e.Lines.Sum(l => l.Debit),
            TotalCredit = e.Lines.Sum(l => l.Credit)
        });
    }
}
