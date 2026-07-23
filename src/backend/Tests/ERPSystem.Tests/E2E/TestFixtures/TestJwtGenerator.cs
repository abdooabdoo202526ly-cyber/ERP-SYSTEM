using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ERPSystem.Host.Controllers;
using Microsoft.IdentityModel.Tokens;

namespace ERPSystem.Tests.E2E.TestFixtures;

/// <summary>
/// Generates JWTs for E2E tests (Sprint-4.5 T-012 / DEC-060).
/// Tokens signed with the test secret — accepted by the Host's JWT validation.
/// </summary>
public static class TestJwtGenerator
{
    public static string Generate(
        string userId,
        string tenantId,
        string email = "[email protected]",
        string fullName = "E2E Test User",
        string[]? roles = null,
        TimeSpan? expires = null)
    {
        roles ??= new[] { "Admin" };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ErpWebApplicationFactory.TestJwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, userId),
            new("tenantId", tenantId),
            new("tid", tenantId),
            new("fullName", fullName),
            new(ClaimTypes.Role, string.Join(",", roles))
        };

        var token = new JwtSecurityToken(
            issuer: "E2E-TEST",
            audience: "E2E-TEST-Users",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(expires ?? TimeSpan.FromMinutes(30)),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}