using FluentMigrator;

namespace ERPSystem.Shared.Migrations;

/// <summary>
/// Migration 016 — soft delete columns (Sprint-4.5 T-011 / DEC-059).
///
/// Adds `deleted_at` (nullable timestamp) to finance + project tables.
/// History is preserved by marking rows as deleted instead of hard-deleting.
/// </summary>
[Migration(20260706_130000)]
public class AddSoftDelete : Migration
{
    // الجداول الأساسية (T-011 scope)
    private static readonly string[] PrimaryTables = new[]
    {
        "sales_invoices",
        "projects"
    };

    // جداول ثانوية (future PR — نفس النمط)
    private static readonly string[] SecondaryTables = new[]
    {
        "customers",
        "vendors",
        "employees"
    };

    public override void Up()
    {
        // DEC-082: NoOp — schema now defined in JSON: deleted_at columns + ix_*_deleted_at indexes
        // The DataTypeMigrator (DEC-079) handles all additive schema changes.
        // This migration is kept so FluentMigrator versioninfo still records it as applied.
    }

    public override void Down()
    {
        foreach (var table in PrimaryTables.Reverse())
        {
            if (Schema.Table(table).Exists() && Schema.Table(table).Column("deleted_at").Exists())
            {
                Delete.Index($"ix_{table}_deleted_at");
                Delete.Column("deleted_at").FromTable(table);
            }
        }
    }
}