using FluentMigrator;

namespace ERPSystem.Shared.Migrations;

[Migration(20260615_020000)]
public class AddMultiCompanySupport : Migration
{
    public override void Up()
    {
        // DEC-082: NoOp — schema now defined in JSON: companies + cost_centers (multi-company cols on accounts/journal_entries/journal_lines)
        // The DataTypeMigrator (DEC-079) handles all additive schema changes.
        // This migration is kept so FluentMigrator versioninfo still records it as applied.
    }
    public override void Down()
    {
        Delete.ForeignKey("fk_jl_cost_center").OnTable("journal_lines");
        Delete.ForeignKey("fk_jl_company").OnTable("journal_lines");
        Delete.Column("cost_center_id").FromTable("journal_lines");
        Delete.Column("company_id").FromTable("journal_lines");
        Delete.ForeignKey("fk_je_company").OnTable("journal_entries");
        Delete.Column("company_id").FromTable("journal_entries");
        Delete.ForeignKey("fk_accounts_company").OnTable("accounts");
        Delete.Column("is_intercompany").FromTable("accounts");
        Delete.Column("company_id").FromTable("accounts");
        Delete.Table("cost_centers");
        Delete.Table("companies");
    }
}
