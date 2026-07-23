using ERPSystem.Tests.Fakes;
using ERPSystem.Modules.AccountsReceivable.Application;
using ERPSystem.Modules.AccountsReceivable.Application.Services;
using ERPSystem.Modules.AccountsReceivable.Entities;
using ERPSystem.Modules.AccountsReceivable.Infrastructure;
using ERPSystem.Modules.Finance.Application.Services;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Finance.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERPSystem.Tests.AccountsReceivable;

public class ReceiptServiceTests
{
    private static (ReceiptService svc,
                    FakeCustomerRepository customers,
                    FakeSalesInvoiceRepository invoices,
                    FakeReceiptRepository receipts,
                    FakeArDocumentSequenceRepository seq,
                    FakeJournalEntryService journals,
                    FakeAccountRepository accounts)
        Build()
    {
        var customers = new FakeCustomerRepository();
        var invoices = new FakeSalesInvoiceRepository();
        var receipts = new FakeReceiptRepository();
        var seq = new FakeArDocumentSequenceRepository();
        var journals = new FakeJournalEntryService();
        var accounts = new FakeAccountRepository(new List<Account>());
        var svc = new ReceiptService(
            receipts, customers, invoices, seq, journals, accounts, new FakeDefaultAccountsHelper(), NullLogger<ReceiptService>.Instance);
        return (svc, customers, invoices, receipts, seq, journals, accounts);
    }

    private static async Task<Customer> CreateCustomerAsync(FakeCustomerRepository repo, string code = "C-001", Guid? tenantId = null)
    {
        var c = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? Guid.NewGuid(),
            Code = code,
            Name = "عميل اختبار",
            IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        await repo.InsertAsync(c, CancellationToken.None);
        return c;
    }

    private static async Task<SalesInvoice> CreatePostedInvoiceAsync(
        FakeCustomerRepository customers, FakeSalesInvoiceRepository invoices,
        Guid tenantId, decimal total, decimal paid = 0, SalesInvoiceStatus? overrideStatus = null)
    {
        var cust = await CreateCustomerAsync(customers);
        cust.TenantId = tenantId;
        await customers.UpdateAsync(cust, CancellationToken.None);
        var inv = new SalesInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = Guid.Empty,
            CustomerId = cust.Id,
            InvoiceNumber = "SI-Fake-0001",
            InvoiceDate = DateTime.UtcNow.AddDays(-30),
            DueDate = DateTime.UtcNow,
            CurrencyCode = "LYD",
            ExchangeRate = 1m,
            Subtotal = total,
            TaxAmount = 0,
            TotalAmount = total,
            PaidAmount = paid,
            Outstanding = total - paid,
            Status = overrideStatus ?? SalesInvoiceStatus.Sent,
            PostedAt = (overrideStatus ?? SalesInvoiceStatus.Sent) == SalesInvoiceStatus.Draft ? null : DateTime.UtcNow,
            PostedBy = (overrideStatus ?? SalesInvoiceStatus.Sent) == SalesInvoiceStatus.Draft ? null : Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = Guid.NewGuid(),
        };
        await invoices.InsertAsync(inv, CancellationToken.None);
        return inv;
    }

    private static void SeedDefaultCoA(FakeAccountRepository accounts, Guid tenantId)
    {
        accounts.Add(new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "1210", Name = "النقدية",
            Type = AccountType.Asset, NormalBalance = NormalBalance.Debit, IsPostable = true, IsActive = true
        });
        accounts.Add(new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "1230", Name = "ذمم مدينة",
            Type = AccountType.Asset, NormalBalance = NormalBalance.Debit, IsPostable = true, IsActive = true
        });
    }

    [Fact]
    public async Task Create_GeneratesNumber_AndStoresAllocations()
    {
        var (svc, customers, invoices, receipts, _, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var inv = await CreatePostedInvoiceAsync(customers, invoices, tenantId, total: 1000m);

        var r = await svc.CreateAsync(tenantId, userId, new CreateReceiptRequest
        {
            CustomerId = inv.CustomerId,
            ReceiptDate = DateTime.UtcNow,
            Amount = 1000m,
            CurrencyCode = "LYD",
            PaymentMethod = "Cash",
            Allocations = new List<CreateReceiptAllocationRequest>
            {
                new() { SalesInvoiceId = inv.Id, AmountApplied = 1000m }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.Amount.Should().Be(1000m);
        r.Value.ReceiptNumber.Should().StartWith("RC-");
        r.Value.Allocations.Count.Should().Be(1);

        var stored = await receipts.GetByIdAsync(r.Value.Id, CancellationToken.None);
        stored.Should().NotBeNull();
        stored!.CustomerId.Should().Be(inv.CustomerId);
    }

    [Fact]
    public async Task Create_AllocationsMismatchAmount_Fails()
    {
        var (svc, customers, invoices, _, _, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var inv = await CreatePostedInvoiceAsync(customers, invoices, tenantId, total: 1000m);

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateReceiptRequest
        {
            CustomerId = inv.CustomerId,
            Amount = 1000m,
            CurrencyCode = "LYD",
            Allocations = new List<CreateReceiptAllocationRequest>
            {
                new() { SalesInvoiceId = inv.Id, AmountApplied = 500m }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ArErrorCode.ValidationError);
    }

    [Fact]
    public async Task Create_UnknownInvoice_Fails()
    {
        var (svc, customers, _, _, _, _, _) = Build();
        var cust = await CreateCustomerAsync(customers);

        var r = await svc.CreateAsync(cust.TenantId, Guid.NewGuid(), new CreateReceiptRequest
        {
            CustomerId = cust.Id,
            Amount = 100m,
            CurrencyCode = "LYD",
            Allocations = new List<CreateReceiptAllocationRequest>
            {
                new() { SalesInvoiceId = Guid.NewGuid(), AmountApplied = 100m }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ArErrorCode.NotFound);
    }

    [Fact]
    public async Task Create_InvoiceOfAnotherCustomer_Fails()
    {
        var (svc, customers, invoices, _, _, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var cust1 = await CreateCustomerAsync(customers, "C-1");
        cust1.TenantId = tenantId;
        await customers.UpdateAsync(cust1, CancellationToken.None);
        // invoice belongs to a different customer
        var inv = await CreatePostedInvoiceAsync(customers, invoices, tenantId, total: 500m);
        // recipient is cust1 (different from inv.CustomerId)
        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateReceiptRequest
        {
            CustomerId = cust1.Id,
            Amount = 500m,
            CurrencyCode = "LYD",
            Allocations = new List<CreateReceiptAllocationRequest>
            {
                new() { SalesInvoiceId = inv.Id, AmountApplied = 500m }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ArErrorCode.BusinessRuleViolation);
    }

    [Fact]
    public async Task Create_CancelledInvoice_Fails()
    {
        var (svc, customers, invoices, _, _, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var inv = await CreatePostedInvoiceAsync(customers, invoices, tenantId, total: 500m,
            overrideStatus: SalesInvoiceStatus.Cancelled);

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateReceiptRequest
        {
            CustomerId = inv.CustomerId,
            Amount = 500m,
            CurrencyCode = "LYD",
            Allocations = new List<CreateReceiptAllocationRequest>
            {
                new() { SalesInvoiceId = inv.Id, AmountApplied = 500m }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ArErrorCode.BusinessRuleViolation);
    }

    [Fact]
    public async Task Create_AllocationExceedsOutstanding_Fails()
    {
        var (svc, customers, invoices, _, _, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var inv = await CreatePostedInvoiceAsync(customers, invoices, tenantId, total: 500m, paid: 400m);

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateReceiptRequest
        {
            CustomerId = inv.CustomerId,
            Amount = 200m, // بقية 100 فقط متبقية
            CurrencyCode = "LYD",
            Allocations = new List<CreateReceiptAllocationRequest>
            {
                new() { SalesInvoiceId = inv.Id, AmountApplied = 200m }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ArErrorCode.BusinessRuleViolation);
    }

    [Fact]
    public async Task Create_InvalidPaymentMethod_Fails()
    {
        var (svc, customers, invoices, _, _, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var inv = await CreatePostedInvoiceAsync(customers, invoices, tenantId, total: 1000m);

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateReceiptRequest
        {
            CustomerId = inv.CustomerId,
            Amount = 1000m,
            CurrencyCode = "LYD",
            PaymentMethod = "BitCoin", // غير معتمد
            Allocations = new List<CreateReceiptAllocationRequest>
            {
                new() { SalesInvoiceId = inv.Id, AmountApplied = 1000m }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ArErrorCode.ValidationError);
    }

    [Fact]
    public async Task Post_UpdatesInvoicePaidAmount_AndSetsToPaid()
    {
        var (svc, customers, invoices, _, _, journals, accounts) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedDefaultCoA(accounts, tenantId);
        var inv = await CreatePostedInvoiceAsync(customers, invoices, tenantId, total: 1000m);

        var create = await svc.CreateAsync(tenantId, userId, new CreateReceiptRequest
        {
            CustomerId = inv.CustomerId,
            Amount = 1000m,
            CurrencyCode = "LYD",
            PaymentMethod = "Bank",
            Allocations = new List<CreateReceiptAllocationRequest>
            {
                new() { SalesInvoiceId = inv.Id, AmountApplied = 1000m }
            }
        }, CancellationToken.None);
        create.Succeeded.Should().BeTrue();

        var r = await svc.PostAsync(tenantId, userId, create.Value!.Id, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.PostedAt.Should().NotBeNull();
        r.Value.JournalEntryId.Should().NotBeNull();
        journals.Posted.Count.Should().BeGreaterOrEqualTo(1);

        // re-read invoice
        var refreshed = await invoices.GetByIdAsync(inv.Id, CancellationToken.None);
        refreshed!.PaidAmount.Should().Be(1000m);
        refreshed.Outstanding.Should().Be(0m);
        refreshed.Status.Should().Be(SalesInvoiceStatus.Paid);
    }

    [Fact]
    public async Task Post_PartialAllocations_SetsInvoicePartiallyPaid()
    {
        var (svc, customers, invoices, _, _, _, accounts) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedDefaultCoA(accounts, tenantId);
        var inv = await CreatePostedInvoiceAsync(customers, invoices, tenantId, total: 1000m);

        var create = await svc.CreateAsync(tenantId, userId, new CreateReceiptRequest
        {
            CustomerId = inv.CustomerId,
            Amount = 400m,
            CurrencyCode = "LYD",
            PaymentMethod = "Cash",
            Allocations = new List<CreateReceiptAllocationRequest>
            {
                new() { SalesInvoiceId = inv.Id, AmountApplied = 400m }
            }
        }, CancellationToken.None);

        var r = await svc.PostAsync(tenantId, userId, create.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();

        var refreshed = await invoices.GetByIdAsync(inv.Id, CancellationToken.None);
        refreshed!.PaidAmount.Should().Be(400m);
        refreshed.Outstanding.Should().Be(600m);
        refreshed.Status.Should().Be(SalesInvoiceStatus.PartiallyPaid);
    }

    [Fact]
    public async Task Post_Twice_Fails()
    {
        var (svc, customers, invoices, _, _, _, accounts) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedDefaultCoA(accounts, tenantId);
        var inv = await CreatePostedInvoiceAsync(customers, invoices, tenantId, total: 500m);

        var create = await svc.CreateAsync(tenantId, userId, new CreateReceiptRequest
        {
            CustomerId = inv.CustomerId,
            Amount = 500m,
            CurrencyCode = "LYD",
            Allocations = new List<CreateReceiptAllocationRequest>
            {
                new() { SalesInvoiceId = inv.Id, AmountApplied = 500m }
            }
        }, CancellationToken.None);
        await svc.PostAsync(tenantId, userId, create.Value!.Id, CancellationToken.None);

        var r2 = await svc.PostAsync(tenantId, userId, create.Value!.Id, CancellationToken.None);
        r2.Succeeded.Should().BeFalse();
        r2.ErrorCode.Should().Be(ArErrorCode.InvalidStatusTransition);
    }

    [Fact]
    public async Task Reverse_RevertsInvoicePayment()
    {
        var (svc, customers, invoices, _, _, _, accounts) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedDefaultCoA(accounts, tenantId);
        var inv = await CreatePostedInvoiceAsync(customers, invoices, tenantId, total: 1000m);

        var create = await svc.CreateAsync(tenantId, userId, new CreateReceiptRequest
        {
            CustomerId = inv.CustomerId,
            Amount = 1000m,
            CurrencyCode = "LYD",
            Allocations = new List<CreateReceiptAllocationRequest>
            {
                new() { SalesInvoiceId = inv.Id, AmountApplied = 1000m }
            }
        }, CancellationToken.None);
        await svc.PostAsync(tenantId, userId, create.Value!.Id, CancellationToken.None);

        var r = await svc.ReverseAsync(tenantId, userId, create.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();

        var refreshed = await invoices.GetByIdAsync(inv.Id, CancellationToken.None);
        refreshed!.PaidAmount.Should().Be(0m);
        refreshed.Outstanding.Should().Be(1000m);
        // back to Sent (was Posted) because we revert the full payment
        refreshed.Status.Should().Be(SalesInvoiceStatus.Sent);
    }

    [Fact]
    public async Task Reverse_DraftReceipt_Fails()
    {
        var (svc, customers, invoices, _, _, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var inv = await CreatePostedInvoiceAsync(customers, invoices, tenantId, total: 1000m);

        var create = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateReceiptRequest
        {
            CustomerId = inv.CustomerId,
            Amount = 1000m,
            CurrencyCode = "LYD",
            Allocations = new List<CreateReceiptAllocationRequest>
            {
                new() { SalesInvoiceId = inv.Id, AmountApplied = 1000m }
            }
        }, CancellationToken.None);

        var r = await svc.ReverseAsync(tenantId, Guid.NewGuid(), create.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ArErrorCode.InvalidStatusTransition);
    }

    [Fact]
    public async Task GetById_WrongTenant_Fails()
    {
        var (svc, customers, invoices, _, _, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var inv = await CreatePostedInvoiceAsync(customers, invoices, tenantId, total: 500m);

        var create = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateReceiptRequest
        {
            CustomerId = inv.CustomerId,
            Amount = 500m,
            CurrencyCode = "LYD",
            Allocations = new List<CreateReceiptAllocationRequest>
            {
                new() { SalesInvoiceId = inv.Id, AmountApplied = 500m }
            }
        }, CancellationToken.None);

        var r = await svc.GetByIdAsync(Guid.NewGuid(), create.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ArErrorCode.NotFound);
    }
}

// ============== Fakes ==============

internal class FakeReceiptRepository : IReceiptRepository
{
    private readonly Dictionary<Guid, Receipt> _items = new();
    private readonly object _lock = new();

    public Task<Receipt?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        lock (_lock) { return Task.FromResult(_items.TryGetValue(id, out var r) ? r : null); }
    }

    public Task<Receipt?> GetByReceiptNumberAsync(Guid tenantId, string receiptNumber, CancellationToken ct)
    {
        lock (_lock)
        {
            return Task.FromResult(_items.Values.FirstOrDefault(r => r.TenantId == tenantId && r.ReceiptNumber == receiptNumber));
        }
    }

    public Task<IReadOnlyList<Receipt>> ListAsync(Guid tenantId, Guid? customerId, int skip, int take, CancellationToken ct)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<Receipt>>(
                _items.Values
                    .Where(r => r.TenantId == tenantId && (customerId == null || r.CustomerId == customerId))
                    .OrderBy(r => r.ReceiptNumber)
                    .Skip(skip).Take(take)
                    .ToList());
        }
    }

    public Task InsertAsync(Receipt r, CancellationToken ct)
    {
        lock (_lock) { _items[r.Id] = r; }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Receipt r, CancellationToken ct)
    {
        lock (_lock) { _items[r.Id] = r; }
        return Task.CompletedTask;
    }

    public Task InsertAllocationsAsync(Guid tenantId, Guid receiptId, IEnumerable<ReceiptAllocation> allocations, CancellationToken ct)
    {
        lock (_lock)
        {
            if (_items.TryGetValue(receiptId, out var r))
                r.Allocations = allocations.ToList();
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReceiptAllocation>> GetAllocationsAsync(Guid receiptId, CancellationToken ct)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ReceiptAllocation>>(
                _items.TryGetValue(receiptId, out var r) ? r.Allocations.ToList() : new List<ReceiptAllocation>());
        }
    }
}
