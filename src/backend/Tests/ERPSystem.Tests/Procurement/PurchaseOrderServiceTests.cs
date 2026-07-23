using ERPSystem.Modules.Procurement.Application;
using ERPSystem.Modules.Procurement.Application.Services;
using ERPSystem.Modules.Procurement.Entities;
using ERPSystem.Modules.Procurement.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERPSystem.Tests.Procurement;

public class PurchaseOrderServiceTests
{
    private static (PurchaseOrderService svc, FakePurchaseOrderRepository pos, FakeVendorRepository vendors) Build()
    {
        var pos = new FakePurchaseOrderRepository();
        var vendors = new FakeVendorRepository();
        var seq = new FakeDocumentSequenceRepository();
        var svc = new PurchaseOrderService(pos, vendors, seq, NullLogger<PurchaseOrderService>.Instance);
        return (svc, pos, vendors);
    }

    private static Vendor SeedVendor(FakeVendorRepository vendors, Guid tenantId, string code = "V-001", bool isActive = true)
    {
        var v = new Vendor
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = code, Name = "Vendor Test",
            Currency = "LYD", PaymentTerms = PaymentTerms.Net30, IsActive = isActive,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
        };
        vendors.Items[v.Id] = v;
        return v;
    }

    [Fact]
    public async Task Create_GeneratesPoNumber_CalculatesTotals_StartsAtDraft()
    {
        var (svc, pos, vendors) = Build();
        var tenantId = Guid.NewGuid();
        var v = SeedVendor(vendors, tenantId);

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreatePurchaseOrderRequest
        {
            VendorId = v.Id,
            OrderDate = DateTime.UtcNow,
            Currency = "LYD",
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { ItemId = Guid.NewGuid(), Quantity = 10, UnitPrice = 50m,  TaxRate = 0.15m },
                new() { ItemId = Guid.NewGuid(), Quantity =  5, UnitPrice = 100m, TaxRate = 0m   }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.PoNumber.Should().StartWith("PO-");
        r.Value.Status.Should().Be(PurchaseOrderStatus.Draft);
        r.Value.SubTotal.Should().Be(1000m, "10*50 + 5*100");
        r.Value.TaxAmount.Should().Be(75m, "10*50*0.15");
        r.Value.TotalAmount.Should().Be(1075m);
        r.Value.Lines.Should().HaveCount(2);
        r.Value.Lines[0].LineOrder.Should().Be(0);
        r.Value.Lines[1].LineOrder.Should().Be(1);

        pos.Items.Count.Should().Be(1);
        pos.Lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_VendorNotFound_Fails()
    {
        var (svc, _, _) = Build();
        var r = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreatePurchaseOrderRequest
        {
            VendorId = Guid.NewGuid(),
            OrderDate = DateTime.UtcNow,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 10m, TaxRate = 0m }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ProcurementErrorCode.NotFound);
    }

    [Fact]
    public async Task Create_VendorInactive_Fails()
    {
        var (svc, _, vendors) = Build();
        var tenantId = Guid.NewGuid();
        var v = SeedVendor(vendors, tenantId, "V-INA", isActive: false);

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreatePurchaseOrderRequest
        {
            VendorId = v.Id,
            OrderDate = DateTime.UtcNow,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 10m, TaxRate = 0m }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ProcurementErrorCode.BusinessRuleViolation);
    }

    [Fact]
    public async Task Create_VendorInDifferentTenant_Fails()
    {
        var (svc, _, vendors) = Build();
        // vendor belongs to different tenant
        var v = SeedVendor(vendors, Guid.NewGuid(), "V-OTHER");
        var r = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreatePurchaseOrderRequest
        {
            VendorId = v.Id,
            OrderDate = DateTime.UtcNow,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 10m, TaxRate = 0m }
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ProcurementErrorCode.NotFound);
    }

    [Fact]
    public async Task Approve_DraftToApproved_Succeeds()
    {
        var (svc, _, vendors) = Build();
        var tenantId = Guid.NewGuid();
        var v = SeedVendor(vendors, tenantId);
        var po = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreatePurchaseOrderRequest
        {
            VendorId = v.Id,
            OrderDate = DateTime.UtcNow,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 10m, TaxRate = 0m }
            }
        }, CancellationToken.None);

        var r = await svc.ApproveAsync(tenantId, Guid.NewGuid(), po.Value!.Id, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(PurchaseOrderStatus.Approved);
        r.Value.ApprovedAt.Should().NotBeNull();
        r.Value.ApprovedBy.Should().NotBeNull();
    }

    [Fact]
    public async Task Approve_AlreadyApproved_Fails()
    {
        var (svc, _, vendors) = Build();
        var tenantId = Guid.NewGuid();
        var v = SeedVendor(vendors, tenantId);
        var po = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreatePurchaseOrderRequest
        {
            VendorId = v.Id,
            OrderDate = DateTime.UtcNow,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 10m, TaxRate = 0m }
            }
        }, CancellationToken.None);
        await svc.ApproveAsync(tenantId, Guid.NewGuid(), po.Value!.Id, CancellationToken.None);

        var r2 = await svc.ApproveAsync(tenantId, Guid.NewGuid(), po.Value!.Id, CancellationToken.None);
        r2.Succeeded.Should().BeFalse();
        r2.ErrorCode.Should().Be(ProcurementErrorCode.InvalidStatusTransition);
    }

    [Fact]
    public async Task Send_RequiresApproved()
    {
        var (svc, _, vendors) = Build();
        var tenantId = Guid.NewGuid();
        var v = SeedVendor(vendors, tenantId);
        var po = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreatePurchaseOrderRequest
        {
            VendorId = v.Id,
            OrderDate = DateTime.UtcNow,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 10m, TaxRate = 0m }
            }
        }, CancellationToken.None);

        // Sending directly from Draft must fail
        var r1 = await svc.SendAsync(tenantId, Guid.NewGuid(), po.Value!.Id, CancellationToken.None);
        r1.Succeeded.Should().BeFalse();
        r1.ErrorCode.Should().Be(ProcurementErrorCode.InvalidStatusTransition);

        // approve then send
        await svc.ApproveAsync(tenantId, Guid.NewGuid(), po.Value!.Id, CancellationToken.None);
        var r2 = await svc.SendAsync(tenantId, Guid.NewGuid(), po.Value!.Id, CancellationToken.None);
        r2.Succeeded.Should().BeTrue();
        r2.Value!.Status.Should().Be(PurchaseOrderStatus.Sent);
        r2.Value.SentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetById_WrongTenant_Fails()
    {
        var (svc, _, vendors) = Build();
        var tenantId = Guid.NewGuid();
        var v = SeedVendor(vendors, tenantId);
        var po = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreatePurchaseOrderRequest
        {
            VendorId = v.Id,
            OrderDate = DateTime.UtcNow,
            Lines = new List<CreatePurchaseOrderLineRequest>
            {
                new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 10m, TaxRate = 0m }
            }
        }, CancellationToken.None);

        var r = await svc.GetByIdAsync(Guid.NewGuid(), po.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ProcurementErrorCode.NotFound);
    }

    [Fact]
    public async Task List_FiltersByVendorAndStatus()
    {
        var (svc, _, vendors) = Build();
        var tenantId = Guid.NewGuid();
        var v1 = SeedVendor(vendors, tenantId, "V-1");
        var v2 = SeedVendor(vendors, tenantId, "V-2");

        var p1 = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreatePurchaseOrderRequest
        {
            VendorId = v1.Id, OrderDate = DateTime.UtcNow,
            Lines = new List<CreatePurchaseOrderLineRequest> { new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m, TaxRate = 0m } }
        }, CancellationToken.None);
        var p2 = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreatePurchaseOrderRequest
        {
            VendorId = v2.Id, OrderDate = DateTime.UtcNow,
            Lines = new List<CreatePurchaseOrderLineRequest> { new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m, TaxRate = 0m } }
        }, CancellationToken.None);

        // make p1 Approved
        await svc.ApproveAsync(tenantId, Guid.NewGuid(), p1.Value!.Id, CancellationToken.None);

        var byVendor = await svc.ListAsync(tenantId, v1.Id, status: null, 0, 50, CancellationToken.None);
        byVendor.Value!.Should().HaveCount(1);

        var approved = await svc.ListAsync(tenantId, vendorId: null, status: PurchaseOrderStatus.Approved, 0, 50, CancellationToken.None);
        approved.Value!.Should().HaveCount(1);
        approved.Value![0].Id.Should().Be(p1.Value!.Id);

        var all = await svc.ListAsync(tenantId, vendorId: null, status: null, 0, 50, CancellationToken.None);
        all.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_PoNumberIsUnique_AcrossCalls()
    {
        var (svc, _, vendors) = Build();
        var tenantId = Guid.NewGuid();
        var v = SeedVendor(vendors, tenantId);

        var p1 = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreatePurchaseOrderRequest
        {
            VendorId = v.Id, OrderDate = DateTime.UtcNow,
            Lines = new List<CreatePurchaseOrderLineRequest> { new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m, TaxRate = 0m } }
        }, CancellationToken.None);
        var p2 = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreatePurchaseOrderRequest
        {
            VendorId = v.Id, OrderDate = DateTime.UtcNow,
            Lines = new List<CreatePurchaseOrderLineRequest> { new() { ItemId = Guid.NewGuid(), Quantity = 1, UnitPrice = 1m, TaxRate = 0m } }
        }, CancellationToken.None);

        p1.Value!.PoNumber.Should().NotBe(p2.Value!.PoNumber);
    }
}
