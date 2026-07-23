using Dapper;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Shared.Events.Application.Services;

public interface IProcessedEventsRepository
{
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct);
    Task MarkProcessedAsync(Guid eventId, Guid tenantId, CancellationToken ct);
}

public sealed class ProcessedEventsRepository : IProcessedEventsRepository
{
    private readonly IDbConnectionFactory _db;
    public ProcessedEventsRepository(IDbConnectionFactory db) => _db = db;

    public async Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var hit = await conn.QueryFirstOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT 1 FROM processed_events WHERE event_id = @EventId LIMIT 1",
            new { EventId = eventId }, cancellationToken: ct));
        return hit.HasValue;
    }

    public async Task MarkProcessedAsync(Guid eventId, Guid tenantId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO processed_events (event_id, tenant_id, processed_at) VALUES (@EventId, @TenantId, @At)",
            new { EventId = eventId, TenantId = tenantId, At = DateTime.UtcNow }, cancellationToken: ct));
    }
}

/// <summary>Handler contract — one per event type. Discovered via DI.</summary>
public interface IIntegrationEventHandler<in T> where T : IIntegrationEvent
{
    Task HandleAsync(T @event, CancellationToken ct);
}
