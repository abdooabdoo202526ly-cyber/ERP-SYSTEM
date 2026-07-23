using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Application.Services;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Finance.Infrastructure;
using ERPSystem.Shared.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERPSystem.Tests.Finance;

/// <summary>اختبارات منطق الـ double-entry — تستخدم mock repositories (لا تحتاج DB)</summary>
public class JournalEntryServiceUnitTests
{
    private static JournalEntryService BuildService(
        FakeAccountRepository accounts,
        FakeJournalEntryRepository entries)
    {
        return new JournalEntryService(entries, accounts, NullLogger<JournalEntryService>.Instance);
    }

    [Fact]
    public async Task CreateDraft_BalancedEntry_Succeeds()
    {
        var tenantId = Guid.NewGuid();
        var cashId = Guid.NewGuid();
        var revenueId = Guid.NewGuid();

        var accounts = new FakeAccountRepository();
        accounts.Add(new Account { Id = cashId, TenantId = tenantId, Code = "1110", Name = "الصندوق", Type = AccountType.Asset, NormalBalance = NormalBalance.Debit, IsPostable = true, IsActive = true });
        accounts.Add(new Account { Id = revenueId, TenantId = tenantId, Code = "4100", Name = "إيرادات", Type = AccountType.Revenue, NormalBalance = NormalBalance.Credit, IsPostable = true, IsActive = true });

        var entries = new FakeJournalEntryRepository();
        var svc = BuildService(accounts, entries);

        var req = new PostJournalEntryRequest
        {
            EntryDate = DateTime.UtcNow,
            Description = "بيع نقدي",
            Lines = new()
            {
                new() { AccountId = cashId, Debit = 500, Credit = 0 },
                new() { AccountId = revenueId, Debit = 0, Credit = 500 }
            }
        };

        var result = await svc.CreateDraftAsync(tenantId, Guid.NewGuid(), req, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.EntryNumber.Should().StartWith("JE-");
        result.Value.TotalDebit.Should().Be(500);
        result.Value.TotalCredit.Should().Be(500);
        result.Value.Status.Should().Be(JournalEntryStatus.Draft);
        result.Value.Lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateDraft_UnbalancedEntry_Fails()
    {
        var tenantId = Guid.NewGuid();
        var cashId = Guid.NewGuid();
        var revenueId = Guid.NewGuid();

        var accounts = new FakeAccountRepository();
        accounts.Add(new Account { Id = cashId, TenantId = tenantId, Code = "1110", Name = "Cash", Type = AccountType.Asset, NormalBalance = NormalBalance.Debit, IsPostable = true, IsActive = true });
        accounts.Add(new Account { Id = revenueId, TenantId = tenantId, Code = "4100", Name = "Revenue", Type = AccountType.Revenue, NormalBalance = NormalBalance.Credit, IsPostable = true, IsActive = true });

        var entries = new FakeJournalEntryRepository();
        var svc = BuildService(accounts, entries);

        var req = new PostJournalEntryRequest
        {
            EntryDate = DateTime.UtcNow,
            Description = "قيد غير متوازن",
            Lines = new()
            {
                new() { AccountId = cashId, Debit = 1000, Credit = 0 },
                new() { AccountId = revenueId, Debit = 0, Credit = 500 }
            }
        };

        var result = await svc.CreateDraftAsync(tenantId, Guid.NewGuid(), req, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(FinanceErrorCode.Unbalanced);
    }

    [Fact]
    public async Task CreateDraft_NonPostableAccount_Fails()
    {
        var tenantId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var cashId = Guid.NewGuid();

        var accounts = new FakeAccountRepository();
        accounts.Add(new Account { Id = parentId, TenantId = tenantId, Code = "1100", Name = "Parent (non-postable)", Type = AccountType.Asset, NormalBalance = NormalBalance.Debit, IsPostable = false, IsActive = true });
        accounts.Add(new Account { Id = cashId, TenantId = tenantId, Code = "1110", Name = "Cash", Type = AccountType.Asset, NormalBalance = NormalBalance.Debit, IsPostable = true, IsActive = true });

        var entries = new FakeJournalEntryRepository();
        var svc = BuildService(accounts, entries);

        var req = new PostJournalEntryRequest
        {
            EntryDate = DateTime.UtcNow,
            Description = "قيد على حساب تجميعي",
            Lines = new()
            {
                new() { AccountId = parentId, Debit = 100, Credit = 0 },
                new() { AccountId = cashId, Debit = 0, Credit = 100 }
            }
        };

        var result = await svc.CreateDraftAsync(tenantId, Guid.NewGuid(), req, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(FinanceErrorCode.InvalidAccount);
    }

    [Fact]
    public async Task Post_DraftToPosted_Succeeds()
    {
        var tenantId = Guid.NewGuid();
        var cashId = Guid.NewGuid();
        var revenueId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var accounts = new FakeAccountRepository();
        accounts.Add(new Account { Id = cashId, TenantId = tenantId, Code = "1110", Name = "Cash", Type = AccountType.Asset, NormalBalance = NormalBalance.Debit, IsPostable = true, IsActive = true });
        accounts.Add(new Account { Id = revenueId, TenantId = tenantId, Code = "4100", Name = "Revenue", Type = AccountType.Revenue, NormalBalance = NormalBalance.Credit, IsPostable = true, IsActive = true });

        var entries = new FakeJournalEntryRepository();
        var svc = BuildService(accounts, entries);

        var draft = await svc.CreateDraftAsync(tenantId, userId, new PostJournalEntryRequest
        {
            EntryDate = DateTime.UtcNow,
            Description = "test",
            Lines = new()
            {
                new() { AccountId = cashId, Debit = 200, Credit = 0 },
                new() { AccountId = revenueId, Debit = 0, Credit = 200 }
            }
        }, CancellationToken.None);

        var post = await svc.PostAsync(tenantId, userId, draft.Value!.Id, CancellationToken.None);

        post.Succeeded.Should().BeTrue();
        post.Value!.Status.Should().Be(JournalEntryStatus.Posted);
        post.Value.PostedAt.Should().NotBeNull();
    }
}

// ============== Fake Repositories (in-memory) ==============

internal class FakeAccountRepository : IAccountRepository
{
    private readonly List<Account> _accounts = new();
    public void Add(Account a) => _accounts.Add(a);
    public Task<Account?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(_accounts.FirstOrDefault(a => a.Id == id));
    public Task<Account?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct) => Task.FromResult(_accounts.FirstOrDefault(a => a.TenantId == tenantId && a.Code == code));
    public Task<IReadOnlyList<Account>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Account>>(_accounts.Where(a => a.TenantId == tenantId && (includeInactive || a.IsActive)).ToList());
    public Task<IReadOnlyList<Account>> ListChildrenAsync(Guid parentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Account>>(_accounts.Where(a => a.ParentAccountId == parentId).ToList());
    public Task InsertAsync(Account account, CancellationToken ct) { _accounts.Add(account); return Task.CompletedTask; }
    public Task UpdateAsync(Account account, CancellationToken ct) { var existing = _accounts.FindIndex(a => a.Id == account.Id); if (existing >= 0) _accounts[existing] = account; return Task.CompletedTask; }
    public Task<int> CountPostingsAsync(Guid accountId, CancellationToken ct) => Task.FromResult(0);
    public Task EnsureDefaultCoAAsync(Guid tenantId, Guid companyId, CancellationToken ct) => Task.CompletedTask;
    public Task CloneCoAFromCompanyAsync(Guid targetCompanyId, Guid sourceCompanyId, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<Account>> ListByCompanyAsync(Guid tenantId, Guid? companyId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Account>>(_accounts.Where(a => a.TenantId == tenantId && (companyId == null || a.CompanyId == companyId)).ToList());
}

internal class FakeJournalEntryRepository : IJournalEntryRepository
{
    private readonly Dictionary<Guid, JournalEntry> _entries = new();
    private int _counter = 1;

    public Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_entries.TryGetValue(id, out var e) ? e : null);
    public Task<JournalEntry?> GetWithLinesAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_entries.TryGetValue(id, out var e) ? e : null);
    public Task<bool> EntryNumberExistsAsync(Guid tenantId, string entryNumber, CancellationToken ct) =>
        Task.FromResult(_entries.Values.Any(e => e.TenantId == tenantId && e.EntryNumber == entryNumber));
    public Task<string> GetNextEntryNumberAsync(Guid tenantId, CancellationToken ct) =>
        Task.FromResult($"JE-{DateTime.UtcNow.Year}-{_counter++:D4}");
    public Task InsertAsync(JournalEntry entry, CancellationToken ct) { _entries[entry.Id] = entry; return Task.CompletedTask; }
    public Task UpdateAsync(JournalEntry entry, CancellationToken ct) { _entries[entry.Id] = entry; return Task.CompletedTask; }
    public Task<IReadOnlyList<JournalEntry>> ListAsync(Guid tenantId, DateTime? from, DateTime? to, JournalEntryStatus? status, int skip, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<JournalEntry>>(_entries.Values
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.EntryDate)
            .ToList());
}
