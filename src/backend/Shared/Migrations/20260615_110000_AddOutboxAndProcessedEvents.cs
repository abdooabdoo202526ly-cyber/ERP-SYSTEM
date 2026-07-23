using FluentMigrator;

namespace ERPSystem.Shared.Migrations;

/// <summary>
/// Migration 007 — Event Bus (Outbox Pattern) + Idempotency
///
/// - outbox_events: queue of integration events. Inserted in same transaction
///   as the business operation (atomic). Background processor reads + dispatches.
/// - processed_events: idempotency — if EventId already here, skip (duplicate).
/// </summary>
[Migration(20260615_110000)]
public class AddOutboxAndProcessedEvents : Migration
{
    public override void Up()
    {
        // DEC-082: NoOp — schema now defined in JSON: outbox_events, processed_events
        // The DataTypeMigrator (DEC-079) handles all additive schema changes.
        // This migration is kept so FluentMigrator versioninfo still records it as applied.
    }

    public override void Down()
    {
        Delete.Table("processed_events");
        Delete.Table("outbox_events");
    }
}
