using FluentMigrator;

namespace ERPSystem.Shared.Migrations;

/// <summary>
/// Migration 015 — audit_log table (Sprint-4.5: Stability & Shipping).
///
/// Tracks CREATE/UPDATE/DELETE on Finance + Projects entities.
/// DEC-056: First deliverable of Sprint-4.5 audit subsystem.
/// </summary>
[Migration(20260706_120000)]
public class AddAuditLog : Migration
{
    public override void Up()
    {
        // DEC-082: NoOp — schema now defined in JSON: audit_log (new table + indexes)
        // The DataTypeMigrator (DEC-079) handles all additive schema changes.
        // This migration is kept so FluentMigrator versioninfo still records it as applied.
    }

    public override void Down()
    {
        if (Schema.Table("audit_log").Exists())
        {
            Delete.Table("audit_log");
        }
    }
}