using ERPSystem.Tests.Fakes;
using ERPSystem.Modules.AccountsReceivable.Application;
using ERPSystem.Modules.AccountsReceivable.Application.Services;
using ERPSystem.Modules.AccountsReceivable.Entities;
using ERPSystem.Modules.AccountsReceivable.Infrastructure;
using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Application.Services;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Finance.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERPSystem.Tests.AccountsReceivable;

public class SalesInvoiceServiceTests
{
    private static (SalesInvoiceService svc,
                    FakeCustomerRepository customers,
                    FakeSalesInvoiceRepository invoices,
                    FakeArDocumentSequenceRepository seq,
                    FakeJournalEntryService journals,
                    FakeAccountRepository accounts)
        Build()
    {
        var customers = new FakeCustomerRepository();
        var invoices = new FakeSalesInvoiceRepository();
        var seq = new FakeArDocumentSequenceRepository();
        var journals = new FakeJournalEntryService();
        var accounts = new FakeAccountRepository(new List<Account>());
        var svc = new SalesInvoiceService(
            invoices, customers, seq, journals, accounts, new FakeDefaultAccountsHelper(), NullLogger<SalesInvoiceService>.Instance);
        return (svc, customers, invoices, seq, journals, accounts);
    }

    private static async Task<Customer> CreateCustomerAsync(FakeCustomerRepository repo, string code = "C-001", Guid? tenantId = null)
    {
        var c = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId ?? Guid.NewGuid(),
            Code = code,
            Name = "عميل اختبار",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await repo.InsertAsync(c, CancellationToken.None);
        return c;
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
        accounts.Add(new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "5110", Name = "إيرادات المشاريع",
            Type = AccountType.Revenue, NormalBalance = NormalBalance.Credit, IsPostable = true, IsActive = true
        });
    }

    [Fact]
    public async Task Create_DefaultsToDraft_AndCalculatesTotals()
    {
        var (svc, customers, invoices, _, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cust = await CreateCustomerAsync(customers, tenantId: tenantId);
        var req = new CreateSalesInvoiceRequest
        {
            CustomerId = cust.Id,
            InvoiceDate = DateTime.UtcNow,
            CurrencyCode = "LYD",
            Lines = new List<CreateSalesInvoiceLineRequest>
            {
                new() { Description = "بند 1", Quantity = 2, UnitPrice = 100m, TaxRate = 0.15m },
                new() { Description = "بند 2", Quantity = 1, UnitPrice = 50m,  TaxRate = 0m },
            }
        };

        var r = await svc.CreateAsync(tenantId, userId, req, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(SalesInvoiceStatus.Draft);
        r.Value.Subtotal.Should().Be(250m, "2*100 + 1*50");
        r.Value.TaxAmount.Should().Be(30m, "200 * 0.15");
        r.Value.TotalAmount.Should().Be(280m);
        r.Value.Outstanding.Should().Be(280m);
        r.Value.PaidAmount.Should().Be(0m);
        r.Value.Lines.Count.Should().Be(2);
        r.Value.InvoiceNumber.Should().StartWith("SI-");
    }

    [Fact]
    public async Task Create_UnknownCustomer_Fails()
    {
        var (svc, _, _, _, _, _) = Build();
        var r = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateSalesInvoiceRequest
        {
            CustomerId = Guid.NewGuid(),
            CurrencyCode = "LYD",
            Lines = new List<CreateSalesInvoiceLineRequest>
            {
                new() { Description = "X", Quantity = 1, UnitPrice = 10, TaxRate = 0 }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ArErrorCode.NotFound);
    }

    [Fact]
    public async Task Create_InactiveCustomer_Fails()
    {
        var (svc, customers, _, _, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var cust = await CreateCustomerAsync(customers, code: "C-Inactive", tenantId: tenantId);
        cust.IsActive = false;
        await customers.UpdateAsync(cust, CancellationToken.None);

        var r = await svc.CreateAsync(cust.TenantId, Guid.NewGuid(), new CreateSalesInvoiceRequest
        {
            CustomerId = cust.Id,
            CurrencyCode = "LYD",
            Lines = new List<CreateSalesInvoiceLineRequest>
            {
                new() { Description = "X", Quantity = 1, UnitPrice = 10, TaxRate = 0 }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ArErrorCode.BusinessRuleViolation);
    }

    [Fact]
    public async Task Create_EmptyLines_Fails()
    {
        var (svc, customers, _, _, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var cust = await CreateCustomerAsync(customers, tenantId: tenantId);

        var r = await svc.CreateAsync(cust.TenantId, Guid.NewGuid(), new CreateSalesInvoiceRequest
        {
            CustomerId = cust.Id,
            CurrencyCode = "LYD",
            Lines = new List<CreateSalesInvoiceLineRequest>()
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ArErrorCode.ValidationError);
    }

    [Fact]
    public async Task Post_TransitionsToSent_AndCreatesJournalEntry()
    {
        var (svc, customers, _, seq, journals, accounts) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cust = await CreateCustomerAsync(customers, tenantId: tenantId);
        SeedDefaultCoA(accounts, tenantId);

        var create = await svc.CreateAsync(tenantId, userId, new CreateSalesInvoiceRequest
        {
            CustomerId = cust.Id,
            CurrencyCode = "LYD",
            Lines = new List<CreateSalesInvoiceLineRequest>
            {
                new() { Description = "بند", Quantity = 1, UnitPrice = 1000m, TaxRate = 0.10m }
            }
        }, CancellationToken.None);
        create.Succeeded.Should().BeTrue();

        var r = await svc.PostAsync(tenantId, userId, create.Value!.Id, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(SalesInvoiceStatus.Sent);
        r.Value.JournalEntryId.Should().NotBeNull();
        r.Value.PostedAt.Should().NotBeNull();

        journals.Created.Count.Should().Be(1, "قيد محاسبي Dr 1230 / Cr 5110");
        journals.Posted.Count.Should().Be(1);
        journals.Created[0].Lines.Should().HaveCount(2);
        journals.Created[0].Lines.Sum(l => l.Debit).Should().Be(journals.Created[0].Lines.Sum(l => l.Credit));
    }

    [Fact]
    public async Task Post_AlreadyPosted_Fails()
    {
        var (svc, customers, _, _, _, accounts) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cust = await CreateCustomerAsync(customers, tenantId: tenantId);
        SeedDefaultCoA(accounts, tenantId);

        var create = await svc.CreateAsync(tenantId, userId, new CreateSalesInvoiceRequest
        {
            CustomerId = cust.Id,
            CurrencyCode = "LYD",
            Lines = new List<CreateSalesInvoiceLineRequest>
            {
                new() { Description = "X", Quantity = 1, UnitPrice = 100, TaxRate = 0 }
            }
        }, CancellationToken.None);
        await svc.PostAsync(tenantId, userId, create.Value!.Id, CancellationToken.None);

        var r2 = await svc.PostAsync(tenantId, userId, create.Value!.Id, CancellationToken.None);
        r2.Succeeded.Should().BeFalse();
        r2.ErrorCode.Should().Be(ArErrorCode.InvalidStatusTransition);
    }

    [Fact]
    public async Task Post_MissingAccounts_Fails()
    {
        var (svc, customers, _, _, _, accounts) = Build();
        var tenantId = Guid.NewGuid();
        var cust = await CreateCustomerAsync(customers, tenantId: tenantId);
        // No CoA seeded
        var create = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalesInvoiceRequest
        {
            CustomerId = cust.Id,
            CurrencyCode = "LYD",
            Lines = new List<CreateSalesInvoiceLineRequest>
            {
                new() { Description = "X", Quantity = 1, UnitPrice = 100, TaxRate = 0 }
            }
        }, CancellationToken.None);
        create.Succeeded.Should().BeTrue();

        var r = await svc.PostAsync(tenantId, Guid.NewGuid(), create.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ArErrorCode.BusinessRuleViolation);
    }

    [Fact]
    public async Task Cancel_DraftInvoice_Succeeds()
    {
        var (svc, customers, _, _, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var cust = await CreateCustomerAsync(customers, tenantId: tenantId);
        var create = await svc.CreateAsync(cust.TenantId, Guid.NewGuid(), new CreateSalesInvoiceRequest
        {
            CustomerId = cust.Id,
            CurrencyCode = "LYD",
            Lines = new List<CreateSalesInvoiceLineRequest>
            {
                new() { Description = "X", Quantity = 1, UnitPrice = 100, TaxRate = 0 }
            }
        }, CancellationToken.None);

        var r = await svc.CancelAsync(cust.TenantId, Guid.NewGuid(), create.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(SalesInvoiceStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_Fails()
    {
        var (svc, customers, _, _, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var cust = await CreateCustomerAsync(customers, tenantId: tenantId);
        var create = await svc.CreateAsync(cust.TenantId, Guid.NewGuid(), new CreateSalesInvoiceRequest
        {
            CustomerId = cust.Id,
            CurrencyCode = "LYD",
            Lines = new List<CreateSalesInvoiceLineRequest>
            {
                new() { Description = "X", Quantity = 1, UnitPrice = 100, TaxRate = 0 }
            }
        }, CancellationToken.None);
        await svc.CancelAsync(cust.TenantId, Guid.NewGuid(), create.Value!.Id, CancellationToken.None);

        var r2 = await svc.CancelAsync(cust.TenantId, Guid.NewGuid(), create.Value!.Id, CancellationToken.None);
        r2.Succeeded.Should().BeFalse();
        r2.ErrorCode.Should().Be(ArErrorCode.InvalidStatusTransition);
    }

    [Fact]
    public async Task Cancel_WithPayments_Fails()
    {
        var (svc, customers, invoices, _, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var cust = await CreateCustomerAsync(customers, tenantId: tenantId);
        var create = await svc.CreateAsync(cust.TenantId, Guid.NewGuid(), new CreateSalesInvoiceRequest
        {
            CustomerId = cust.Id,
            CurrencyCode = "LYD",
            Lines = new List<CreateSalesInvoiceLineRequest>
            {
                new() { Description = "X", Quantity = 1, UnitPrice = 100, TaxRate = 0 }
            }
        }, CancellationToken.None);
        // simulate payment
        var inv = await invoices.GetByIdAsync(create.Value!.Id, CancellationToken.None);
        inv!.PaidAmount = 50m;
        await invoices.UpdateAsync(inv, CancellationToken.None);

        var r = await svc.CancelAsync(cust.TenantId, Guid.NewGuid(), create.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ArErrorCode.BusinessRuleViolation);
    }

    [Fact]
    public async Task Update_OnlyDraft_RecalculatesTotals()
    {
        var (svc, customers, _, _, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var cust = await CreateCustomerAsync(customers, tenantId: tenantId);
        var create = await svc.CreateAsync(cust.TenantId, Guid.NewGuid(), new CreateSalesInvoiceRequest
        {
            CustomerId = cust.Id,
            CurrencyCode = "LYD",
            Lines = new List<CreateSalesInvoiceLineRequest>
            {
                new() { Description = "X", Quantity = 1, UnitPrice = 100, TaxRate = 0 }
            }
        }, CancellationToken.None);

        var r = await svc.UpdateAsync(cust.TenantId, Guid.NewGuid(), create.Value!.Id, new UpdateSalesInvoiceRequest
        {
            CurrencyCode = "LYD",
            Lines = new List<CreateSalesInvoiceLineRequest>
            {
                new() { Description = "X", Quantity = 5, UnitPrice = 200, TaxRate = 0.10m }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.Subtotal.Should().Be(1000m);
        r.Value.TaxAmount.Should().Be(100m);
        r.Value.TotalAmount.Should().Be(1100m);
    }

    [Fact]
    public async Task Update_NonDraft_Fails()
    {
        var (svc, customers, _, _, _, accounts) = Build();
        var tenantId = Guid.NewGuid();
        var cust = await CreateCustomerAsync(customers, tenantId: tenantId);
        SeedDefaultCoA(accounts, tenantId);

        var create = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalesInvoiceRequest
        {
            CustomerId = cust.Id,
            CurrencyCode = "LYD",
            Lines = new List<CreateSalesInvoiceLineRequest>
            {
                new() { Description = "X", Quantity = 1, UnitPrice = 100, TaxRate = 0 }
            }
        }, CancellationToken.None);
        await svc.PostAsync(tenantId, Guid.NewGuid(), create.Value!.Id, CancellationToken.None);

        var r = await svc.UpdateAsync(tenantId, Guid.NewGuid(), create.Value!.Id, new UpdateSalesInvoiceRequest
        {
            CurrencyCode = "LYD",
            Lines = new List<CreateSalesInvoiceLineRequest>
            {
                new() { Description = "Y", Quantity = 1, UnitPrice = 50, TaxRate = 0 }
            }
        }, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ArErrorCode.InvalidStatusTransition);
    }

    [Fact]
    public async Task GetById_WrongTenant_Fails()
    {
        var (svc, customers, _, _, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var cust = await CreateCustomerAsync(customers, tenantId: tenantId);
        var create = await svc.CreateAsync(cust.TenantId, Guid.NewGuid(), new CreateSalesInvoiceRequest
        {
            CustomerId = cust.Id,
            CurrencyCode = "LYD",
            Lines = new List<CreateSalesInvoiceLineRequest>
            {
                new() { Description = "X", Quantity = 1, UnitPrice = 100, TaxRate = 0 }
            }
        }, CancellationToken.None);

        var r = await svc.GetByIdAsync(Guid.NewGuid(), create.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ArErrorCode.NotFound);
    }

    [Fact]
    public async Task List_FiltersByCustomer()
    {
        var (svc, customers, _, _, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var c1 = await CreateCustomerAsync(customers, "C-A", tenantId: tenantId);
        var c2 = await CreateCustomerAsync(customers, "C-B", tenantId: tenantId);
        await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalesInvoiceRequest
        {
            CustomerId = c1.Id, CurrencyCode = "LYD",
            Lines = new List<CreateSalesInvoiceLineRequest> { new() { Description = "A1", Quantity = 1, UnitPrice = 100, TaxRate = 0 } }
        }, CancellationToken.None);
        await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalesInvoiceRequest
        {
            CustomerId = c1.Id, CurrencyCode = "LYD",
            Lines = new List<CreateSalesInvoiceLineRequest> { new() { Description = "A2", Quantity = 1, UnitPrice = 50, TaxRate = 0 } }
        }, CancellationToken.None);
        await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalesInvoiceRequest
        {
            CustomerId = c2.Id, CurrencyCode = "LYD",
            Lines = new List<CreateSalesInvoiceLineRequest> { new() { Description = "B1", Quantity = 1, UnitPrice = 75, TaxRate = 0 } }
        }, CancellationToken.None);

        var r = await svc.ListAsync(tenantId, c1.Id, null, 0, 50, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Count.Should().Be(2);
        r.Value.All(i => i.CustomerId == c1.Id).Should().BeTrue();
    }

    [Fact]
    public async Task Aging_GroupsOpenInvoicesByBucket()
    {
        var (svc, customers, invoices, _, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var cust = await CreateCustomerAsync(customers, tenantId: tenantId);
        // Invoice due 10 days ago (0-30 bucket)
        var r1 = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalesInvoiceRequest
        {
            CustomerId = cust.Id, CurrencyCode = "LYD",
            InvoiceDate = DateTime.UtcNow.AddDays(-40),
            DueDate = DateTime.UtcNow.AddDays(-10),
            Lines = new List<CreateSalesInvoiceLineRequest> { new() { Description = "A", Quantity = 1, UnitPrice = 100, TaxRate = 0 } }
        }, CancellationToken.None);
        // Manually mark as Sent/Posted so it appears in aging
        var inv1 = await invoices.GetByIdAsync(r1.Value!.Id, CancellationToken.None);
        inv1!.Status = SalesInvoiceStatus.Sent;
        inv1.PaidAmount = 0;
        inv1.Outstanding = 100;
        await invoices.UpdateAsync(inv1, CancellationToken.None);

        var r2 = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalesInvoiceRequest
        {
            CustomerId = cust.Id, CurrencyCode = "LYD",
            InvoiceDate = DateTime.UtcNow.AddDays(-200),
            DueDate = DateTime.UtcNow.AddDays(-150), // > 120 days
            Lines = new List<CreateSalesInvoiceLineRequest> { new() { Description = "B", Quantity = 1, UnitPrice = 200, TaxRate = 0 } }
        }, CancellationToken.None);
        var inv2 = await invoices.GetByIdAsync(r2.Value!.Id, CancellationToken.None);
        inv2!.Status = SalesInvoiceStatus.Sent;
        inv2.PaidAmount = 0;
        inv2.Outstanding = 200;
        await invoices.UpdateAsync(inv2, CancellationToken.None);

        var aging = await svc.GetAgingReportAsync(tenantId, DateTime.UtcNow, CancellationToken.None);
        aging.Succeeded.Should().BeTrue();
        aging.Value!.GrandTotal.Total.Should().Be(300m);
        aging.Value.Rows.Should().HaveCount(1);
        aging.Value.Rows[0].CustomerId.Should().Be(cust.Id);
        aging.Value.GrandTotal.Bucket0To30.Should().Be(100m);
        aging.Value.GrandTotal.Bucket120Plus.Should().Be(200m);
    }
}

// ============== Fakes ==============

internal class FakeSalesInvoiceRepository : ISalesInvoiceRepository
{
    private readonly Dictionary<Guid, SalesInvoice> _items = new();
    private readonly object _lock = new();

    public Task<SalesInvoice?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        lock (_lock)
        {
            return Task.FromResult(_items.TryGetValue(id, out var inv) ? Clone(inv) : null);
        }
    }

    public Task<SalesInvoice?> GetByInvoiceNumberAsync(Guid tenantId, string invoiceNumber, CancellationToken ct)
    {
        lock (_lock)
        {
            return Task.FromResult(_items.Values.FirstOrDefault(i => i.TenantId == tenantId && i.InvoiceNumber == invoiceNumber));
        }
    }

    public Task<IReadOnlyList<SalesInvoice>> ListAsync(Guid tenantId, Guid? customerId, SalesInvoiceStatus? status, int skip, int take, CancellationToken ct)
    {
        lock (_lock)
        {
            var q = _items.Values
                .Where(i => i.TenantId == tenantId)
                .Where(i => customerId == null || i.CustomerId == customerId)
                .Where(i => status == null || i.Status == status)
                .OrderBy(i => i.InvoiceNumber)
                .Skip(skip).Take(take)
                .ToList();
            return Task.FromResult<IReadOnlyList<SalesInvoice>>(q);
        }
    }

    public Task InsertAsync(SalesInvoice inv, CancellationToken ct)
    {
        lock (_lock) { _items[inv.Id] = Clone(inv); }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(SalesInvoice inv, CancellationToken ct)
    {
        lock (_lock) { _items[inv.Id] = Clone(inv); }
        return Task.CompletedTask;
    }

    public Task InsertLinesAsync(Guid tenantId, Guid salesInvoiceId, IEnumerable<SalesInvoiceLine> lines, CancellationToken ct)
    {
        lock (_lock)
        {
            if (_items.TryGetValue(salesInvoiceId, out var inv))
            {
                inv.Lines = lines.ToList();
            }
        }
        return Task.CompletedTask;
    }

    public Task UpdateLinesAsync(Guid tenantId, Guid salesInvoiceId, IEnumerable<SalesInvoiceLine> lines, CancellationToken ct)
    {
        lock (_lock)
        {
            if (_items.TryGetValue(salesInvoiceId, out var inv))
            {
                inv.Lines = lines.ToList();
            }
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SalesInvoiceLine>> GetLinesAsync(Guid salesInvoiceId, CancellationToken ct)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<SalesInvoiceLine>>(
                _items.TryGetValue(salesInvoiceId, out var inv) ? inv.Lines.ToList() : new List<SalesInvoiceLine>());
        }
    }

    public Task<decimal> GetTotalAllocatedAsync(Guid tenantId, Guid salesInvoiceId, CancellationToken ct)
    {
        lock (_lock)
        {
            return Task.FromResult(_items.TryGetValue(salesInvoiceId, out var inv) ? inv.PaidAmount : 0m);
        }
    }

    public Task<IReadOnlyList<SalesInvoice>> ListOpenByCustomerAsync(Guid tenantId, Guid customerId, CancellationToken ct)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<SalesInvoice>>(
                _items.Values.Where(i => i.TenantId == tenantId && i.CustomerId == customerId && i.Outstanding > 0).ToList());
        }
    }

    public Task<IReadOnlyList<SalesInvoice>> ListAllOpenAsync(Guid tenantId, CancellationToken ct)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<SalesInvoice>>(
                _items.Values.Where(i => i.TenantId == tenantId && i.Outstanding > 0).ToList());
        }
    }

    private static SalesInvoice Clone(SalesInvoice i) => new()
    {
        Id = i.Id, TenantId = i.TenantId, CompanyId = i.CompanyId, CustomerId = i.CustomerId,
        InvoiceNumber = i.InvoiceNumber, InvoiceDate = i.InvoiceDate, DueDate = i.DueDate,
        CurrencyCode = i.CurrencyCode, ExchangeRate = i.ExchangeRate,
        Subtotal = i.Subtotal, TaxAmount = i.TaxAmount, TotalAmount = i.TotalAmount,
        PaidAmount = i.PaidAmount, Outstanding = i.Outstanding,
        Status = i.Status, Notes = i.Notes, ProjectId = i.ProjectId,
        PostedAt = i.PostedAt, PostedBy = i.PostedBy, JournalEntryId = i.JournalEntryId,
        CreatedAt = i.CreatedAt, CreatedBy = i.CreatedBy, UpdatedAt = i.UpdatedAt, UpdatedBy = i.UpdatedBy,
        Lines = i.Lines.ToList(),
    };
}

internal class FakeArDocumentSequenceRepository : IArDocumentSequenceRepository
{
    private readonly Dictionary<(Guid, string), int> _counters = new();
    private readonly object _lock = new();
    public Task<string> GetNextNumberAsync(Guid tenantId, string prefix, CancellationToken ct)
    {
        lock (_lock)
        {
            var key = (tenantId, prefix);
            if (!_counters.TryGetValue(key, out var n)) n = 0;
            n++;
            _counters[key] = n;
            var year = DateTime.UtcNow.Year;
            var format = prefix == "RC" ? "D6" : "D6";
            return Task.FromResult($"{prefix}-{year}-{n.ToString(format)}");
        }
    }
}

internal class FakeJournalEntryService : IJournalEntryService
{
    public List<JournalEntry> Created { get; } = new();
    public List<JournalEntry> Posted { get; } = new();
    private readonly Dictionary<Guid, JournalEntry> _items = new();
    private int _counter;

    public Task<FinanceResult<JournalEntryResponse>> CreateDraftAsync(Guid tenantId, Guid userId, PostJournalEntryRequest request, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        _counter++;
        var entry = new JournalEntry
        {
            Id = id,
            TenantId = tenantId,
            EntryNumber = $"JE-Fake-{_counter:D4}",
            EntryDate = request.EntryDate,
            Description = request.Description,
            Reference = request.Reference,
            Status = JournalEntryStatus.Draft,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Lines = request.Lines.Select((l, idx) => new JournalLine
            {
                Id = Guid.NewGuid(),
                JournalEntryId = id,
                AccountId = l.AccountId,
                Debit = l.Debit,
                Credit = l.Credit,
                LineNumber = idx + 1,
                Description = l.Description,
            }).ToList()
        };
        _items[id] = entry;
        Created.Add(entry);
        return Task.FromResult(FinanceResult<JournalEntryResponse>.Ok(BuildResponse(entry)));
    }

    public Task<FinanceResult<JournalEntryResponse>> PostAsync(Guid tenantId, Guid userId, Guid entryId, CancellationToken ct)
    {
        if (!_items.TryGetValue(entryId, out var e))
            return Task.FromResult(FinanceResult<JournalEntryResponse>.Fail("غير موجود", FinanceErrorCode.NotFound));
        e.Status = JournalEntryStatus.Posted;
        e.PostedAt = DateTime.UtcNow;
        e.UpdatedAt = DateTime.UtcNow;
        Posted.Add(e);
        return Task.FromResult(FinanceResult<JournalEntryResponse>.Ok(BuildResponse(e)));
    }

    public Task<FinanceResult<JournalEntryResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        if (!_items.TryGetValue(id, out var e))
            return Task.FromResult(FinanceResult<JournalEntryResponse>.Fail("غير موجود", FinanceErrorCode.NotFound));
        return Task.FromResult(FinanceResult<JournalEntryResponse>.Ok(BuildResponse(e)));
    }

    public Task<FinanceResult<IReadOnlyList<JournalEntryResponse>>> ListAsync(Guid tenantId, DateTime? from, DateTime? to, JournalEntryStatus? status, int skip, int take, CancellationToken ct)
    {
        return Task.FromResult(FinanceResult<IReadOnlyList<JournalEntryResponse>>.Ok(new List<JournalEntryResponse>()));
    }

    private static JournalEntryResponse BuildResponse(JournalEntry e) => new()
    {
        Id = e.Id,
        EntryNumber = e.EntryNumber,
        EntryDate = e.EntryDate,
        Description = e.Description,
        Reference = e.Reference,
        Status = e.Status,
        PostedAt = e.PostedAt,
        TotalDebit = e.Lines.Sum(l => l.Debit),
        TotalCredit = e.Lines.Sum(l => l.Credit),
        Lines = e.Lines.OrderBy(l => l.LineNumber).Select(l => new JournalLineResponse
        {
            LineNumber = l.LineNumber, AccountId = l.AccountId, Debit = l.Debit, Credit = l.Credit, Description = l.Description
        }).ToList()
    };
}

internal class FakeAccountRepository : IAccountRepository
{
    private readonly List<Account> _accounts;
    public FakeAccountRepository(List<Account> seed) => _accounts = seed;

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
