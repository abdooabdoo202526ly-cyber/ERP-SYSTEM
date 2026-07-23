using FluentMigrator;

namespace ERPSystem.Shared.Migrations;

/// <summary>
/// Migration 010 — Payroll Core (Phase 4)
///
/// الجداول:
/// - payroll.salary_structures            (هيكل الراتب — تعريف عام، مع currency)
/// - payroll.salary_structure_lines       (مكوّنات الهيكل: earnings / deductions)
/// - payroll.payroll_runs                 (الـ Aggregate Root: Draft → Processing → Posted)
/// - payroll.payroll_items                (قسيمة راتب لكل موظف داخل run)
/// - payroll.payslip_components           (تفاصيل مكوّنات القسيمة: earnings / deductions)
///
/// Business Rules:
/// - SalaryStructure.code فريد داخل الـ tenant
/// - PayrollRun.State machine: Draft → Processing → Posted (لا رجوع)
/// - عند Post: total_gross / total_net تُحدّث، posted_at يُسجّل
/// - PayrollItem.payment_days: عدد أيام العمل الفعلية (default 30)
/// - عند حذف موظف/هيكل: ON DELETE RESTRICT (لا نحذف تاريخ payroll — لـ SOX)
/// </summary>
[Migration(20260624_100000)]
public class CreatePayrollTables : Migration
{
    public override void Up()
    {
        // DEC-082: NoOp — schema now defined in JSON: salary_structures, salary_structure_lines, payroll_runs, payroll_items, payslip_components
        // The DataTypeMigrator (DEC-079) handles all additive schema changes.
        // This migration is kept so FluentMigrator versioninfo still records it as applied.
    }

    public override void Down()
    {
        Delete.Table("payslip_components");
        Delete.Table("payroll_items");
        Delete.Table("payroll_runs");
        Delete.Table("salary_structure_lines");
        Delete.Table("salary_structures");
    }
}