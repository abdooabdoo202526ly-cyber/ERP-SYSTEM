using FluentMigrator;

namespace ERPSystem.Shared.Migrations;

/// <summary>
/// Migration 002 — إنشاء جداول Finance Core.
///
/// الجداول:
/// - accounts:        Chart of Accounts (with hierarchy via parent_account_id)
/// - journal_entries: رأس القيد (header)
/// - journal_lines:   سطور القيد (debit/credit منفصلين)
/// - posting_rules:   Rules Engine templates
/// </summary>
[Migration(20260614_180000)]
public class CreateFinanceTables : Migration
{
    public override void Up()
    {
        // DEC-082: NoOp — schema now defined in JSON: accounts/journal_entries/journal_lines
        // The DataTypeMigrator (DEC-079) handles all additive schema changes.
        // This migration is kept so FluentMigrator versioninfo still records it as applied.
    }

    public override void Down()
    {
        Delete.Table("posting_rules");
        Delete.Table("journal_lines");
        Delete.Table("journal_entries");
        Delete.Table("accounts");
    }
}

/// <summary>enum محلي لتفادي cross-module referencing</summary>
internal enum JournalEntryStatus
{
    Draft = 1,
    Posted = 2,
    Reversed = 3,
}
