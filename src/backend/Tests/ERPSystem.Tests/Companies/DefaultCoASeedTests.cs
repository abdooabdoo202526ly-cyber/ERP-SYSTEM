using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Finance.Infrastructure;
using ERPSystem.Shared.SeedData;
using FluentAssertions;

namespace ERPSystem.Tests.Companies;

public class DefaultCoASeedTests
{
    [Fact]
    public void HoldingAccounts_ContainsAtLeast40Accounts()
    {
        DefaultCoASeed.HoldingAccounts.Length.Should().BeGreaterThanOrEqualTo(40);
    }

    [Fact]
    public void HoldingAccounts_AllParentCodesReferenceExistingCodes()
    {
        var codes = DefaultCoASeed.HoldingAccounts.Select(a => a.Code).ToHashSet();
        foreach (var (code, _, _, parentCode, _, _) in DefaultCoASeed.HoldingAccounts)
        {
            if (parentCode != null)
                codes.Should().Contain(parentCode, $"الحساب {code} يشير لأب {parentCode} غير موجود");
        }
    }

    [Fact]
    public void HoldingAccounts_IntercompanyFlagOnCorrectGroups()
    {
        var intercompany = DefaultCoASeed.HoldingAccounts.Where(a => a.Intercompany).Select(a => a.Code).ToList();
        intercompany.Should().Contain("1220");
        intercompany.Should().Contain("1221");
        intercompany.Should().Contain("1222");
        intercompany.Should().Contain("1223");
        intercompany.Should().Contain("2220");
        intercompany.Should().Contain("4400");
        intercompany.Should().Contain("5200");
        intercompany.Should().NotContain("1101");
        intercompany.Should().NotContain("1210");
    }

    [Fact]
    public async Task EnsureDefaultCoAAsync_SeedsAllAccounts()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var accountRepo = new FakeAccountRepository(new List<Account>());

        await accountRepo.EnsureDefaultCoAAsync(tenantId, companyId, CancellationToken.None);
        var accounts = await accountRepo.ListByCompanyAsync(tenantId, companyId, CancellationToken.None);
        accounts.Count.Should().Be(DefaultCoASeed.HoldingAccounts.Length, "كل الحسابات تضاف");
        accounts.Should().Contain(a => a.Code == "0000");
        accounts.Should().Contain(a => a.Code == "1210");
        accounts.Should().Contain(a => a.Code == "4111");
        accounts.Should().Contain(a => a.Code == "4112");
        accounts.Should().Contain(a => a.Code == "4113");
        accounts.Should().Contain(a => a.Code == "4114");
    }

    [Fact]
    public async Task EnsureDefaultCoAAsync_PreservesHierarchy()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var accountRepo = new FakeAccountRepository(new List<Account>());
        await accountRepo.EnsureDefaultCoAAsync(tenantId, companyId, CancellationToken.None);
        var accounts = await accountRepo.ListByCompanyAsync(tenantId, companyId, CancellationToken.None);

        // 1101 (sub) should have parent 1100 (which has parent 1000)
        var sub = accounts.First(a => a.Code == "1101");
        var parent = accounts.First(a => a.Id == sub.ParentAccountId);
        parent.Code.Should().Be("1100");
        var grandparent = accounts.First(a => a.Id == parent.ParentAccountId);
        grandparent.Code.Should().Be("1000");
    }

    [Fact]
    public async Task EnsureDefaultCoAAsync_IsIdempotent()
    {
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var accountRepo = new FakeAccountRepository(new List<Account>());

        await accountRepo.EnsureDefaultCoAAsync(tenantId, companyId, CancellationToken.None);
        var first = (await accountRepo.ListByCompanyAsync(tenantId, companyId, CancellationToken.None)).Count;
        await accountRepo.EnsureDefaultCoAAsync(tenantId, companyId, CancellationToken.None);
        var second = (await accountRepo.ListByCompanyAsync(tenantId, companyId, CancellationToken.None)).Count;
        second.Should().Be(first);
    }

    [Fact]
    public async Task CloneCoAFromCompany_PreservesStructureAndFlags()
    {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var accounts = new List<Account>();
        var idByCode = new Dictionary<string, Guid>();
        foreach (var (code, name, type, parentCode, postable, intercompany) in DefaultCoASeed.HoldingAccounts)
        {
            var id = Guid.NewGuid();
            idByCode[code] = id;
            accounts.Add(new Account
            {
                Id = id, TenantId = tenantId, CompanyId = sourceId, Code = code, Name = name,
                Type = type, NormalBalance = type == AccountType.Asset || type == AccountType.Expense ? NormalBalance.Debit : NormalBalance.Credit,
                ParentAccountId = parentCode != null ? idByCode[parentCode] : null,
                IsPostable = postable, IsActive = true, IsIntercompany = intercompany,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
        }

        var accountRepo = new FakeAccountRepository(accounts);
        await accountRepo.CloneCoAFromCompanyAsync(targetId, sourceId, CancellationToken.None);

        var cloned = await accountRepo.ListByCompanyAsync(tenantId, targetId, CancellationToken.None);
        cloned.Count.Should().Be(DefaultCoASeed.HoldingAccounts.Length);
        cloned.Count(a => a.IsIntercompany).Should().Be(14);
        cloned.Count(a => a.ParentAccountId == null).Should().Be(6);
    }
}

internal class FakeAccountRepository : IAccountRepository
{
    private readonly List<Account> _accounts;
    public FakeAccountRepository(List<Account> seed) => _accounts = seed;

    public Task<Account?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_accounts.FirstOrDefault(a => a.Id == id));
    public Task<Account?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct) =>
        Task.FromResult(_accounts.FirstOrDefault(a => a.TenantId == tenantId && a.Code == code));
    public Task<IReadOnlyList<Account>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Account>>(_accounts.Where(a => a.TenantId == tenantId).ToList());
    public Task<IReadOnlyList<Account>> ListChildrenAsync(Guid parentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Account>>(_accounts.Where(a => a.ParentAccountId == parentId).ToList());
    public Task<IReadOnlyList<Account>> ListByCompanyAsync(Guid tenantId, Guid? companyId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Account>>(_accounts.Where(a => a.TenantId == tenantId && a.CompanyId == companyId).ToList());
    public Task InsertAsync(Account account, CancellationToken ct) { _accounts.Add(account); return Task.CompletedTask; }
    public Task UpdateAsync(Account account, CancellationToken ct)
    {
        var idx = _accounts.FindIndex(a => a.Id == account.Id);
        if (idx >= 0) _accounts[idx] = account;
        return Task.CompletedTask;
    }
    public Task<int> CountPostingsAsync(Guid accountId, CancellationToken ct) => Task.FromResult(0);
    public async Task EnsureDefaultCoAAsync(Guid tenantId, Guid companyId, CancellationToken ct)
    {
        if (_accounts.Any(a => a.Code == "0000")) return;
        var all = DefaultCoASeed.HoldingAccounts.ToList();
        var idByCode = new Dictionary<string, Guid>();
        var added = 0;
        while (added < all.Count)
        {
            var pass = 0;
            foreach (var (code, name, type, parentCode, postable, intercompany) in all)
            {
                if (idByCode.ContainsKey(code)) continue;
                Guid? parentId = null;
                if (parentCode != null)
                {
                    if (!idByCode.TryGetValue(parentCode, out var p)) continue;
                    parentId = p;
                }
                _accounts.Add(new Account { Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = companyId, Code = code, Name = name, Type = type, NormalBalance = type == AccountType.Asset || type == AccountType.Expense ? NormalBalance.Debit : NormalBalance.Credit, ParentAccountId = parentId, IsPostable = postable, IsActive = true, IsIntercompany = intercompany, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
                idByCode[code] = _accounts.Last().Id;
                pass++;
            }
            if (pass == 0) break;
            added += pass;
        }
    }
    public async Task CloneCoAFromCompanyAsync(Guid targetCompanyId, Guid sourceCompanyId, CancellationToken ct)
    {
        var src = _accounts.Where(a => a.CompanyId == sourceCompanyId).ToList();
        if (src.Count == 0) return;
        var tenantId = src.First().TenantId;
        var idMap = new Dictionary<Guid, Guid>();
        foreach (var a in src) idMap[a.Id] = Guid.NewGuid();
        foreach (var a in src.Where(a => a.ParentAccountId == null))
            _accounts.Add(new Account { Id = idMap[a.Id], TenantId = tenantId, CompanyId = targetCompanyId, Code = a.Code, Name = a.Name, Type = a.Type, NormalBalance = a.NormalBalance, IsPostable = a.IsPostable, IsActive = a.IsActive, IsIntercompany = a.IsIntercompany, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        foreach (var a in src.Where(a => a.ParentAccountId != null))
        {
            Guid? newParent = a.ParentAccountId.HasValue && idMap.TryGetValue(a.ParentAccountId.Value, out var p) ? p : null;
            _accounts.Add(new Account { Id = idMap[a.Id], TenantId = tenantId, CompanyId = targetCompanyId, Code = a.Code, Name = a.Name, Type = a.Type, NormalBalance = a.NormalBalance, ParentAccountId = newParent, IsPostable = a.IsPostable, IsActive = a.IsActive, IsIntercompany = a.IsIntercompany, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        }
        await Task.CompletedTask;
    }
}
