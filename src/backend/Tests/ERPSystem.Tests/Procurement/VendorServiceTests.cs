using ERPSystem.Modules.Procurement.Application;
using ERPSystem.Modules.Procurement.Application.Services;
using ERPSystem.Modules.Procurement.Entities;
using ERPSystem.Modules.Procurement.Infrastructure;
using FluentAssertions;

namespace ERPSystem.Tests.Procurement;

public class VendorServiceTests
{
    private static (VendorService svc, FakeVendorRepository repo) Build()
    {
        var repo = new FakeVendorRepository();
        var svc = new VendorService(repo);
        return (svc, repo);
    }

    [Fact]
    public async Task Create_DefaultsToActive_StoresAllFields()
    {
        var (svc, repo) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var r = await svc.CreateAsync(tenantId, userId, new CreateVendorRequest
        {
            Code = "V-001",
            Name = "Vendor Test",
            Email = "vendor@example.com",
            Phone = "+218-91-1234567",
            TaxNumber = "TAX-001",
            Website = "https://vendor.example.com",
            Currency = "LYD",
            PaymentTerms = PaymentTerms.Net30
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.Code.Should().Be("V-001");
        r.Value.Name.Should().Be("Vendor Test");
        r.Value.Email.Should().Be("vendor@example.com");
        r.Value.Website.Should().Be("https://vendor.example.com");
        r.Value.Currency.Should().Be("LYD");
        r.Value.PaymentTerms.Should().Be(PaymentTerms.Net30);
        r.Value.IsActive.Should().BeTrue();

        repo.Items.Count.Should().Be(1);
        repo.Items.Values.First().TenantId.Should().Be(tenantId);
        repo.Items.Values.First().CreatedBy.Should().Be(userId);
    }

    [Fact]
    public async Task Create_DuplicateCode_Fails()
    {
        var (svc, _) = Build();
        var tenantId = Guid.NewGuid();
        await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateVendorRequest
        {
            Code = "V-DUP", Name = "A", PaymentTerms = PaymentTerms.Cash
        }, CancellationToken.None);

        var r2 = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateVendorRequest
        {
            Code = "V-DUP", Name = "B", PaymentTerms = PaymentTerms.Cash
        }, CancellationToken.None);

        r2.Succeeded.Should().BeFalse();
        r2.ErrorCode.Should().Be(ProcurementErrorCode.AlreadyExists);
    }

    [Fact]
    public async Task Create_CodeIsUniquePerTenant()
    {
        var (svc, _) = Build();
        // Same code in different tenants — both should succeed
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var r1 = await svc.CreateAsync(t1, Guid.NewGuid(), new CreateVendorRequest
        {
            Code = "V-SAME", Name = "T1", PaymentTerms = PaymentTerms.Net30
        }, CancellationToken.None);
        var r2 = await svc.CreateAsync(t2, Guid.NewGuid(), new CreateVendorRequest
        {
            Code = "V-SAME", Name = "T2", PaymentTerms = PaymentTerms.Net30
        }, CancellationToken.None);

        r1.Succeeded.Should().BeTrue();
        r2.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Update_ChangesFields_PreservesTenant()
    {
        var (svc, _) = Build();
        var tenantId = Guid.NewGuid();
        var created = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateVendorRequest
        {
            Code = "V-UPD", Name = "Old Name", Email = "old@x.com", PaymentTerms = PaymentTerms.Cash
        }, CancellationToken.None);

        var r = await svc.UpdateAsync(tenantId, Guid.NewGuid(), created.Value!.Id, new UpdateVendorRequest
        {
            Name = "New Name",
            Email = "new@x.com",
            Currency = "USD",
            PaymentTerms = PaymentTerms.Net60,
            IsActive = true,
            Website = "https://new.example.com"
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.Name.Should().Be("New Name");
        r.Value.Email.Should().Be("new@x.com");
        r.Value.Currency.Should().Be("USD");
        r.Value.PaymentTerms.Should().Be(PaymentTerms.Net60);
        r.Value.Website.Should().Be("https://new.example.com");
        r.Value.TenantId.Should().Be(tenantId);

        // Confirm persisted
        var fetched = await svc.GetByIdAsync(tenantId, created.Value!.Id, CancellationToken.None);
        fetched.Value!.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task Update_WrongTenant_Fails()
    {
        var (svc, _) = Build();
        var created = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateVendorRequest
        {
            Code = "V-UT", Name = "X", PaymentTerms = PaymentTerms.Cash
        }, CancellationToken.None);

        var r = await svc.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), created.Value!.Id, new UpdateVendorRequest
        {
            Name = "Y", PaymentTerms = PaymentTerms.Cash
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ProcurementErrorCode.NotFound);
    }

    [Fact]
    public async Task GetById_WrongTenant_Fails()
    {
        var (svc, _) = Build();
        var created = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateVendorRequest
        {
            Code = "V-GT", Name = "X", PaymentTerms = PaymentTerms.Cash
        }, CancellationToken.None);

        var r = await svc.GetByIdAsync(Guid.NewGuid(), created.Value!.Id, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ProcurementErrorCode.NotFound);
    }

    [Fact]
    public async Task List_FiltersByIncludeInactive()
    {
        var (svc, _) = Build();
        var tenantId = Guid.NewGuid();
        var a = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateVendorRequest
        {
            Code = "V-A", Name = "Active", PaymentTerms = PaymentTerms.Cash
        }, CancellationToken.None);
        var b = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateVendorRequest
        {
            Code = "V-B", Name = "To Deactivate", PaymentTerms = PaymentTerms.Cash
        }, CancellationToken.None);
        await svc.DeactivateAsync(tenantId, Guid.NewGuid(), b.Value!.Id, CancellationToken.None);

        var activeOnly = await svc.ListAsync(tenantId, includeInactive: false, 0, 50, CancellationToken.None);
        var all = await svc.ListAsync(tenantId, includeInactive: true, 0, 50, CancellationToken.None);

        activeOnly.Value!.Should().HaveCount(1);
        activeOnly.Value![0].Id.Should().Be(a.Value!.Id);

        all.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task Deactivate_SetsIsActiveFalse()
    {
        var (svc, _) = Build();
        var tenantId = Guid.NewGuid();
        var v = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateVendorRequest
        {
            Code = "V-DA", Name = "X", PaymentTerms = PaymentTerms.Cash
        }, CancellationToken.None);

        var r = await svc.DeactivateAsync(tenantId, Guid.NewGuid(), v.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();

        var fetched = await svc.GetByIdAsync(tenantId, v.Value!.Id, CancellationToken.None);
        fetched.Value!.IsActive.Should().BeFalse();
    }
}
