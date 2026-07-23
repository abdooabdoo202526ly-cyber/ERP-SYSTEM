using FluentMigrator;

namespace ERPSystem.Shared.Migrations;

/// <summary>
/// Migration 005 — Inventory Core
///
/// الجداول:
/// - items
/// - warehouses
/// - units_of_measure
/// - item_categories
/// </summary>
[Migration(20260615_070000)]
public class AddInventoryCore : Migration
{
    public override void Up()
    {
        // DEC-082: NoOp — schema now defined in JSON: items, warehouses, item_categories, units_of_measure
        // The DataTypeMigrator (DEC-079) handles all additive schema changes.
        // This migration is kept so FluentMigrator versioninfo still records it as applied.
    }

    public override void Down()
    {
        Delete.Table("warehouses");
        Delete.Table("items");
        Delete.Table("item_categories");
        Delete.Table("units_of_measure");
    }
}

internal enum ItemTypeLocal { RawMaterial = 1, FinishedGood = 2, Consumable = 3, Service = 4 }
internal enum CostingMethodLocal { FIFO = 1, LIFO = 2, Average = 3, Standard = 4 }
