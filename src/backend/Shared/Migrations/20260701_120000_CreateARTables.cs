using FluentMigrator;

namespace ERPSystem.Shared.Migrations;

/// <summary>
/// Migration 010 — Accounts Receivable (Phase 5 Sprint 1)
///
/// الجداول:
/// - customers                              (العملاء — ماستر)
/// - sales_invoices + sales_invoice_lines   (فواتير المبيعات)
/// - receipts + receipt_allocations         (سندات القبض + تخصيصاتها)
///
/// Business Rules:
/// - SalesInvoice.Post (Draft → Sent) → JournalEntry (Dr 1230 AR / Cr 5110 Revenue)
/// - Receipt.Post → JournalEntry (Dr 1210 Cash / Cr 1230 AR) + تحديث الفواتير
/// - OnDelete: SetNull للـ customer_id في sales_invoices (لا نمسح الفواتير مع العميل)؛
///   Restrict للـ customer_id في receipts (لا نمسح العميل إذا عليه سندات)؛
///   Cascade للـ lines و allocations.
/// </summary>
[Migration(20260701_120000)]
public class CreateARTables : Migration
{
    public override void Up()
    {
        // DEC-080: NoOp — schema is now defined in
        //   src/backend/Host/data-types/customers.json
        // The DataTypeMigrator (DEC-079) handles all additive schema changes.
        // This migration is kept so FluentMigrator versioninfo still records it as applied.
    }

    public override void Down()
    {
        Delete.Table("receipt_allocations");
        Delete.Table("receipts");
        Delete.Table("sales_invoice_lines");
        Delete.Table("sales_invoices");
        Delete.Table("customers");
    }
}
