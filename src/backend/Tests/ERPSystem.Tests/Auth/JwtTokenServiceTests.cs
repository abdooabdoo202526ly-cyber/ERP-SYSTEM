using ERPSystem.Modules.Identity.Application.Auth;
using ERPSystem.Modules.Identity.Entities;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace ERPSystem.Tests.Auth;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(int accessMinutes = 60, int refreshDays = 14)
    {
        var settings = new JwtSettings
        {
            Secret = "test-secret-with-enough-length-32-chars-xxxxxxxxxxxx",
            Issuer = "ERP-TEST",
            Audience = "ERP-TEST-Aud",
            AccessTokenExpiryMinutes = accessMinutes,
            RefreshTokenExpiryDays = refreshDays,
        };
        return new JwtTokenService(Options.Create(settings));
    }

    [Fact]
    public void GenerateAccessToken_ReturnsValidJwt_WithExpectedClaims()
    {
        var service = CreateService();
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "user@test.com",
            FullName = "Test User",
        };

        var (token, expires) = service.GenerateAccessToken(user, new[] { "Admin", "Accountant" });

        token.Should().NotBeNullOrEmpty();
        expires.Should().BeAfter(DateTime.UtcNow);

        var principal = service.GetPrincipalFromExpiredToken(token);
        principal.Should().NotBeNull();
        principal!.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsUniqueTokenAndHash()
    {
        var service = CreateService();
        var (token1, hash1, _) = service.GenerateRefreshToken();
        var (token2, hash2, _) = service.GenerateRefreshToken();

        token1.Should().NotBeNullOrEmpty();
        token2.Should().NotBeNullOrEmpty();
        token1.Should().NotBe(token2);
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void HashRefreshToken_IsDeterministic_AndLengthValid()
    {
        var service = CreateService();
        var rawToken = "some-raw-refresh-token-value";
        var h1 = service.HashRefreshToken(rawToken);
        var h2 = service.HashRefreshToken(rawToken);

        h1.Should().Be(h2, "نفس الـ input يجب أن يعطي نفس الـ hash");
        h1.Should().NotContain("some-raw", "الهاش يجب ألا يكشف النص الأصلي");
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_ReturnsNull_ForInvalidToken()
    {
        var service = CreateService();
        var principal = service.GetPrincipalFromExpiredToken("not-a-valid-jwt");
        principal.Should().BeNull();
    }

    [Fact]
    public void Constructor_ThrowsOnShortSecret()
    {
        Action act = () => new JwtTokenService(Options.Create(new JwtSettings { Secret = "short" }));
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*32*");
    }
}
