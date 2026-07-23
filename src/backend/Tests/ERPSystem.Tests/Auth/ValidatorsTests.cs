using ERPSystem.Modules.Identity.Application.Auth;
using FluentAssertions;

namespace ERPSystem.Tests.Auth;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var req = new RegisterRequest
        {
            TenantId = Guid.NewGuid(),
            Email = "user@example.com",
            Password = "Strong1Pass",
            FullName = "Test User"
        };
        var result = _validator.Validate(req);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void NewTenantName_IsAccepted_WhenNoTenantId()
    {
        var req = new RegisterRequest
        {
            TenantId = Guid.Empty,
            TenantName = "Acme Co",
            Email = "founder@acme.com",
            Password = "Strong1Pass",
            FullName = "Founder"
        };
        var result = _validator.Validate(req);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("user@")]
    public void InvalidEmail_FailsValidation(string email)
    {
        var req = new RegisterRequest
        {
            TenantId = Guid.NewGuid(),
            Email = email,
            Password = "Strong1Pass",
            FullName = "Test"
        };
        _validator.Validate(req).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("short1A")]        // < 8 chars
    [InlineData("alllowercase1")]  // no uppercase
    [InlineData("ALLUPPERCASE1")]  // no lowercase
    [InlineData("NoDigitsHere")]   // no digit
    public void WeakPassword_FailsValidation(string password)
    {
        var req = new RegisterRequest
        {
            TenantId = Guid.NewGuid(),
            Email = "user@example.com",
            Password = password,
            FullName = "Test"
        };
        _validator.Validate(req).IsValid.Should().BeFalse();
    }

    [Fact]
    public void MissingBothTenantIdAndName_FailsValidation()
    {
        var req = new RegisterRequest
        {
            TenantId = Guid.Empty,
            TenantName = null,
            Email = "user@example.com",
            Password = "Strong1Pass",
            FullName = "Test"
        };
        _validator.Validate(req).IsValid.Should().BeFalse();
    }
}
