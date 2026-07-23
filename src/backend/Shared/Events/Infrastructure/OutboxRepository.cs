using Dapper;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Shared.Events.Infrastructure;

public interface IOutboxRepository
{
    Task InsertAsync(OutboxEvent evt, CancellationToken ct);
    Task<IReadOnlyList<OutboxEvent>> FetchUnprocessedAsync(int batchSize, CancellationToken ct);
    Task MarkProcessedAsync(Guid id, DateTime processedAt, CancellationToken ct);
    Task MarkFailedAsync(Guid id, int retryCount, string error, CancellationToken ct);
    Task ResetForRetryAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<OutboxEvent>> ListAllAsync(Guid tenantId, bool unprocessedOnly, int skip, int take, CancellationToken ct);
    Task<OutboxEvent?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<int> CountPendingAsync(Guid tenantId, CancellationToken ct);
}

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly IDbConnectionFactory _db;
    public OutboxRepository(IDbConnectionFactory db) => _db = db;
    private const string Sel = @"id, tenant_id AS TenantId, event_type AS EventType, aggregate_id AS AggregateId,
        aggregate_type AS AggregateType, payload, occurred_at AS OccurredAt,
        processed_at AS ProcessedAt, retry_count AS RetryCount, max_retries AS MaxRetries, last_error AS LastError";

    public async Task InsertAsync(OutboxEvent evt, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO outbox_events (id, tenant_id, event_type, aggregate_id, aggregate_type,
                                       payload, occurred_at, processed_at, retry_count, max_retries, last_error)
            VALUES (@Id, @TenantId, @EventType, @AggregateId, @AggregateType,
                    @Payload, @OccurredAt, @ProcessedAt, @RetryCount, @MaxRetries, @LastError)", evt, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<OutboxEvent>> FetchUnprocessedAsync(int batchSize, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var rows = await conn.QueryAsync<OutboxEvent>(new CommandDefinition(@$"
            SELECT {Sel} FROM outbox_events
            WHERE processed_at IS NULL AND retry_count < max_retries
            ORDER BY occurred_at
            LIMIT @BatchSize", new { BatchSize = batchSize }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task MarkProcessedAsync(Guid id, DateTime processedAt, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE outbox_events SET processed_at = @At WHERE id = @Id",
            new { Id = id, At = processedAt }, cancellationToken: ct));
    }

    public async Task MarkFailedAsync(Guid id, int retryCount, string error, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE outbox_events SET retry_count = @Retry, last_error = @Error WHERE id = @Id",
            new { Id = id, Retry = retryCount, Error = error.Length > 4000 ? error.Substring(0, 4000) : error }, cancellationToken: ct));
    }

    public async Task ResetForRetryAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE outbox_events SET retry_count = 0, last_error = NULL, processed_at = NULL WHERE id = @Id",
            new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<OutboxEvent>> ListAllAsync(Guid tenantId, bool unprocessedOnly, int skip, int take, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {Sel} FROM outbox_events WHERE tenant_id = @TenantId"
            + (unprocessedOnly ? " AND processed_at IS NULL" : "")
            + " ORDER BY occurred_at DESC OFFSET @Skip LIMIT @Take";
        var rows = await conn.QueryAsync<OutboxEvent>(new CommandDefinition(sql,
            new { TenantId = tenantId, Skip = skip, Take = take }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<OutboxEvent?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<OutboxEvent>(new CommandDefinition(
            $"SELECT {Sel} FROM outbox_events WHERE id = @Id LIMIT 1", new { Id = id }, cancellationToken: ct));
    }

    public async Task<int> CountPendingAsync(Guid tenantId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM outbox_events WHERE tenant_id = @TenantId AND processed_at IS NULL",
            new { TenantId = tenantId }, cancellationToken: ct));
    }
}
