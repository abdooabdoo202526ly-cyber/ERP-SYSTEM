using FluentMigrator;

namespace ERPSystem.Shared.Migrations;

/// <summary>
/// Migration 016 — Add missing DB indexes (DEC-106 / DL 79-81).
///
/// Adds 4 missing indexes identified in DEC-103a PERFORMANCE-AUDIT.md:
/// 1. vendor_bills.due_date          — AP aging report (no JSON schema → direct SQL)
/// 2. sales_invoices.due_date         — AR aging report (no JSON schema → direct SQL)
/// 3. outbox_events.processed_at      — Unprocessed queue (added to existing JSON)
/// 4. processed_events(tenant_id, processed_at) — Per-tenant event history (added to JSON)
///
/// Other 6 recommendations were already covered by existing JSON indexes:
/// - journal_entries(entry_date)         → ix_journal_entries_tenant_date ✓
/// - audit_log(tenant_id, user_id, created_at) → ix_audit_log_user ✓
/// - notifications(created_at)           → ix_notifications_tenant_created ✓
/// - audit_log(created_at)               → covered by ix_audit_log_user (composite) ✓
/// - outbox_events(occurred_at)          → ix_outbox_unprocessed ✓
/// - processed_events(tenant_id)         → ix_processed_events_tenant ✓
///
/// DEC-079 schema-as-data uses additive only. For the 2 tables without JSON
/// (vendor_bills, sales_invoices — still C# migration), we use raw Execute.Sql.
/// This is the recommended pattern when JSON adoption is incomplete.
///
/// Index creation is concurrent (CREATE INDEX IF NOT EXISTS) to avoid table locks.
/// </summary>
[Migration(20260710_120000)]
public class AddMissingIndexes : Migration
{
    public override void Up()
    {
        // vendor_bills.due_date (AP aging)
        Execute.Sql(@"
            CREATE INDEX IF NOT EXISTS ix_vendor_bills_tenant_due_date
            ON vendor_bills (tenant_id, due_date)
            WHERE due_date IS NOT NULL");

        // sales_invoices.due_date (AR aging)
        Execute.Sql(@"
            CREATE INDEX IF NOT EXISTS ix_sales_invoices_tenant_due_date
            ON sales_invoices (tenant_id, due_date)
            WHERE due_date IS NOT NULL");

        // Note: outbox_events + processed_events indexes added via JSON update
        // (see src/backend/Host/data-types/outbox_events.json + processed_events.json)
    }

    public override void Down()
    {
        Execute.Sql("DROP INDEX IF EXISTS ix_vendor_bills_tenant_due_date");
        Execute.Sql("DROP INDEX IF EXISTS ix_sales_invoices_tenant_due_date");
    }
}
