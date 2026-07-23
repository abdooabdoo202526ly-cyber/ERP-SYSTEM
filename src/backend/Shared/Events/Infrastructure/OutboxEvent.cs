using System;

namespace ERPSystem.Shared.Events.Infrastructure;

/// <summary>
/// Row in outbox_events table.
/// - Inserted in the SAME transaction as the business operation (atomic).
/// - Read by the background processor (every 5s) and dispatched to handlers.
/// </summary>
public class OutboxEvent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public string AggregateType { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public DateTime OccurredAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public string? LastError { get; set; }
}
