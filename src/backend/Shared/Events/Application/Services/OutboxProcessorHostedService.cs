using System.Text.Json;
using ERPSystem.Shared.Events;
using ERPSystem.Shared.Events.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Shared.Events.Application.Services;

/// <summary>
/// Background job: reads unprocessed events from outbox_events,
/// finds the matching IIntegrationEventHandler<T> via DI, and dispatches.
/// Idempotent via processed_events table (EventId-based dedup).
///
/// Every 5 seconds. Production-grade: would use Postgres LISTEN/NOTIFY
/// or FOR UPDATE SKIP LOCKED for true real-time. Phase 2.4 keeps it simple.
/// </summary>
public sealed class OutboxProcessorHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessorHostedService> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 50;

    public OutboxProcessorHostedService(IServiceProvider sp, ILogger<OutboxProcessorHostedService> logger)
    {
        _serviceProvider = sp; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor started. Polling every {Seconds}s, batch={Batch}", PollInterval.TotalSeconds, BatchSize);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxProcessor batch failed");
            }
            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
        _logger.LogInformation("OutboxProcessor stopped");
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var processed = scope.ServiceProvider.GetRequiredService<IProcessedEventsRepository>();

        var pending = await outbox.FetchUnprocessedAsync(BatchSize, ct);
        if (pending.Count == 0) return;

        _logger.LogDebug("OutboxProcessor picked {Count} events", pending.Count);
        foreach (var evt in pending)
        {
            // Idempotency check
            if (await processed.IsProcessedAsync(evt.Id, ct))
            {
                _logger.LogDebug("Event {EventId} already processed — marking outbox row", evt.Id);
                await outbox.MarkProcessedAsync(evt.Id, DateTime.UtcNow, ct);
                continue;
            }

            try
            {
                await DispatchAsync(evt, scope.ServiceProvider, ct);
                await processed.MarkProcessedAsync(evt.Id, evt.TenantId, ct);
                await outbox.MarkProcessedAsync(evt.Id, DateTime.UtcNow, ct);
            }
            catch (Exception ex)
            {
                var newRetry = evt.RetryCount + 1;
                var errorMsg = ex.Message;
                _logger.LogWarning(ex, "Outbox event {EventId} ({EventType}) failed (retry {Retry}/{Max})",
                    evt.Id, evt.EventType, newRetry, evt.MaxRetries);
                await outbox.MarkFailedAsync(evt.Id, newRetry, errorMsg, ct);
                if (newRetry >= evt.MaxRetries)
                {
                    _logger.LogError("Outbox event {EventId} hit max retries — marking processed to stop loop", evt.Id);
                    await processed.MarkProcessedAsync(evt.Id, evt.TenantId, ct);
                    await outbox.MarkProcessedAsync(evt.Id, DateTime.UtcNow, ct);
                }
            }
        }
    }

    private async Task DispatchAsync(OutboxEvent evt, IServiceProvider sp, CancellationToken ct)
    {
        switch (evt.EventType)
        {
            case nameof(StockReceivedEvent):
                var received = JsonSerializer.Deserialize<StockReceivedEvent>(evt.Payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (received == null) throw new InvalidOperationException("Failed to deserialize StockReceivedEvent");
                var h1 = sp.GetService<IIntegrationEventHandler<StockReceivedEvent>>();
                if (h1 != null) await h1.HandleAsync(received, ct);
                break;
            case nameof(StockIssuedEvent):
                var issued = JsonSerializer.Deserialize<StockIssuedEvent>(evt.Payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (issued == null) throw new InvalidOperationException("Failed to deserialize StockIssuedEvent");
                var h2 = sp.GetService<IIntegrationEventHandler<StockIssuedEvent>>();
                if (h2 != null) await h2.HandleAsync(issued, ct);
                break;
            case nameof(StockTransferredEvent):
                var transferred = JsonSerializer.Deserialize<StockTransferredEvent>(evt.Payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (transferred == null) throw new InvalidOperationException("Failed to deserialize StockTransferredEvent");
                var ht = sp.GetService<IIntegrationEventHandler<StockTransferredEvent>>();
                if (ht != null) await ht.HandleAsync(transferred, ct);
                break;
            case nameof(StockAdjustedEvent):
                var adjusted = JsonSerializer.Deserialize<StockAdjustedEvent>(evt.Payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (adjusted == null) throw new InvalidOperationException("Failed to deserialize StockAdjustedEvent");
                var ha = sp.GetService<IIntegrationEventHandler<StockAdjustedEvent>>();
                if (ha != null) await ha.HandleAsync(adjusted, ct);
                break;
            case nameof(JournalEntryPostedEvent):
                var journaled = JsonSerializer.Deserialize<JournalEntryPostedEvent>(evt.Payload,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (journaled == null) throw new InvalidOperationException("Failed to deserialize JournalEntryPostedEvent");
                var h3 = sp.GetService<IIntegrationEventHandler<JournalEntryPostedEvent>>();
                if (h3 != null) await h3.HandleAsync(journaled, ct);
                break;
            default:
                _logger.LogWarning("Outbox event {EventId} has unknown event type '{Type}' — skipping", evt.Id, evt.EventType);
                break;
        }
    }
}
