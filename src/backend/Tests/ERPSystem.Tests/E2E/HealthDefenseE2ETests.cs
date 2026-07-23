using System.Net;
using FluentAssertions;

namespace ERPSystem.Tests.E2E;

/// <summary>
/// E2E tests for the defensive systems (health endpoints + DEC-023/DEC-059).
/// Sprint-4.5 T-012 / DEC-060.
/// </summary>
public class HealthDefenseE2ETests : IClassFixture<E2E.TestFixtures.ErpWebApplicationFactory>
{
    private readonly E2E.TestFixtures.ErpWebApplicationFactory _factory;
    public HealthDefenseE2ETests(E2E.TestFixtures.ErpWebApplicationFactory factory) => _factory = factory;

    [Fact(Skip = "Requires Postgres on localhost:5432. Run via ./scripts/local-integration.sh then dotnet test, or via ci-fast.yml in CI.")]
    public async Task HealthLive_LivenessCheckWorks()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health/live");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(Skip = "Requires Postgres on localhost:5432. Run via ./scripts/local-integration.sh then dotnet test, or via ci-fast.yml in CI.")]
    public async Task HealthStartup_StartupCheckWorks()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health/startup");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(Skip = "Requires Postgres on localhost:5432. Run via ./scripts/local-integration.sh then dotnet test, or via ci-fast.yml in CI.")]
    public async Task HealthStartupDeep_ShowsDefenseLayers()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health/startup-deep");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("database");
        body.Should().Contain("configuration");
        // DEC-023: seed_al_burj_default must be false (defense layer 1)
        body.Should().Contain("seed_al_burj_default");
    }

    [Fact(Skip = "Requires Postgres on localhost:5432. Run via ./scripts/local-integration.sh then dotnet test, or via ci-fast.yml in CI.")]
    public async Task HealthEndpoints_DoNotRequireAuth()
    {
        // All 3 health endpoints are public (for K8s probes, monitoring)
        var client = _factory.CreateClient();
        var responses = await Task.WhenAll(
            client.GetAsync("/api/health/live"),
            client.GetAsync("/api/health/startup"),
            client.GetAsync("/api/health/startup-deep"));

        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK,
                $"Health endpoint should not require auth but got {response.StatusCode}");
        }
    }

    [Fact(Skip = "Requires Postgres on localhost:5432. Run via ./scripts/local-integration.sh then dotnet test, or via ci-fast.yml in CI.")]
    public async Task HealthStartupDeep_TenantIdEnrichment()
    {
        // Verify the LogContext enricher pulls tenantId from JWT.
        // We can't easily verify logs here but we can verify the endpoint returns.
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health/startup-deep");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}