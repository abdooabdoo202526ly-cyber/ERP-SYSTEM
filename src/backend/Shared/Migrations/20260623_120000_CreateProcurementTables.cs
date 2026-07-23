using FluentMigrator;

namespace ERPSystem.Shared.Migrations;

/// <summary>
/// Migration 008 — Procurement Core (Phase 3)
///
/// الجداول:
/// - vendors                                  (المورّدون)
/// - purchase_orders + purchase_order_lines   (أوامر الشراء)
/// - goods_receipts + goods_receipt_lines     (سندات الاستلام)
/// - vendor_bills + vendor_bill_lines         (فواتير المورّدين)
///
/// Business Rules:
/// - GR يُنشأ فقط لـ PO في حالة Approved أو Sent
/// - Bill يُنشأ فقط لـ GR في حالة Received
/// - عند Post Bill → JournalEntry (Dr Inventory / Cr A/P) — يدوياً في الـ service
/// </summary>
[Migration(20260623_120000)]
public class CreateProcurementTables : Migration
{
    public override void Up()
    {
        // DEC-080: NoOp — schema is now defined in
        //   src/backend/Host/data-types/vendors.json
        // The DataTypeMigrator (DEC-079) handles all additive schema changes.
        // This migration is kept so FluentMigrator versioninfo still records it as applied.
    }

    public override void Down()
    {
        Delete.Table("vendor_bill_lines");
        Delete.Table("vendor_bills");
        Delete.Table("goods_receipt_lines");
        Delete.Table("goods_receipts");
        Delete.Table("purchase_order_lines");
        Delete.Table("purchase_orders");
        Delete.Table("vendors");
    }
}
