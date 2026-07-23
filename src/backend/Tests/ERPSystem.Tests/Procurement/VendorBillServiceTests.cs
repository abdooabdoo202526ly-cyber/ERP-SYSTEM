using ERPSystem.Modules.Procurement.Application;
using ERPSystem.Modules.Procurement.Application.Services;
using ERPSystem.Modules.Procurement.Entities;
using ERPSystem.Modules.Procurement.Infrastructure;
using ERPSystem.Tests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERPSystem.Tests.Procurement;

public class VendorBillServiceTests
{
    private static (VendorBillService svc,
                    FakeVendorBillRepository bills,
                    FakeGoodsReceiptRepository grs,
                    FakePurchaseOrderRepository pos,
                    FakeDocumentSequenceRepository seq,
                    FakeJournalEntryService journal,
                    FakeAccountsConnectionFactory db,
                    FakeVendorRepository vendors) Build()
    {
        var bills = new FakeVendorBillRepository();
        var grs = new FakeGoodsReceiptRepository();
        var pos = new FakePurchaseOrderRepository();
        var seq = new FakeDocumentSequenceRepository();
        var journal = new FakeJournalEntryService();
        var db = new FakeAccountsConnectionFactory();
        var vendors = new FakeVendorRepository();
        var svc = new VendorBillService(bills, grs, pos, seq, journal, db,
            NullLogger<VendorBillService>.Instance);
        return (svc, bills, grs, pos, seq, journal, db, vendors);
    }

    /// <summary>
    /// Full E2E scenario: vendor -> PO Approved -> GR Received -> Bill Posted (with JE).
    /// Exercises the entire procurement cycle from start to finish.
    /// </summary>
    [Fact]
    public async Task FullScenario_VendorToBill_E2E()
    {
        var (svc, bills, grs, pos, seq, journal, db, vendors) = Build();
        var tenantId = Guid.NewGuid();
        var invAcctId = Guid.NewGuid();
        var apAcctId = Guid.NewGuid();
        SeedAccounts(db, tenantId, invAcctId, apAcctId);

        // 1) Vendor
        var v = new Vendor
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "V-E2E", Name = "Vendor E2E",
            Currency = "LYD", PaymentTerms = PaymentTerms.Net30, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        vendors.Items[v.Id] = v;

        // 2) PO Approved
        var itemId = Guid.NewGuid();
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PoNumber = "PO-E2E", VendorId = v.Id,
            Status = PurchaseOrderStatus.Approved, OrderDate = DateTime.UtcNow, Currency = "LYD",
            SubTotal = 1000m, TaxAmount = 0m, TotalAmount = 1000m,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid(),
            ApprovedAt = DateTime.UtcNow, ApprovedBy = Guid.NewGuid()
        };
        pos.Items[po.Id] = po;
        var poLine = new PurchaseOrderLine
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PurchaseOrderId = po.Id, ItemId = itemId,
            Quantity = 10m, UnitPrice = 100m, TaxRate = 0m, SubTotal = 1000m, LineOrder = 0
        };
        pos.Lines.Add(poLine);

        // 3) GR Received
        var gr = new GoodsReceipt
        {
            Id = Guid.NewGuid(), TenantId = tenantId, GrNumber = "GR-E2E", PurchaseOrderId = po.Id,
            Status = GoodsReceiptStatus.Received, ReceivedDate = DateTime.UtcNow, WarehouseId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        grs.Items[gr.Id] = gr;
        grs.Lines.Add(new GoodsReceiptLine
        {
            Id = Guid.NewGuid(), TenantId = tenantId, GoodsReceiptId = gr.Id, ItemId = itemId,
            Quantity = 10m, UnitCost = 100m, LineOrder = 0
        });

        // 4) Bill
        var billRes = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateVendorBillRequest
        {
            GoodsReceiptId = gr.Id, BillDate = DateTime.UtcNow,
            Currency = "LYD",
            Lines = new List<CreateVendorBillLineRequest>
            {
                new() { ItemId = itemId, Quantity = 10m, UnitPrice = 100m, TaxRate = 0m }
            }
        }, CancellationToken.None);

        billRes.Succeeded.Should().BeTrue();
        billRes.Value!.BillNumber.Should().StartWith("BILL-");
        billRes.Value!.VendorId.Should().Be(v.Id);
        billRes.Value!.TotalAmount.Should().Be(1000m);
        billRes.Value!.Status.Should().Be(VendorBillStatus.Draft);

        // 5) Post the bill — should create a JournalEntry
        var postRes = await svc.PostAsync(tenantId, Guid.NewGuid(), billRes.Value!.Id, CancellationToken.None);
        postRes.Succeeded.Should().BeTrue();
        postRes.Value!.Status.Should().Be(VendorBillStatus.Posted);
        postRes.Value!.PostedAt.Should().NotBeNull();
        postRes.Value!.JournalEntryId.Should().NotBeNull();

        // JE created with correct lines
        journal.CreatedEntries.Should().HaveCount(1);
        var je = journal.CreatedEntries[0];
        je.Lines.Should().HaveCount(2);
        je.Lines[0].Debit.Should().Be(1000m);   // Dr Inventory
        je.Lines[0].AccountId.Should().Be(invAcctId);
        je.Lines[1].Credit.Should().Be(1000m);  // Cr AP
        je.Lines[1].AccountId.Should().Be(apAcctId);

        // JE was posted
        journal.PostedEntries.Should().HaveCount(1);
    }

    [Fact]
    public async Task Create_FromDraftGR_Succeeds_v1_0_31()
    {
        var (svc, _, grs, pos, _, _, _, vendors) = Build();
        var tenantId = Guid.NewGuid();
        var v = new Vendor
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "V", Name = "X",
            Currency = "LYD", PaymentTerms = PaymentTerms.Cash, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        vendors.Items[v.Id] = v;
        var gr = new GoodsReceipt
        {
            Id = Guid.NewGuid(), TenantId = tenantId, GrNumber = "GR-D", PurchaseOrderId = Guid.NewGuid(),
            Status = GoodsReceiptStatus.Draft, ReceivedDate = DateTime.UtcNow, WarehouseId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        grs.Items[gr.Id] = gr;

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateVendorBillRequest
        {
            GoodsReceiptId = gr.Id, BillDate = DateTime.UtcNow, Currency = "LYD",
            Lines = new List<CreateVendorBillLineRequest> { new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 10m, TaxRate = 0m } }
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue(); // v1.0.31: نسمح بـ Draft GR
    }

    [Fact]
    public async Task Create_GRNotFound_Fails()
    {
        var (svc, _, _, _, _, _, _, _) = Build();
        var r = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateVendorBillRequest
        {
            GoodsReceiptId = Guid.NewGuid(), BillDate = DateTime.UtcNow, Currency = "LYD",
            Lines = new List<CreateVendorBillLineRequest> { new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 10m, TaxRate = 0m } }
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ProcurementErrorCode.NotFound);
    }

    [Fact]
    public async Task Create_CalculatesTotals_WithTax()
    {
        var (svc, _, grs, pos, _, _, _, vendors) = Build();
        var tenantId = Guid.NewGuid();
        var v = new Vendor
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "V-T", Name = "X",
            Currency = "LYD", PaymentTerms = PaymentTerms.Cash, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        vendors.Items[v.Id] = v;

        // Seed PO and Received GR
        var itemId = Guid.NewGuid();
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PoNumber = "PO-T", VendorId = v.Id,
            Status = PurchaseOrderStatus.Received, OrderDate = DateTime.UtcNow, Currency = "LYD",
            SubTotal = 1000m, TaxAmount = 0m, TotalAmount = 1000m,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        pos.Items[po.Id] = po;
        pos.Lines.Add(new PurchaseOrderLine
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PurchaseOrderId = po.Id, ItemId = itemId,
            Quantity = 10m, UnitPrice = 100m, TaxRate = 0m, SubTotal = 1000m, LineOrder = 0
        });

        var gr = new GoodsReceipt
        {
            Id = Guid.NewGuid(), TenantId = tenantId, GrNumber = "GR-T", PurchaseOrderId = po.Id,
            Status = GoodsReceiptStatus.Received, ReceivedDate = DateTime.UtcNow, WarehouseId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        grs.Items[gr.Id] = gr;
        grs.Lines.Add(new GoodsReceiptLine
        {
            Id = Guid.NewGuid(), TenantId = tenantId, GoodsReceiptId = gr.Id, ItemId = itemId,
            Quantity = 10m, UnitCost = 100m, LineOrder = 0
        });

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateVendorBillRequest
        {
            GoodsReceiptId = gr.Id, BillDate = DateTime.UtcNow, Currency = "LYD",
            Lines = new List<CreateVendorBillLineRequest>
            {
                new() { ItemId = itemId, Quantity = 10m, UnitPrice = 100m, TaxRate = 0.15m }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.SubTotal.Should().Be(1000m);
        r.Value!.TaxAmount.Should().Be(150m);
        r.Value!.TotalAmount.Should().Be(1150m);
    }

    [Fact]
    public async Task Post_AlreadyPosted_Fails()
    {
        var (svc, bills, grs, pos, _, journal, db, vendors) = Build();
        var tenantId = Guid.NewGuid();
        SeedAccounts(db, tenantId, Guid.NewGuid(), Guid.NewGuid());

        // Seed minimal PO/GR/Received
        var v = new Vendor
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "V-P", Name = "X",
            Currency = "LYD", PaymentTerms = PaymentTerms.Cash, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        vendors.Items[v.Id] = v;
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PoNumber = "PO-P", VendorId = v.Id,
            Status = PurchaseOrderStatus.Received, OrderDate = DateTime.UtcNow, Currency = "LYD",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        pos.Items[po.Id] = po;
        var gr = new GoodsReceipt
        {
            Id = Guid.NewGuid(), TenantId = tenantId, GrNumber = "GR-P", PurchaseOrderId = po.Id,
            Status = GoodsReceiptStatus.Received, ReceivedDate = DateTime.UtcNow, WarehouseId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        grs.Items[gr.Id] = gr;

        var bill = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateVendorBillRequest
        {
            GoodsReceiptId = gr.Id, BillDate = DateTime.UtcNow, Currency = "LYD",
            Lines = new List<CreateVendorBillLineRequest> { new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 10m, TaxRate = 0m } }
        }, CancellationToken.None);
        await svc.PostAsync(tenantId, Guid.NewGuid(), bill.Value!.Id, CancellationToken.None);

        // Second post
        var r2 = await svc.PostAsync(tenantId, Guid.NewGuid(), bill.Value!.Id, CancellationToken.None);
        r2.Succeeded.Should().BeFalse();
        r2.ErrorCode.Should().Be(ProcurementErrorCode.InvalidStatusTransition);
    }

    [Fact]
    public async Task Post_Idempotency_AlreadyHasJournalEntry_ReturnsOkWithoutCreatingNewJE()
    {
        var (svc, bills, grs, pos, _, journal, db, vendors) = Build();
        var tenantId = Guid.NewGuid();
        SeedAccounts(db, tenantId, Guid.NewGuid(), Guid.NewGuid());

        // Setup minimal bill already in Posted state with JE
        var v = new Vendor
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "V-Idem", Name = "X",
            Currency = "LYD", PaymentTerms = PaymentTerms.Cash, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        vendors.Items[v.Id] = v;
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PoNumber = "PO-Idem", VendorId = v.Id,
            Status = PurchaseOrderStatus.Received, OrderDate = DateTime.UtcNow, Currency = "LYD",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        pos.Items[po.Id] = po;
        var gr = new GoodsReceipt
        {
            Id = Guid.NewGuid(), TenantId = tenantId, GrNumber = "GR-Idem", PurchaseOrderId = po.Id,
            Status = GoodsReceiptStatus.Received, ReceivedDate = DateTime.UtcNow, WarehouseId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        grs.Items[gr.Id] = gr;

        var bill = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateVendorBillRequest
        {
            GoodsReceiptId = gr.Id, BillDate = DateTime.UtcNow, Currency = "LYD",
            Lines = new List<CreateVendorBillLineRequest> { new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 10m, TaxRate = 0m } }
        }, CancellationToken.None);

        // Manually mark as Draft (not Posted) but with JE ID — to test the idempotency check
        var fetched = bills.Items[bill.Value!.Id];
        fetched.JournalEntryId = Guid.NewGuid();
        fetched.Status = VendorBillStatus.Draft;
        bills.Items[fetched.Id] = fetched;

        // Call Post again — should be idempotent
        var r = await svc.PostAsync(tenantId, Guid.NewGuid(), bill.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        journal.CreatedEntries.Should().BeEmpty("no new JE created due to idempotency");
    }

    [Fact]
    public async Task Post_WithoutAccounts_FallsBackToLegacyPath()
    {
        var (svc, _, grs, pos, _, journal, db, vendors) = Build();
        var tenantId = Guid.NewGuid();
        // no accounts seeded

        var v = new Vendor
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "V-NA", Name = "X",
            Currency = "LYD", PaymentTerms = PaymentTerms.Cash, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        vendors.Items[v.Id] = v;
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PoNumber = "PO-NA", VendorId = v.Id,
            Status = PurchaseOrderStatus.Received, OrderDate = DateTime.UtcNow, Currency = "LYD",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        pos.Items[po.Id] = po;
        var gr = new GoodsReceipt
        {
            Id = Guid.NewGuid(), TenantId = tenantId, GrNumber = "GR-NA", PurchaseOrderId = po.Id,
            Status = GoodsReceiptStatus.Received, ReceivedDate = DateTime.UtcNow, WarehouseId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        grs.Items[gr.Id] = gr;

        var bill = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateVendorBillRequest
        {
            GoodsReceiptId = gr.Id, BillDate = DateTime.UtcNow, Currency = "LYD",
            Lines = new List<CreateVendorBillLineRequest> { new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 10m, TaxRate = 0m } }
        }, CancellationToken.None);

        var r = await svc.PostAsync(tenantId, Guid.NewGuid(), bill.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(VendorBillStatus.Posted);
        r.Value!.JournalEntryId.Should().BeNull("no accounts -> legacy path");
        journal.CreatedEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task Post_JournalEntryCreationFails_ReturnsError()
    {
        var (svc, _, grs, pos, _, journal, db, vendors) = Build();
        var tenantId = Guid.NewGuid();
        SeedAccounts(db, tenantId, Guid.NewGuid(), Guid.NewGuid());
        journal.FailNextCreate = true;

        var v = new Vendor
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "V-JEF", Name = "X",
            Currency = "LYD", PaymentTerms = PaymentTerms.Cash, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        vendors.Items[v.Id] = v;
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PoNumber = "PO-JEF", VendorId = v.Id,
            Status = PurchaseOrderStatus.Received, OrderDate = DateTime.UtcNow, Currency = "LYD",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        pos.Items[po.Id] = po;
        var gr = new GoodsReceipt
        {
            Id = Guid.NewGuid(), TenantId = tenantId, GrNumber = "GR-JEF", PurchaseOrderId = po.Id,
            Status = GoodsReceiptStatus.Received, ReceivedDate = DateTime.UtcNow, WarehouseId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        grs.Items[gr.Id] = gr;

        var bill = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateVendorBillRequest
        {
            GoodsReceiptId = gr.Id, BillDate = DateTime.UtcNow, Currency = "LYD",
            Lines = new List<CreateVendorBillLineRequest> { new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 10m, TaxRate = 0m } }
        }, CancellationToken.None);

        var r = await svc.PostAsync(tenantId, Guid.NewGuid(), bill.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ProcurementErrorCode.BusinessRuleViolation);
    }

    [Fact]
    public async Task GetById_WrongTenant_Fails()
    {
        var (svc, _, _, _, _, _, _, _) = Build();
        var r = await svc.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ProcurementErrorCode.NotFound);
    }

    [Fact]
    public async Task List_FiltersByVendor()
    {
        var (svc, bills, _, _, _, _, _, vendors) = Build();
        var tenantId = Guid.NewGuid();
        var v1 = new Vendor { Id = Guid.NewGuid(), TenantId = tenantId, Code = "V1", Name = "A", Currency = "LYD", PaymentTerms = PaymentTerms.Cash, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid() };
        var v2 = new Vendor { Id = Guid.NewGuid(), TenantId = tenantId, Code = "V2", Name = "B", Currency = "LYD", PaymentTerms = PaymentTerms.Cash, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid() };
        vendors.Items[v1.Id] = v1;
        vendors.Items[v2.Id] = v2;

        bills.Items[Guid.NewGuid()] = new VendorBill
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BillNumber = "B-1", GoodsReceiptId = Guid.NewGuid(),
            VendorId = v1.Id, Status = VendorBillStatus.Draft, BillDate = DateTime.UtcNow, Currency = "LYD",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        bills.Items[Guid.NewGuid()] = new VendorBill
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BillNumber = "B-2", GoodsReceiptId = Guid.NewGuid(),
            VendorId = v2.Id, Status = VendorBillStatus.Draft, BillDate = DateTime.UtcNow, Currency = "LYD",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };

        var r = await svc.ListAsync(tenantId, vendorId: v1.Id, grId: null, status: null, 0, 50, CancellationToken.None);
        r.Value!.Should().HaveCount(1);
        r.Value![0].VendorId.Should().Be(v1.Id);
    }

    private static void SeedAccounts(FakeAccountsConnectionFactory db, Guid tenantId, Guid invAcctId, Guid apAcctId)
    {
        db.InventoryAccountIdByTenant[tenantId] = invAcctId;
        db.ApAccountIdByTenant[tenantId] = apAcctId;
    }
}
