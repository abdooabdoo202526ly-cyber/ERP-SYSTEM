using ERPSystem.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Shared.DataTypes;

/// <summary>
/// DEC-079 PoC: Startup hook that loads JSON DataTypes and reconciles DB schema.
/// Runs AFTER FluentMigrator (which is registered earlier in Program.cs).
/// Additive only — does NOT modify or replace existing migrations.
///
/// Defense Layer 24 candidate: JSON-driven schema migration.
/// </summary>
public sealed class DataTypeHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataTypeHostedService> _logger;
    private readonly IConfiguration _config;

    // Static so debug endpoint (/api/debug/data-types) can read what was loaded
    public static DataTypeRegistry? LastRegistry { get; private set; }
    public static MigrationResult? LastResult { get; private set; }

    public DataTypeHostedService(
        IServiceProvider serviceProvider,
        ILogger<DataTypeHostedService> logger,
        IConfiguration config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var enabled = _config.GetValue("Database:JsonMigrationEnabled", false);
        if (!enabled)
        {
            _logger.LogInformation("[DataTypeMigrator] JsonMigrationEnabled=false — skipping");
            return;
        }

        try
        {
            // Default to <content root>/data-types (frontend app would be /app/data-types at runtime)
            var path = _config.GetValue<string>("Database:JsonMigrationPath");
            if (string.IsNullOrEmpty(path))
            {
                // Best-effort fallback for HF Spaces (cwd is /app, data-types is /app/data-types)
                var cwd = Directory.GetCurrentDirectory();
                path = Path.Combine(cwd, "data-types");
            }

            _logger.LogInformation("[DataTypeMigrator] Loading DataTypes from {Path}", path);

            var registry = new DataTypeRegistry();
            registry.LoadFromDirectory(path);
            LastRegistry = registry;

            _logger.LogInformation("[DataTypeMigrator] Loaded {N} DataTypes ({E} errors)",
                registry.All.Count, registry.Errors.Count);

            foreach (var err in registry.Errors)
            {
                _logger.LogWarning("[DataTypeMigrator] Registry error: {Err}", err);
            }

            if (registry.All.Count == 0)
            {
                _logger.LogInformation("[DataTypeMigrator] No DataTypes to reconcile");
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DataTypeMigrator>>();
            var migrator = new DataTypeMigrator(db, logger);

            var result = await migrator.ReconcileAsync(registry.All, cancellationToken);
            LastResult = result;

            if (result.Errors.Count > 0)
            {
                _logger.LogWarning("[DataTypeMigrator] Reconciliation had {N} errors: {Errors}",
                    result.Errors.Count, string.Join("; ", result.Errors));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DataTypeMigrator] Reconciliation failed");
            // Don't throw — the existing app should keep working even if JSON migrator fails
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}