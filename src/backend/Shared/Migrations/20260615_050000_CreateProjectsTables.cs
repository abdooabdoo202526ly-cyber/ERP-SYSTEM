using FluentMigrator;

namespace ERPSystem.Shared.Migrations;

/// <summary>
/// Migration 004 — Projects Module
///
/// الجداول:
/// - projects
/// - project_tasks
/// - resources
/// - project_budgets
/// - resource_assignments
/// </summary>
[Migration(20260615_050000)]
public class CreateProjectsTables : Migration
{
    public override void Up()
    {
        // DEC-080: NoOp — schema is now defined in
        //   src/backend/Host/data-types/projects.json
        // The DataTypeMigrator (DEC-079) handles all additive schema changes.
        // This migration is kept so FluentMigrator versioninfo still records it as applied.
    }

    public override void Down()
    {
        Delete.Table("resource_assignments");
        Delete.Table("project_budgets");
        Delete.Table("resources");
        Delete.Table("project_tasks");
        Delete.Table("projects");
    }
}

// local enums to avoid cross-module referencing in migration
internal enum ProjectStatusLocal { Planning = 1, Active = 2, OnHold = 3, Completed = 4, Cancelled = 5 }
internal enum TaskStatusLocal { NotStarted = 1, InProgress = 2, Blocked = 3, Completed = 4, Cancelled = 5 }
internal enum ResourceTypeLocal { Labor = 1, Equipment = 2, Material = 3, Service = 4 }
