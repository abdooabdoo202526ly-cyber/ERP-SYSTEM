using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERPSystem.Tests.E2E.TestFixtures;

/// <summary>
/// Spin up the actual ASP.NET Core Host in-process for E2E tests (Sprint-4.5 T-012 / DEC-060).
///
/// Uses WebApplicationFactory&lt;Program&gt; to host the real app.
/// Test config overrides via in-memory configuration.
///
/// Prerequisites:
///   - Postgres running on localhost:5432 (see scripts/local-integration.sh)
///   - DB_CONNECTION env var pointing to test DB
///   - Set Database__AutoMigrate=true so migrations run on startup (or pre-migrate)
/// </summary>
public class ErpWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestTenantId = "11111111-1111-1111-1111-111111111111";
    public const string TestUserId = "22222222-2222-2222-2222-222222222222";

    public const string TestJwtSecret = "E2E_TEST_SECRET_AT_LEAST_32_CHARACTERS_LONG_xxxxxxxxxx";

    /// <summary>
    /// Test database connection string. Override via DB_CONNECTION env var in CI.
    /// </summary>
    public static string TestConnectionString =>
        Environment.GetEnvironmentVariable("DB_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=erp_e2e;Username=postgres;Password=postgres;SSL Mode=Disable";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("E2E");

        builder.ConfigureAppConfiguration((ctx, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = TestJwtSecret,
                ["JwtSettings:Issuer"] = "E2E-TEST",
                ["JwtSettings:Audience"] = "E2E-TEST-Users",
                ["Database:AutoMigrate"] = "true",
                ["Database:SeedAlFajrScenario"] = "false",
                ["Database:SeedAlBurjScenario"] = "false",
                ["Logging:LogLevel:Default"] = "Warning"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddHttpContextAccessor();
        });
    }

    /// <summary>
    /// Returns true if Postgres is reachable. Tests should skip if false.
    /// Used by IClassFixture-based tests to short-circuit gracefully.
    /// </summary>
    public static bool IsDatabaseReachable()
    {
        try
        {
            using var tcp = new System.Net.Sockets.TcpClient();
            var connectTask = tcp.ConnectAsync("localhost", 5432);
            return connectTask.Wait(TimeSpan.FromSeconds(2)) && tcp.Connected;
        }
        catch
        {
            return false;
        }
    }
}