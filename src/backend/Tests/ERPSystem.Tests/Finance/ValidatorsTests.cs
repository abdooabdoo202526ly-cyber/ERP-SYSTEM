using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Entities;
using FluentAssertions;

namespace ERPSystem.Tests.Finance;

public class CreateAccountRequestValidatorTests
{
    private readonly CreateAccountRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var req = new CreateAccountRequest
        {
            Code = "1100",
            Name = "النقدية",
            Type = AccountType.Asset,
        };
        _validator.Validate(req).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("has spaces")]
    [InlineData("special$chars")]
    public void InvalidCode_FailsValidation(string code)
    {
        var req = new CreateAccountRequest { Code = code, Name = "test", Type = AccountType.Asset };
        _validator.Validate(req).IsValid.Should().BeFalse();
    }

    [Fact]
    public void MissingType_FailsValidation()
    {
        var req = new CreateAccountRequest { Code = "1100", Name = "test" };
        _validator.Validate(req).IsValid.Should().BeFalse();
    }
}

public class PostJournalEntryRequestValidatorTests
{
    private readonly PostJournalEntryRequestValidator _validator = new();

    [Fact]
    public void BalancedTwoLineEntry_PassesValidation()
    {
        var req = new PostJournalEntryRequest
        {
            EntryDate = DateTime.UtcNow,
            Description = "قيد اختبار",
            Lines = new()
            {
                new() { AccountId = Guid.NewGuid(), Debit = 1000, Credit = 0 },
                new() { AccountId = Guid.NewGuid(), Debit = 0, Credit = 1000 }
            }
        };
        _validator.Validate(req).IsValid.Should().BeTrue();
    }

    [Fact]
    public void SingleLineEntry_FailsValidation()
    {
        var req = new PostJournalEntryRequest
        {
            EntryDate = DateTime.UtcNow,
            Description = "قيد بسطر واحد",
            Lines = new() { new() { AccountId = Guid.NewGuid(), Debit = 100 } }
        };
        _validator.Validate(req).IsValid.Should().BeFalse();
    }

    [Fact]
    public void LineWithBothDebitAndCredit_FailsValidation()
    {
        var req = new PostJournalEntryRequest
        {
            EntryDate = DateTime.UtcNow,
            Description = "قيد غير صحيح",
            Lines = new()
            {
                new() { AccountId = Guid.NewGuid(), Debit = 100, Credit = 100 },
                new() { AccountId = Guid.NewGuid(), Debit = 0, Credit = 0 }
            }
        };
        _validator.Validate(req).IsValid.Should().BeFalse();
    }

    [Fact]
    public void EmptyDescription_FailsValidation()
    {
        var req = new PostJournalEntryRequest
        {
            EntryDate = DateTime.UtcNow,
            Description = "",
            Lines = new()
            {
                new() { AccountId = Guid.NewGuid(), Debit = 100, Credit = 0 },
                new() { AccountId = Guid.NewGuid(), Debit = 0, Credit = 100 }
            }
        };
        _validator.Validate(req).IsValid.Should().BeFalse();
    }
}
