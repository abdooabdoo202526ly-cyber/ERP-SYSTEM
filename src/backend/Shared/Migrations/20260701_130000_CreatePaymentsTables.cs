using FluentMigrator;

namespace ERPSystem.Shared.Migrations;

/// <summary>
/// Migration 011 — إنشاء جداول Payments (Phase 5.A).
///
/// - payments: رأس سند الدفع/القبض
///   - party_type: "Customer" | "Vendor" (مفتوح — يدعم كلا الـ streams)
///   - party_id: Guid → customers (مستقبلي) أو vendors (Procurement)
///   - status: Draft | Posted | Cancelled
///   - journal_entry_id: ربط بالقيد المُنشأ عند الترحيل
///
/// - payment_allocations: تخصيصات الـ Payment على فواتير
///   - ref_type: "SalesInvoice" | "VendorBill"
///   - sum(amount_applied) ≤ payments.amount (الباقي = On Account)
/// </summary>
[Migration(20260701_130000)]
public class CreatePaymentsTables : Migration
{
    public override void Up()
    {
        // DEC-082: NoOp — schema now defined in JSON: payments, payment_allocations
        // The DataTypeMigrator (DEC-079) handles all additive schema changes.
        // This migration is kept so FluentMigrator versioninfo still records it as applied.
    }

    public override void Down()
    {
        Delete.ForeignKey("fk_payments_je").OnTable("payments");
        Delete.ForeignKey("fk_pa_payment").OnTable("payment_allocations");
        Delete.Table("payment_allocations");
        Delete.Table("payments");
    }
}
