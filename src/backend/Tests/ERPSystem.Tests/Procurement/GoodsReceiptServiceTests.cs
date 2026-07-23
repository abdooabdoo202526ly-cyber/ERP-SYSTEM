using ERPSystem.Modules.Companies.Entities;
using ERPSystem.Modules.Inventory.Entities;
using ERPSystem.Modules.Procurement.Application;
using ERPSystem.Modules.Procurement.Application.Services;
using ERPSystem.Modules.Procurement.Entities;
using ERPSystem.Modules.Procurement.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERPSystem.Tests.Procurement;

public class GoodsReceiptServiceTests
{
    private static (GoodsReceiptService svc,
                    FakeGoodsReceiptRepository grs,
                    FakePurchaseOrderRepository pos,
                    FakeStockMovementService stock,
                    FakeDocumentSequenceRepository seq,
                    FakeCompanyRepository companies,
                    FakeVendorRepository vendors,
                    FakeWarehouseRepository warehouses) Build()
    {
        var grs = new FakeGoodsReceiptRepository();
        var pos = new FakePurchaseOrderRepository();
        var stock = new FakeStockMovementService();
        var seq = new FakeDocumentSequenceRepository();
        var companies = new FakeCompanyRepository();
        var vendors = new FakeVendorRepository();
        var warehouses = new FakeWarehouseRepository();
        var svc = new GoodsReceiptService(grs, pos, stock, seq, companies, vendors, warehouses,
            NullLogger<GoodsReceiptService>.Instance);
        return (svc, grs, pos, stock, seq, companies, vendors, warehouses);
    }

    private static async Task<PurchaseOrder> SeedApprovedPOAsync(
        FakePurchaseOrderRepository pos,
        FakeVendorRepository vendors,
        FakeDocumentSequenceRepository seq,
        Guid tenantId,
        params (Guid itemId, decimal qty, decimal price, decimal tax)[] lines)
    {
        var v = new Vendor
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "V-GR", Name = "GR Vendor",
            Currency = "LYD", PaymentTerms = PaymentTerms.Net30, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        vendors.Items[v.Id] = v;

        var poLines = new List<PurchaseOrderLine>();
        decimal sub = 0, tax = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            var (itemId, qty, price, taxRate) = lines[i];
            var lineSub = qty * price;
            var lineTax = lineSub * taxRate;
            sub += lineSub; tax += lineTax;
            poLines.Add(new PurchaseOrderLine
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                ItemId = itemId, Quantity = qty, UnitPrice = price, TaxRate = taxRate,
                SubTotal = lineSub, LineOrder = i
            });
        }
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            PoNumber = await seq.GetNextNumberAsync(tenantId, "PO", CancellationToken.None),
            VendorId = v.Id, Status = PurchaseOrderStatus.Approved,
            OrderDate = DateTime.UtcNow, Currency = "LYD",
            SubTotal = sub, TaxAmount = tax, TotalAmount = sub + tax,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid(),
            ApprovedAt = DateTime.UtcNow, ApprovedBy = Guid.NewGuid(),
            Lines = poLines
        };
        pos.Items[po.Id] = po;
        pos.Lines.AddRange(poLines);
        // Ensure each line's PurchaseOrderId matches po.Id (needed for the fake's GetByIdAsync load)
        // The real repo would set this via SQL during InsertLinesAsync, but the test seeds
        // directly into pos.Lines, so we set it explicitly.
        foreach (var l in poLines) l.PurchaseOrderId = po.Id;
        return po;
    }

    private static Warehouse SeedWarehouse(FakeWarehouseRepository warehouses, Guid tenantId, string code = "WH-001")
    {
        var w = new Warehouse
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = Guid.NewGuid(),
            Code = code, Name = "Main Warehouse", IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        warehouses.Items[w.Id] = w;
        return w;
    }

    private static Company SeedHoldingCompany(FakeCompanyRepository companies, Guid tenantId)
    {
        var c = new Company
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "HOLD", Name = "Holding Co",
            IsGroup = true, BaseCurrency = "LYD", IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        companies.Companies[c.Id] = c;
        companies.HoldingCompanyId = c.Id;
        return c;
    }

    [Fact]
    public async Task Create_FromApprovedPO_Succeeds_StartsAsDraft()
    {
        var (svc, grs, pos, _, _, _, vendors, warehouses) = Build();
        var tenantId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var po = await SeedApprovedPOAsync(pos, vendors, new FakeDocumentSequenceRepository(), tenantId,
            (itemId, 10m, 50m, 0m));
        var wh = SeedWarehouse(warehouses, tenantId);

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = po.Id,
            ReceivedDate = DateTime.UtcNow,
            WarehouseId = wh.Id,
            Lines = new List<CreateGoodsReceiptLineRequest>
            {
                new() { ItemId = itemId, Quantity = 10m, UnitCost = 50m }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.GrNumber.Should().StartWith("GR-");
        r.Value.Status.Should().Be(GoodsReceiptStatus.Draft);
        r.Value.Lines.Should().HaveCount(1);
        grs.Items.Count.Should().Be(1);
    }

    [Fact]
    public async Task Create_FromDraftPO_Fails()
    {
        var (svc, _, pos, _, _, _, vendors, warehouses) = Build();
        var tenantId = Guid.NewGuid();
        // Seed a PO in Draft status (not Approved)
        var v = new Vendor
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "V-X", Name = "X",
            Currency = "LYD", PaymentTerms = PaymentTerms.Cash, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        vendors.Items[v.Id] = v;
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PoNumber = "PO-DRAFT", VendorId = v.Id,
            Status = PurchaseOrderStatus.Draft, OrderDate = DateTime.UtcNow, Currency = "LYD",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        pos.Items[po.Id] = po;
        var wh = SeedWarehouse(warehouses, tenantId);

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = po.Id, ReceivedDate = DateTime.UtcNow, WarehouseId = wh.Id,
            Lines = new List<CreateGoodsReceiptLineRequest>
            {
                new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitCost = 10m }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ProcurementErrorCode.BusinessRuleViolation);
    }

    [Fact]
    public async Task Create_QuantityExceedsPO_Fails()
    {
        var (svc, _, pos, _, _, _, vendors, warehouses) = Build();
        var tenantId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var po = await SeedApprovedPOAsync(pos, vendors, new FakeDocumentSequenceRepository(), tenantId,
            (itemId, 5m, 10m, 0m)); // PO has only 5 units
        var wh = SeedWarehouse(warehouses, tenantId);

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = po.Id, ReceivedDate = DateTime.UtcNow, WarehouseId = wh.Id,
            Lines = new List<CreateGoodsReceiptLineRequest>
            {
                new() { ItemId = itemId, Quantity = 10m, UnitCost = 10m } // exceeds
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ProcurementErrorCode.BusinessRuleViolation);
    }

    [Fact]
    public async Task Create_ItemNotInPO_Fails()
    {
        var (svc, _, pos, _, _, _, vendors, warehouses) = Build();
        var tenantId = Guid.NewGuid();
        var itemInPO = Guid.NewGuid();
        var itemNotInPO = Guid.NewGuid();
        var po = await SeedApprovedPOAsync(pos, vendors, new FakeDocumentSequenceRepository(), tenantId,
            (itemInPO, 5m, 10m, 0m));
        var wh = SeedWarehouse(warehouses, tenantId);

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = po.Id, ReceivedDate = DateTime.UtcNow, WarehouseId = wh.Id,
            Lines = new List<CreateGoodsReceiptLineRequest>
            {
                new() { ItemId = itemNotInPO, Quantity = 1m, UnitCost = 10m }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ProcurementErrorCode.BusinessRuleViolation);
    }

    [Fact]
    public async Task Receive_CreatesStockMovements_AndUpdatesStatus()
    {
        var (svc, _, pos, stock, seq, companies, vendors, warehouses) = Build();
        var tenantId = Guid.NewGuid();
        SeedHoldingCompany(companies, tenantId);
        var itemId = Guid.NewGuid();
        var po = await SeedApprovedPOAsync(pos, vendors, seq, tenantId,
            (itemId, 10m, 50m, 0m));
        var wh = SeedWarehouse(warehouses, tenantId);

        var created = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = po.Id, ReceivedDate = DateTime.UtcNow, WarehouseId = wh.Id,
            Lines = new List<CreateGoodsReceiptLineRequest>
            {
                new() { ItemId = itemId, Quantity = 10m, UnitCost = 50m }
            }
        }, CancellationToken.None);

        var r = await svc.ReceiveAsync(tenantId, Guid.NewGuid(), created.Value!.Id, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(GoodsReceiptStatus.Received);

        // Stock movement was created and posted
        stock.CreatedReceives.Should().HaveCount(1);
        stock.PostedMovements.Should().HaveCount(1);
        stock.CreatedReceives[0].ItemId.Should().Be(itemId);
        stock.CreatedReceives[0].Quantity.Should().Be(10m);
        stock.CreatedReceives[0].UnitCost.Should().Be(50m);

        // PO should now be Received
        var poFetched = pos.Items[po.Id];
        poFetched.Status.Should().Be(PurchaseOrderStatus.Received);
    }

    [Fact]
    public async Task Receive_WithoutHoldingCompany_Fails()
    {
        var (svc, _, pos, stock, seq, companies, vendors, warehouses) = Build();
        var tenantId = Guid.NewGuid();
        // no holding company
        var itemId = Guid.NewGuid();
        var po = await SeedApprovedPOAsync(pos, vendors, seq, tenantId, (itemId, 5m, 10m, 0m));
        var wh = SeedWarehouse(warehouses, tenantId);
        var created = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = po.Id, ReceivedDate = DateTime.UtcNow, WarehouseId = wh.Id,
            Lines = new List<CreateGoodsReceiptLineRequest>
            {
                new() { ItemId = itemId, Quantity = 5m, UnitCost = 10m }
            }
        }, CancellationToken.None);

        var r = await svc.ReceiveAsync(tenantId, Guid.NewGuid(), created.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ProcurementErrorCode.BusinessRuleViolation);
        stock.CreatedReceives.Should().BeEmpty();
    }

    [Fact]
    public async Task Receive_Twice_FailsWithInvalidStatusTransition()
    {
        var (svc, _, pos, stock, seq, companies, vendors, warehouses) = Build();
        var tenantId = Guid.NewGuid();
        SeedHoldingCompany(companies, tenantId);
        var itemId = Guid.NewGuid();
        var po = await SeedApprovedPOAsync(pos, vendors, seq, tenantId, (itemId, 5m, 10m, 0m));
        var wh = SeedWarehouse(warehouses, tenantId);
        var created = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = po.Id, ReceivedDate = DateTime.UtcNow, WarehouseId = wh.Id,
            Lines = new List<CreateGoodsReceiptLineRequest>
            {
                new() { ItemId = itemId, Quantity = 5m, UnitCost = 10m }
            }
        }, CancellationToken.None);
        await svc.ReceiveAsync(tenantId, Guid.NewGuid(), created.Value!.Id, CancellationToken.None);

        var r2 = await svc.ReceiveAsync(tenantId, Guid.NewGuid(), created.Value!.Id, CancellationToken.None);
        r2.Succeeded.Should().BeFalse();
        r2.ErrorCode.Should().Be(ProcurementErrorCode.InvalidStatusTransition);
    }

    [Fact]
    public async Task GetById_EnrichesWithVendorAndWarehouse()
    {
        var (svc, _, pos, _, seq, _, vendors, warehouses) = Build();
        var tenantId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var po = await SeedApprovedPOAsync(pos, vendors, seq, tenantId, (itemId, 5m, 10m, 0m));
        var wh = SeedWarehouse(warehouses, tenantId, "WH-ENC");
        var created = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = po.Id, ReceivedDate = DateTime.UtcNow, WarehouseId = wh.Id,
            Lines = new List<CreateGoodsReceiptLineRequest>
            {
                new() { ItemId = itemId, Quantity = 5m, UnitCost = 10m }
            }
        }, CancellationToken.None);

        var r = await svc.GetByIdAsync(tenantId, created.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.PoNumber.Should().Be(po.PoNumber);
        r.Value!.PoStatus.Should().Be(PurchaseOrderStatus.Approved.ToString());
        r.Value!.WarehouseName.Should().Be("Main Warehouse");
        r.Value!.WarehouseCode.Should().Be("WH-ENC");
        r.Value!.VendorCode.Should().NotBeNullOrEmpty();
        r.Value!.VendorName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task List_EnrichesAllRecords_NoNPlusOne()
    {
        var (svc, _, pos, _, seq, _, vendors, warehouses) = Build();
        var tenantId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var wh = SeedWarehouse(warehouses, tenantId);

        for (int i = 0; i < 3; i++)
        {
            var po = await SeedApprovedPOAsync(pos, vendors, seq, tenantId, (itemId, 5m, 10m, 0m));
            await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateGoodsReceiptRequest
            {
                PurchaseOrderId = po.Id, ReceivedDate = DateTime.UtcNow, WarehouseId = wh.Id,
                Lines = new List<CreateGoodsReceiptLineRequest> { new() { ItemId = itemId, Quantity = 1m, UnitCost = 10m } }
            }, CancellationToken.None);
        }

        var r = await svc.ListAsync(tenantId, poId: null, status: null, 0, 50, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Should().HaveCount(3);
        // each GR should be enriched
        r.Value!.Should().OnlyContain(g => g.PoNumber != null && g.WarehouseName != null && g.VendorName != null);
    }

    [Fact]
    public async Task GetById_WrongTenant_Fails()
    {
        var (svc, _, pos, _, seq, _, vendors, warehouses) = Build();
        var tenantId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var po = await SeedApprovedPOAsync(pos, vendors, seq, tenantId, (itemId, 5m, 10m, 0m));
        var wh = SeedWarehouse(warehouses, tenantId);
        var created = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateGoodsReceiptRequest
        {
            PurchaseOrderId = po.Id, ReceivedDate = DateTime.UtcNow, WarehouseId = wh.Id,
            Lines = new List<CreateGoodsReceiptLineRequest> { new() { ItemId = itemId, Quantity = 5m, UnitCost = 10m } }
        }, CancellationToken.None);

        var r = await svc.GetByIdAsync(Guid.NewGuid(), created.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ProcurementErrorCode.NotFound);
    }
}
