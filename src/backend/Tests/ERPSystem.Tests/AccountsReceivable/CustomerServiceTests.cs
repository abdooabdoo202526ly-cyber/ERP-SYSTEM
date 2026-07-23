using ERPSystem.Modules.AccountsReceivable.Application;
using ERPSystem.Modules.AccountsReceivable.Application.Services;
using ERPSystem.Modules.AccountsReceivable.Entities;
using ERPSystem.Modules.AccountsReceivable.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERPSystem.Tests.AccountsReceivable;

public class CustomerServiceTests
{
    private static (CustomerService svc, FakeCustomerRepository repo)
        Build()
    {
        var repo = new FakeCustomerRepository();
        var svc = new CustomerService(repo, NullLogger<CustomerService>.Instance);
        return (svc, repo);
    }

    [Fact]
    public async Task Create_DefaultsToActive()
    {
        var (svc, _) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var r = await svc.CreateAsync(tenantId, userId, new CreateCustomerRequest
        {
            Code = "C-001",
            Name = "شركة المستقبل",
            CreditLimit = 10_000m,
            PaymentTermsDays = 30
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.Code.Should().Be("C-001");
        r.Value.Name.Should().Be("شركة المستقبل");
        r.Value.IsActive.Should().BeTrue();
        r.Value.CreditLimit.Should().Be(10_000m);
        r.Value.PaymentTermsDays.Should().Be(30);
        r.Value.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task Create_DuplicateCode_Fails()
    {
        var (svc, _) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var r1 = await svc.CreateAsync(tenantId, userId, new CreateCustomerRequest
        {
            Code = "C-001", Name = "عميل 1", PaymentTermsDays = 30
        }, CancellationToken.None);
        r1.Succeeded.Should().BeTrue();

        var r2 = await svc.CreateAsync(tenantId, userId, new CreateCustomerRequest
        {
            Code = "C-001", Name = "عميل 2", PaymentTermsDays = 60
        }, CancellationToken.None);

        r2.Succeeded.Should().BeFalse();
        r2.ErrorCode.Should().Be(ArErrorCode.AlreadyExists);
    }

    [Fact]
    public async Task Create_SameCodeDifferentTenant_Succeeds()
    {
        var (svc, _) = Build();
        var r1 = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateCustomerRequest
        {
            Code = "C-001", Name = "عميل في مستأجر 1", PaymentTermsDays = 30
        }, CancellationToken.None);
        var r2 = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateCustomerRequest
        {
            Code = "C-001", Name = "عميل في مستأجر 2", PaymentTermsDays = 30
        }, CancellationToken.None);

        r1.Succeeded.Should().BeTrue();
        r2.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Update_ChangesNameAndLimits()
    {
        var (svc, _) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var created = await svc.CreateAsync(tenantId, userId, new CreateCustomerRequest
        {
            Code = "C-002", Name = "قديم", PaymentTermsDays = 30
        }, CancellationToken.None);

        var r = await svc.UpdateAsync(tenantId, userId, created.Value!.Id, new UpdateCustomerRequest
        {
            Name = "جديد", CreditLimit = 50_000m, PaymentTermsDays = 60, IsActive = true
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.Name.Should().Be("جديد");
        r.Value.CreditLimit.Should().Be(50_000m);
        r.Value.PaymentTermsDays.Should().Be(60);
    }

    [Fact]
    public async Task Update_WrongTenant_Fails()
    {
        var (svc, _) = Build();
        var created = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateCustomerRequest
        {
            Code = "C-003", Name = "X", PaymentTermsDays = 30
        }, CancellationToken.None);

        var r = await svc.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), created.Value!.Id, new UpdateCustomerRequest
        {
            Name = "X", PaymentTermsDays = 30, IsActive = true
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ArErrorCode.NotFound);
    }

    [Fact]
    public async Task GetById_WrongTenant_Fails()
    {
        var (svc, _) = Build();
        var created = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateCustomerRequest
        {
            Code = "C-004", Name = "X", PaymentTermsDays = 30
        }, CancellationToken.None);

        var r = await svc.GetByIdAsync(Guid.NewGuid(), created.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ArErrorCode.NotFound);
    }

    [Fact]
    public async Task Deactivate_SetsIsActiveFalse()
    {
        var (svc, _) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var created = await svc.CreateAsync(tenantId, userId, new CreateCustomerRequest
        {
            Code = "C-005", Name = "X", PaymentTermsDays = 30
        }, CancellationToken.None);

        var r = await svc.DeactivateAsync(tenantId, userId, created.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value.Should().BeTrue();

        var fetched = await svc.GetByIdAsync(tenantId, created.Value!.Id, CancellationToken.None);
        fetched.Value!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task List_ExcludesInactiveByDefault()
    {
        var (svc, _) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var c1 = await svc.CreateAsync(tenantId, userId, new CreateCustomerRequest
        {
            Code = "C-1", Name = "Active", PaymentTermsDays = 30
        }, CancellationToken.None);
        var c2 = await svc.CreateAsync(tenantId, userId, new CreateCustomerRequest
        {
            Code = "C-2", Name = "Soon Inactive", PaymentTermsDays = 30
        }, CancellationToken.None);
        await svc.DeactivateAsync(tenantId, userId, c2.Value!.Id, CancellationToken.None);

        var r = await svc.ListAsync(tenantId, includeInactive: false, skip: 0, take: 50, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Count.Should().Be(1, "نُستبعد العملاء غير النشطين افتراضياً");
        r.Value[0].Id.Should().Be(c1.Value!.Id);

        var rAll = await svc.ListAsync(tenantId, includeInactive: true, skip: 0, take: 50, CancellationToken.None);
        rAll.Value!.Count.Should().Be(2, "مع includeInactive نُظهر الجميع");
    }

    [Fact]
    public async Task List_FiltersByTenant()
    {
        var (svc, _) = Build();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        await svc.CreateAsync(t1, Guid.NewGuid(), new CreateCustomerRequest
        {
            Code = "C-T1", Name = "T1", PaymentTermsDays = 30
        }, CancellationToken.None);
        await svc.CreateAsync(t2, Guid.NewGuid(), new CreateCustomerRequest
        {
            Code = "C-T2", Name = "T2", PaymentTermsDays = 30
        }, CancellationToken.None);

        var r1 = await svc.ListAsync(t1, includeInactive: true, skip: 0, take: 50, CancellationToken.None);
        var r2 = await svc.ListAsync(t2, includeInactive: true, skip: 0, take: 50, CancellationToken.None);

        r1.Value!.Count.Should().Be(1);
        r1.Value[0].Code.Should().Be("C-T1");
        r2.Value!.Count.Should().Be(1);
        r2.Value[0].Code.Should().Be("C-T2");
    }
}

// ============== Fakes ==============

internal class FakeCustomerRepository : ICustomerRepository
{
    private readonly Dictionary<Guid, Customer> _items = new();
    private readonly object _lock = new();

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        lock (_lock)
        {
            return Task.FromResult(_items.TryGetValue(id, out var c) ? c : null);
        }
    }

    public Task<Customer?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct)
    {
        lock (_lock)
        {
            return Task.FromResult(_items.Values.FirstOrDefault(c => c.TenantId == tenantId && c.Code == code));
        }
    }

    public Task<IReadOnlyList<Customer>> ListAsync(Guid tenantId, bool includeInactive, int skip, int take, CancellationToken ct)
    {
        lock (_lock)
        {
            var q = _items.Values
                .Where(c => c.TenantId == tenantId)
                .Where(c => includeInactive || c.IsActive)
                .OrderBy(c => c.Code)
                .Skip(skip).Take(take)
                .ToList();
            return Task.FromResult<IReadOnlyList<Customer>>(q);
        }
    }

    public Task InsertAsync(Customer c, CancellationToken ct)
    {
        lock (_lock) { _items[c.Id] = c; }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Customer c, CancellationToken ct)
    {
        lock (_lock) { _items[c.Id] = c; }
        return Task.CompletedTask;
    }
}
