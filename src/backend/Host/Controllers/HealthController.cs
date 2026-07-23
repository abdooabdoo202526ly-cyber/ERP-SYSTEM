using Dapper;
using ERPSystem.Shared.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ERPSystem.Host.Controllers;

/// <summary>
/// Health check endpoints for monitoring (Sprint-4 Day 1).
/// </summary>
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Liveness probe — is the process alive?
    /// </summary>
    [HttpGet("live")]
    public IActionResult Live() => Ok(new
    {
        status = "alive",
        timestamp = DateTime.UtcNow,
        version = "1.0.0"
    });

    /// <summary>
    /// Startup probe — has the process finished starting?
    /// </summary>
    [HttpGet("startup")]
    public IActionResult Startup() => Ok(new
    {
        status = "started",
        timestamp = DateTime.UtcNow,
        uptimeSeconds = (DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalSeconds
    });

    /// <summary>
    /// Detailed startup probe — checks DB connectivity, latest applied migration, and configuration.
    /// Use this for deep diagnostics (DB down, missing config, etc.).
    /// </summary>
    [HttpGet("startup-deep")]
    public async Task<IActionResult> StartupDeep(
        [FromServices] IServiceProvider sp,
        [FromServices] IConfiguration config,
        CancellationToken ct)
    {
        var checks = new Dictionary<string, object>();

        // 1) DB connection check (OLTP) + latest applied migration
        long? latestMigration = null;
        int appliedCount = 0;
        try
        {
            using var scope = sp.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var conn = await factory.CreateOltpConnectionAsync(ct);
            var one = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT 1", cancellationToken: ct));
            // FluentMigrator stores applied versions in "versioninfo" table
            // (default table name; schema may vary by configuration)
            try
            {
                latestMigration = await conn.ExecuteScalarAsync<long?>(new CommandDefinition(
                    "SELECT MAX(\"Version\") FROM versioninfo", cancellationToken: ct));
                appliedCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                    "SELECT COUNT(*) FROM versioninfo", cancellationToken: ct));
            }
            catch
            {
                // versioninfo table may not exist yet (pre-migration); treat as informational only
                latestMigration = null;
                appliedCount = 0;
            }
            checks["database"] = new { status = "healthy", ping = one };
        }
        catch (Exception ex)
        {
            checks["database"] = new { status = "unhealthy", error = ex.Message };
        }

        // 2) Migrations summary (best-effort, from versioninfo table)
        checks["migrations"] = new
        {
            applied_count = appliedCount,
            latest_version = latestMigration?.ToString() ?? "unknown"
        };

        // 3) Configuration check
        var dbConnSet = !string.IsNullOrEmpty(config.GetConnectionString("Postgres"));
        var jwtSecretSet = !string.IsNullOrEmpty(config["JwtSettings:Secret"]);
        var seedAlFajrDefault = config.GetValue("Database:SeedAlFajrScenario", true);
        var seedAlBurjDefault = config.GetValue("Database:SeedAlBurjScenario", false);
        checks["configuration"] = new
        {
            db_connection_set = dbConnSet,
            jwt_secret_set = jwtSecretSet,
            seed_al_fajr_default = seedAlFajrDefault,
            seed_al_burj_default = seedAlBurjDefault
        };

        // 4) Tenant & multi-tenancy
        checks["multi_tenancy"] = new { mode = "row-level", isolation = "tenant_id" };

        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            checks = checks
        });
    }
}
