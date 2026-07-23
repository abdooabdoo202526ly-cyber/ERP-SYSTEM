using System.Text.Json;
using ERPSystem.Shared.Events.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Shared.Events.Application.Services;

public interface IEventBus
{
    /// <summary>
    /// Persists the event to outbox_events in the current DbContext/transaction
    /// (so the publish is atomic with the business operation).
    /// </summary>
    Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : IIntegrationEvent;
}

public sealed class EventBus : IEventBus
{
    private readonly IOutboxRepository _outbox;
    private readonly ILogger<EventBus> _logger;
    public EventBus(IOutboxRepository outbox, ILogger<EventBus> logger)
    {
        _outbox = outbox; _logger = logger;
    }

    public async Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : IIntegrationEvent
    {
        var type = typeof(T);
        // record types: use the clean name (StockReceivedEvent, etc.)
        var eventType = type.Name;
        var aggregateType = type.Name.Replace("Event", "");  // "StockReceived", "StockIssued", "JournalEntryPosted" -> ...
        // For more accurate aggregate_type, hardcode below in handlers — but this is good enough
        var aggregateType2 = AggregateTypeOf(@event);

        var payload = JsonSerializer.Serialize(@event, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var row = new OutboxEvent
        {
            Id = Guid.NewGuid(),
            TenantId = @event.TenantId,
            EventType = eventType,
            AggregateId = @event.EventId,  // EventId is the dedup key; but AggregateId is for routing
            AggregateType = aggregateType2,
            Payload = payload,
            OccurredAt = @event.OccurredAt,
            ProcessedAt = null,
            RetryCount = 0,
            MaxRetries = 3
        };
        await _outbox.InsertAsync(row, ct);
        _logger.LogInformation("Published {EventType} for tenant {TenantId} (EventId={EventId})", eventType, @event.TenantId, @event.EventId);
    }

    private static string AggregateTypeOf(IIntegrationEvent evt) => evt switch
    {
        StockReceivedEvent => "StockMovement",
        StockIssuedEvent => "StockMovement",
        JournalEntryPostedEvent => "JournalEntry",
        _ => "Unknown"
    };
}
