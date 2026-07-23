using FluentMigrator;

namespace ERPSystem.Shared.Migrations;

/// <summary>
/// Migration 006 — Inventory Movements (CQRS) + Notifications
///
/// الجداول:
/// - stock_movements: Aggregate Root (Draft → Posted)
/// - stock_levels: Read Model (denormalized, optimistic version)
/// - stock_reservations: holds for projects/orders
/// - notifications: in-app alerts (LowStock, etc.)
/// </summary>
[Migration(20260615_090000)]
public class AddInventoryMovements : Migration
{
    public override void Up()
    {
        // DEC-082: NoOp — schema now defined in JSON: stock_movements (others: stock_levels/reservations/notifications — not in JSON yet)
        // The DataTypeMigrator (DEC-079) handles all additive schema changes.
        // This migration is kept so FluentMigrator versioninfo still records it as applied.
    }

    public override void Down()
    {
        Delete.Table("notifications");
        Delete.Table("stock_reservations");
        Delete.Table("stock_levels");
        Delete.Table("stock_movements");
    }
}
