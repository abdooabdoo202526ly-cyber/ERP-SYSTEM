using ERPSystem.Tests.Fakes;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Finance.Infrastructure;
using ERPSystem.Modules.Payments.Application;
using ERPSystem.Modules.Payments.Application.Services;
using ERPSystem.Modules.Payments.Entities;
using ERPSystem.Modules.Payments.Infrastructure;
using ERPSystem.Modules.Procurement.Entities;
using ERPSystem.Modules.Procurement.Infrastructure;
using ERPSystem.Shared.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERPSystem.Tests.Payments;

public class PaymentServiceTests
{
    private static (PaymentService svc,
                    FakePaymentRepository payments,
                    FakePaymentSequenceRepository seq,
                    FakeVendorRepository vendors,
                    FakeAccountRepository accounts,
                    FakeJournalEntryRepository entries)
        Build()
    {
        var payments = new FakePaymentRepository();
        var seq = new FakePaymentSequenceRepository();
        var vendors = new FakeVendorRepository();
        var accounts = new FakeAccountRepository();
        var entries = new FakeJournalEntryRepository();
        var db = new NullDbConnectionFactory();
        var svc = new PaymentService(
            payments, seq, vendors, accounts, entries, db, new FakeDefaultAccountsHelper(), NullLogger<PaymentService>.Instance);
        return (svc, payments, seq, vendors, accounts, entries);
    }

    private static async Task<Vendor> CreateVendorAsync(FakeVendorRepository repo, string code = "V-001")
    {
        var v = new Vendor
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Code = code,
            Name = "مورّد اختبار",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            UpdatedAt = DateTime.UtcNow,
        };
        await repo.InsertAsync(v, CancellationToken.None);
        return v;
    }

    private static void SeedCoA(FakeAccountRepository accounts, Guid tenantId)
    {
        accounts.Add(new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "1210", Name = "النقدية",
            Type = AccountType.Asset, NormalBalance = NormalBalance.Debit, IsPostable = true, IsActive = true
        });
        accounts.Add(new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "2210", Name = "دائنون لموردين",
            Type = AccountType.Liability, NormalBalance = NormalBalance.Credit, IsPostable = true, IsActive = true
        });
        accounts.Add(new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "1230", Name = "ذمم مدينة",
            Type = AccountType.Asset, NormalBalance = NormalBalance.Debit, IsPostable = true, IsActive = true
        });
    }

    [Fact]
    public async Task Create_VendorPayment_DefaultsToDraft_WithGeneratedNumber()
    {
        var (svc, _, _, vendors, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vendor = await CreateVendorAsync(vendors);
        vendor.TenantId = tenantId;
        await vendors.UpdateAsync(vendor, CancellationToken.None);

        var r = await svc.CreateAsync(tenantId, userId, new CreatePaymentRequest
        {
            PartyType = PaymentPartyTypes.Vendor,
            PartyId = vendor.Id,
            Amount = 1000m,
            PaymentMethod = PaymentMethods.Bank,
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(PaymentStatus.Draft);
        r.Value.PaymentNumber.Should().StartWith("PAY-");
        r.Value.Amount.Should().Be(1000m);
        r.Value.Allocations.Count.Should().Be(0);
        r.Value.OnAccountAmount.Should().Be(1000m, "On Account = amount - allocations");
    }

    [Fact]
    public async Task Create_UnknownVendor_Fails()
    {
        var (svc, _, _, _, _, _) = Build();
        var r = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreatePaymentRequest
        {
            PartyType = PaymentPartyTypes.Vendor,
            PartyId = Guid.NewGuid(),
            Amount = 100m,
            PaymentMethod = PaymentMethods.Cash,
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PaymentErrorCode.NotFoundParty);
    }

    [Fact]
    public async Task Create_CustomerPartyType_NotYetSupported()
    {
        var (svc, _, _, _, _, _) = Build();
        var r = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreatePaymentRequest
        {
            PartyType = PaymentPartyTypes.Customer,
            PartyId = Guid.NewGuid(),
            Amount = 100m,
            PaymentMethod = PaymentMethods.Cash,
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PaymentErrorCode.NotFoundParty);
    }

    [Fact]
    public async Task Create_ZeroAmountAllocation_Fails()
    {
        var (svc, _, _, vendors, _, _) = Build();
        var vendor = await CreateVendorAsync(vendors);
        vendor.TenantId = Guid.NewGuid();
        await vendors.UpdateAsync(vendor, CancellationToken.None);

        var r = await svc.CreateAsync(vendor.TenantId, Guid.NewGuid(), new CreatePaymentRequest
        {
            PartyType = PaymentPartyTypes.Vendor,
            PartyId = vendor.Id,
            Amount = 100m,
            PaymentMethod = PaymentMethods.Cash,
            Allocations = new List<CreatePaymentAllocationRequest>
            {
                new() { RefType = PaymentRefTypes.VendorBill, RefId = Guid.NewGuid(), AmountApplied = 0 }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PaymentErrorCode.ValidationError);
    }

    [Fact]
    public async Task Create_OverAllocation_Fails()
    {
        var (svc, _, _, vendors, _, _) = Build();
        var vendor = await CreateVendorAsync(vendors);
        vendor.TenantId = Guid.NewGuid();
        await vendors.UpdateAsync(vendor, CancellationToken.None);

        var r = await svc.CreateAsync(vendor.TenantId, Guid.NewGuid(), new CreatePaymentRequest
        {
            PartyType = PaymentPartyTypes.Vendor,
            PartyId = vendor.Id,
            Amount = 100m,
            PaymentMethod = PaymentMethods.Cash,
            Allocations = new List<CreatePaymentAllocationRequest>
            {
                new() { RefType = PaymentRefTypes.VendorBill, RefId = Guid.NewGuid(), AmountApplied = 200m }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PaymentErrorCode.OverAllocation);
    }

    [Fact]
    public async Task Create_WrongTenantVendor_Fails()
    {
        var (svc, _, _, vendors, _, _) = Build();
        var vendor = await CreateVendorAsync(vendors);
        // Use a different tenant than the vendor's
        var r = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreatePaymentRequest
        {
            PartyType = PaymentPartyTypes.Vendor,
            PartyId = vendor.Id,
            Amount = 100m,
            PaymentMethod = PaymentMethods.Cash,
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PaymentErrorCode.NotFoundParty);
    }

    [Fact]
    public async Task Post_VendorPayment_CreatesJournalEntry_AndMarksPosted()
    {
        var (svc, _, _, vendors, accounts, entries) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedCoA(accounts, tenantId);
        var vendor = await CreateVendorAsync(vendors);
        vendor.TenantId = tenantId;
        await vendors.UpdateAsync(vendor, CancellationToken.None);

        var create = await svc.CreateAsync(tenantId, userId, new CreatePaymentRequest
        {
            PartyType = PaymentPartyTypes.Vendor,
            PartyId = vendor.Id,
            Amount = 1500m,
            PaymentMethod = PaymentMethods.Bank,
        }, CancellationToken.None);
        create.Succeeded.Should().BeTrue();

        var r = await svc.PostAsync(tenantId, userId, create.Value!.Id, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(PaymentStatus.Posted);
        r.Value.JournalEntryId.Should().NotBeNull();
        r.Value.PostedAt.Should().NotBeNull();

        entries.Inserted.Count.Should().Be(1);
        var entry = entries.Inserted[0];
        entry.Status.Should().Be(JournalEntryStatus.Posted);
        entry.Lines.Sum(l => l.Debit).Should().Be(entry.Lines.Sum(l => l.Credit));
        entry.Lines.Sum(l => l.Debit).Should().Be(1500m);
    }

    [Fact]
    public async Task Post_AlreadyPosted_Fails()
    {
        var (svc, _, _, vendors, accounts, _) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedCoA(accounts, tenantId);
        var vendor = await CreateVendorAsync(vendors);
        vendor.TenantId = tenantId;
        await vendors.UpdateAsync(vendor, CancellationToken.None);

        var create = await svc.CreateAsync(tenantId, userId, new CreatePaymentRequest
        {
            PartyType = PaymentPartyTypes.Vendor,
            PartyId = vendor.Id, Amount = 100m, PaymentMethod = PaymentMethods.Cash,
        }, CancellationToken.None);
        await svc.PostAsync(tenantId, userId, create.Value!.Id, CancellationToken.None);

        var r2 = await svc.PostAsync(tenantId, userId, create.Value!.Id, CancellationToken.None);
        r2.Succeeded.Should().BeFalse();
        r2.ErrorCode.Should().Be(PaymentErrorCode.InvalidStatus);
    }

    [Fact]
    public async Task Post_MissingAccounts_Fails()
    {
        var (svc, _, _, vendors, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var vendor = await CreateVendorAsync(vendors);
        vendor.TenantId = tenantId;
        await vendors.UpdateAsync(vendor, CancellationToken.None);

        var create = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreatePaymentRequest
        {
            PartyType = PaymentPartyTypes.Vendor,
            PartyId = vendor.Id, Amount = 100m, PaymentMethod = PaymentMethods.Cash,
        }, CancellationToken.None);

        var r = await svc.PostAsync(tenantId, Guid.NewGuid(), create.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PaymentErrorCode.NotFound);
    }

    [Fact]
    public async Task GetById_WrongTenant_Fails()
    {
        var (svc, _, _, vendors, _, _) = Build();
        var vendor = await CreateVendorAsync(vendors);
        var create = await svc.CreateAsync(vendor.TenantId, Guid.NewGuid(), new CreatePaymentRequest
        {
            PartyType = PaymentPartyTypes.Vendor, PartyId = vendor.Id, Amount = 100m, PaymentMethod = PaymentMethods.Cash,
        }, CancellationToken.None);

        var r = await svc.GetByIdAsync(Guid.NewGuid(), create.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PaymentErrorCode.NotFound);
    }

    [Fact]
    public async Task List_FiltersByPartyType()
    {
        var (svc, _, _, vendors, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vendor = await CreateVendorAsync(vendors);
        vendor.TenantId = tenantId;
        await vendors.UpdateAsync(vendor, CancellationToken.None);

        await svc.CreateAsync(tenantId, userId, new CreatePaymentRequest
        {
            PartyType = PaymentPartyTypes.Vendor, PartyId = vendor.Id, Amount = 100m, PaymentMethod = PaymentMethods.Cash,
        }, CancellationToken.None);
        await svc.CreateAsync(tenantId, userId, new CreatePaymentRequest
        {
            PartyType = PaymentPartyTypes.Vendor, PartyId = vendor.Id, Amount = 200m, PaymentMethod = PaymentMethods.Bank,
        }, CancellationToken.None);

        var r = await svc.ListAsync(tenantId, PaymentPartyTypes.Vendor, vendor.Id, null, 0, 50, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Count.Should().Be(2);
    }

    [Fact]
    public async Task List_FiltersByStatus()
    {
        var (svc, _, _, vendors, accounts, _) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedCoA(accounts, tenantId);
        var vendor = await CreateVendorAsync(vendors);
        vendor.TenantId = tenantId;
        await vendors.UpdateAsync(vendor, CancellationToken.None);

        var draft = await svc.CreateAsync(tenantId, userId, new CreatePaymentRequest
        {
            PartyType = PaymentPartyTypes.Vendor, PartyId = vendor.Id, Amount = 100m, PaymentMethod = PaymentMethods.Cash,
        }, CancellationToken.None);
        var posted = await svc.CreateAsync(tenantId, userId, new CreatePaymentRequest
        {
            PartyType = PaymentPartyTypes.Vendor, PartyId = vendor.Id, Amount = 200m, PaymentMethod = PaymentMethods.Cash,
        }, CancellationToken.None);
        await svc.PostAsync(tenantId, userId, posted.Value!.Id, CancellationToken.None);

        var r = await svc.ListAsync(tenantId, null, null, PaymentStatus.Draft, 0, 50, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Count.Should().Be(1);
        r.Value[0].Id.Should().Be(draft.Value!.Id);
    }

    [Fact]
    public async Task Allocate_OnDraftPayment_Fails()
    {
        var (svc, _, _, vendors, _, _) = Build();
        var vendor = await CreateVendorAsync(vendors);
        var create = await svc.CreateAsync(vendor.TenantId, Guid.NewGuid(), new CreatePaymentRequest
        {
            PartyType = PaymentPartyTypes.Vendor, PartyId = vendor.Id, Amount = 100m, PaymentMethod = PaymentMethods.Cash,
        }, CancellationToken.None);

        // Allocation requires Posted status
        var r = await svc.AllocateAsync(vendor.TenantId, Guid.NewGuid(), create.Value!.Id, new AllocatePaymentRequest
        {
            Allocations = new List<CreatePaymentAllocationRequest>
            {
                new() { RefType = PaymentRefTypes.VendorBill, RefId = Guid.NewGuid(), AmountApplied = 50m }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PaymentErrorCode.InvalidStatus);
    }
}

// ============== Fakes ==============

internal class FakePaymentRepository : IPaymentRepository
{
    private readonly Dictionary<Guid, Payment> _items = new();
    private readonly object _lock = new();

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        lock (_lock) { return Task.FromResult(_items.TryGetValue(id, out var p) ? Clone(p) : null); }
    }

    public Task<Payment?> GetByPaymentNumberAsync(Guid tenantId, string paymentNumber, CancellationToken ct)
    {
        lock (_lock)
        {
            return Task.FromResult(_items.Values.FirstOrDefault(p => p.TenantId == tenantId && p.PaymentNumber == paymentNumber));
        }
    }

    public Task<IReadOnlyList<Payment>> ListAsync(Guid tenantId, string? partyType, Guid? partyId, PaymentStatus? status, int skip, int take, CancellationToken ct)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<Payment>>(
                _items.Values
                    .Where(p => p.TenantId == tenantId)
                    .Where(p => partyType == null || p.PartyType == partyType)
                    .Where(p => partyId == null || p.PartyId == partyId)
                    .Where(p => status == null || p.Status == status)
                    .OrderBy(p => p.PaymentNumber)
                    .Skip(skip).Take(take)
                    .ToList());
        }
    }

    public Task InsertAsync(Payment p, CancellationToken ct)
    {
        lock (_lock) { _items[p.Id] = Clone(p); }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Payment p, CancellationToken ct)
    {
        lock (_lock) { _items[p.Id] = Clone(p); }
        return Task.CompletedTask;
    }

    public Task InsertAllocationsAsync(Guid tenantId, Guid paymentId, IEnumerable<PaymentAllocation> allocations, CancellationToken ct)
    {
        lock (_lock)
        {
            if (_items.TryGetValue(paymentId, out var p))
                p.Allocations = allocations.ToList();
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PaymentAllocation>> GetAllocationsAsync(Guid paymentId, CancellationToken ct)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<PaymentAllocation>>(
                _items.TryGetValue(paymentId, out var p) ? p.Allocations.ToList() : new List<PaymentAllocation>());
        }
    }

    public Task<decimal> SumAllocationsForRefAsync(Guid tenantId, string refType, Guid refId, CancellationToken ct)
    {
        lock (_lock)
        {
            var total = _items.Values
                .Where(p => p.TenantId == tenantId)
                .SelectMany(p => p.Allocations)
                .Where(a => a.RefType == refType && a.RefId == refId)
                .Sum(a => a.AmountApplied);
            return Task.FromResult(total);
        }
    }

    private static Payment Clone(Payment p) => new()
    {
        Id = p.Id, TenantId = p.TenantId, CompanyId = p.CompanyId, PartyType = p.PartyType, PartyId = p.PartyId,
        PaymentNumber = p.PaymentNumber, PaymentDate = p.PaymentDate, Amount = p.Amount, CurrencyCode = p.CurrencyCode,
        PaymentMethod = p.PaymentMethod, BankAccountId = p.BankAccountId, Notes = p.Notes,
        Status = p.Status, PostedAt = p.PostedAt, PostedBy = p.PostedBy,
        CreatedAt = p.CreatedAt, CreatedBy = p.CreatedBy, UpdatedAt = p.UpdatedAt, UpdatedBy = p.UpdatedBy,
        JournalEntryId = p.JournalEntryId,
        Allocations = p.Allocations.ToList()
    };
}

internal class FakePaymentSequenceRepository : IPaymentSequenceRepository
{
    private readonly Dictionary<Guid, int> _counters = new();
    public Task<string> GetNextPaymentNumberAsync(Guid tenantId, CancellationToken ct)
    {
        if (!_counters.TryGetValue(tenantId, out var n)) n = 0;
        n++;
        _counters[tenantId] = n;
        var year = DateTime.UtcNow.Year;
        return Task.FromResult($"PAY-{year}-{n:D4}");
    }
}

internal class FakeVendorRepository : IVendorRepository
{
    private readonly Dictionary<Guid, Vendor> _items = new();
    public Task<Vendor?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var v) ? v : null);
    public Task<Vendor?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct) =>
        Task.FromResult(_items.Values.FirstOrDefault(v => v.TenantId == tenantId && v.Code == code));
    public Task<IReadOnlyList<Vendor>> ListAsync(Guid tenantId, bool includeInactive, int skip, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Vendor>>(_items.Values.Where(v => v.TenantId == tenantId).ToList());
    public Task<IReadOnlyList<Vendor>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Vendor>>(_items.Values.Where(v => ids.Contains(v.Id)).ToList());
    public Task InsertAsync(Vendor v, CancellationToken ct) { _items[v.Id] = v; return Task.CompletedTask; }
    public Task UpdateAsync(Vendor v, CancellationToken ct) { _items[v.Id] = v; return Task.CompletedTask; }
}

internal class FakeAccountRepository : IAccountRepository
{
    private readonly List<Account> _accounts = new();
    public void Add(Account a) => _accounts.Add(a);
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
    public Task EnsureDefaultCoAAsync(Guid tenantId, Guid companyId, CancellationToken ct) => Task.CompletedTask;
    public Task CloneCoAFromCompanyAsync(Guid targetCompanyId, Guid sourceCompanyId, CancellationToken ct) => Task.CompletedTask;
}

internal class FakeJournalEntryRepository : IJournalEntryRepository
{
    public List<JournalEntry> Inserted { get; } = new();
    private readonly Dictionary<Guid, JournalEntry> _items = new();
    private int _counter;

    public Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var e) ? e : null);
    public Task<JournalEntry?> GetWithLinesAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var e) ? e : null);
    public Task<bool> EntryNumberExistsAsync(Guid tenantId, string entryNumber, CancellationToken ct) => Task.FromResult(false);
    public Task<string> GetNextEntryNumberAsync(Guid tenantId, CancellationToken ct)
    {
        _counter++;
        return Task.FromResult($"JE-Fake-{_counter:D4}");
    }
    public Task InsertAsync(JournalEntry entry, CancellationToken ct)
    {
        _items[entry.Id] = entry;
        Inserted.Add(entry);
        return Task.CompletedTask;
    }
    public Task UpdateAsync(JournalEntry entry, CancellationToken ct) { _items[entry.Id] = entry; return Task.CompletedTask; }
    public Task<IReadOnlyList<JournalEntry>> ListAsync(Guid tenantId, DateTime? from, DateTime? to, JournalEntryStatus? status, int skip, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<JournalEntry>>(_items.Values.ToList());
}

internal class NullDbConnectionFactory : IDbConnectionFactory
{
    public Task<System.Data.IDbConnection> CreateOltpConnectionAsync(CancellationToken ct = default) =>
        throw new NotImplementedException("NullDbConnectionFactory — bill validation is SQL-bound and not exercised in this test");
    public Task<System.Data.IDbConnection> CreateEventStoreConnectionAsync(CancellationToken ct = default) =>
        throw new NotImplementedException();
}
