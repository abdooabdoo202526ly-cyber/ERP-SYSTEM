using FluentMigrator;

namespace ERPSystem.Shared.Migrations;

/// <summary>
/// Migration 009 — HR Core (Phase 3.5)
///
/// الجداول:
/// - hr.departments
/// - hr.employees
/// - hr.attendance
/// - hr.leave_requests
///
/// Business Rules:
/// - Department: Code فريد داخل الـ tenant
/// - Employee: Email فريد داخل الـ tenant
/// - Attendance: CheckIn/CheckOut متتابع (لا تكرار)
/// - LeaveRequest: EndDate >= StartDate + لا يتعارض مع إجازة أخرى للموظف نفسه
/// </summary>
[Migration(20260623_130000)]
public class CreateHRTables : Migration
{
    public override void Up()
    {
        // DEC-082: NoOp — schema now defined in JSON: departments, employees, attendance, leave_requests
        // The DataTypeMigrator (DEC-079) handles all additive schema changes.
        // This migration is kept so FluentMigrator versioninfo still records it as applied.
    }

    public override void Down()
    {
        Delete.Table("leave_requests");
        Delete.Table("attendance");
        Delete.Table("employees");
        Delete.Table("departments");
    }
}
