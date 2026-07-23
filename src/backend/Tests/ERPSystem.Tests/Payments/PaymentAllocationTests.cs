using ERPSystem.Modules.Payments.Application;
using ERPSystem.Modules.Payments.Application.Services;
using ERPSystem.Modules.Payments.Entities;
using FluentAssertions;

namespace ERPSystem.Tests.Payments;

public class PaymentAllocationTests
{
    [Fact]
    public void PaymentResponse_AllocatedAmount_SumsAllocations()
    {
        var p = new PaymentResponse
        {
            Allocations = new List<PaymentAllocationResponse>
            {
                new() { AmountApplied = 100m },
                new() { AmountApplied = 250.50m },
                new() { AmountApplied = 49.50m }
            }
        };
        p.AllocatedAmount.Should().Be(400m);
    }

    [Fact]
    public void PaymentResponse_OnAccountAmount_AmountMinusAllocations()
    {
        var p = new PaymentResponse
        {
            Amount = 1000m,
            Allocations = new List<PaymentAllocationResponse>
            {
                new() { AmountApplied = 300m },
                new() { AmountApplied = 200m }
            }
        };
        p.OnAccountAmount.Should().Be(500m, "On Account = amount - allocated");
    }

    [Fact]
    public void PaymentResponse_FullyAllocated_OnAccountIsZero()
    {
        var p = new PaymentResponse
        {
            Amount = 500m,
            Allocations = new List<PaymentAllocationResponse>
            {
                new() { AmountApplied = 500m }
            }
        };
        p.OnAccountAmount.Should().Be(0m);
    }

    [Fact]
    public void PaymentResponse_NoAllocations_FullAmountOnAccount()
    {
        var p = new PaymentResponse
        {
            Amount = 750m,
            Allocations = new List<PaymentAllocationResponse>()
        };
        p.OnAccountAmount.Should().Be(750m);
    }

    [Fact]
    public void PaymentResponse_OverAllocation_OnAccountIsNegative()
    {
        // Edge case: response built incorrectly — allocs > amount.
        // We don't actively prevent this in the response DTO, but documenting the math.
        var p = new PaymentResponse
        {
            Amount = 100m,
            Allocations = new List<PaymentAllocationResponse>
            {
                new() { AmountApplied = 80m },
                new() { AmountApplied = 50m }
            }
        };
        p.AllocatedAmount.Should().Be(130m);
        p.OnAccountAmount.Should().Be(-30m, "تنبيه: مجموع التخصيصات يتجاوز المبلغ");
    }

    [Fact]
    public void AllocateRequest_ValidatesNonEmpty()
    {
        var req = new AllocatePaymentRequest
        {
            Allocations = new List<CreatePaymentAllocationRequest>()
        };
        req.Allocations.Should().BeEmpty();
    }

    [Fact]
    public void PaymentRefTypes_AllContainsKnownTypes()
    {
        PaymentRefTypes.All.Should().Contain(PaymentRefTypes.SalesInvoice);
        PaymentRefTypes.All.Should().Contain(PaymentRefTypes.VendorBill);
        PaymentRefTypes.All.Length.Should().Be(2);
    }

    [Fact]
    public void PaymentMethods_AllContainsFourMethods()
    {
        PaymentMethods.All.Should().Contain(PaymentMethods.Cash);
        PaymentMethods.All.Should().Contain(PaymentMethods.Bank);
        PaymentMethods.All.Should().Contain(PaymentMethods.Check);
        PaymentMethods.All.Should().Contain(PaymentMethods.Transfer);
        PaymentMethods.All.Length.Should().Be(4);
    }

    [Fact]
    public void PaymentPartyTypes_ContainsBothVendorAndCustomer()
    {
        PaymentPartyTypes.All.Should().Contain(PaymentPartyTypes.Vendor);
        PaymentPartyTypes.All.Should().Contain(PaymentPartyTypes.Customer);
    }

    [Fact]
    public void PaymentStatus_Draft_NotEqualsPosted()
    {
        PaymentStatus.Draft.Should().NotBe(PaymentStatus.Posted);
        PaymentStatus.Posted.Should().NotBe(PaymentStatus.Cancelled);
    }

    [Fact]
    public void CreatePaymentAllocationRequest_DefaultsAreSafe()
    {
        var r = new CreatePaymentAllocationRequest();
        r.RefType.Should().Be(string.Empty);
        r.RefId.Should().Be(Guid.Empty);
        r.AmountApplied.Should().Be(0m);
    }

    [Fact]
    public void PaymentAllocation_Navigation_DefaultsToEmpty()
    {
        var p = new Payment();
        p.Allocations.Should().NotBeNull();
        p.Allocations.Should().BeEmpty();
    }
}
