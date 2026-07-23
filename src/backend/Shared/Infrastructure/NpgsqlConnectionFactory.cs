using System.Data;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ERPSystem.Shared.Infrastructure;

/// <summary>
/// تنفيذ IDbConnectionFactory باستخدام Npgsql
/// </summary>
public sealed class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly NpgsqlConnectionOptions _options;
    private readonly ILogger<NpgsqlConnectionFactory> _logger;

    public NpgsqlConnectionFactory(IOptions<NpgsqlConnectionOptions> options, ILogger<NpgsqlConnectionFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IDbConnection> CreateOltpConnectionAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.OltpConnectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Postgres غير معرّف في الإعدادات.");
        }
        var conn = new NpgsqlConnection(_options.OltpConnectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        _logger.LogDebug("فُتح اتصال OLTP جديد (DB={Database})", conn.Database);
        return conn;
    }

    public async Task<IDbConnection> CreateEventStoreConnectionAsync(CancellationToken ct = default)
    {
        var cs = _options.EventStoreConnectionString ?? _options.OltpConnectionString;
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException("ConnectionStrings:Marten غير معرّف في الإعدادات.");
        }
        var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync(ct).ConfigureAwait(false);
        _logger.LogDebug("فُتح اتصال EventStore جديد (DB={Database})", conn.Database);
        return conn;
    }
}

/// <summary>إعدادات الاتصال بقواعد البيانات</summary>
public sealed class NpgsqlConnectionOptions
{
    public string OltpConnectionString { get; set; } = string.Empty;
    public string? EventStoreConnectionString { get; set; }
}
