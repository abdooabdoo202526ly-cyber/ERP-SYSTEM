using FluentMigrator;

namespace ERPSystem.Shared.Migrations;

/// <summary>
/// Migration 001 — إنشاء جداول Identity الأساسية
///
/// الجداول:
/// - tenants       : المستأجرون
/// - users         : المستخدمون
/// - roles         : الأدوار
/// - user_roles    : ربط المستخدمين بالأدوار
/// - refresh_tokens: توكنات التجديد (دورة حياة كاملة)
/// </summary>
[Migration(20260614_120000)]
public class CreateIdentityTables : Migration
{
    public override void Up()
    {
        // DEC-082: NoOp — schema now defined in JSON: tenants/users/roles/user_roles/refresh_tokens
        // The DataTypeMigrator (DEC-079) handles all additive schema changes.
        // This migration is kept so FluentMigrator versioninfo still records it as applied.
    }

    public override void Down()
    {
        Delete.Table("refresh_tokens");
        Delete.Table("user_roles");
        Delete.Table("users");
        Delete.Table("roles");
        Delete.Table("tenants");
    }
}
