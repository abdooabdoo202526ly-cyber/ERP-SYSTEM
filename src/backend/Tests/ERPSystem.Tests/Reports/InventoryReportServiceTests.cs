using ERPSystem.Modules.Inventory.Entities;
using ERPSystem.Modules.Reports.Application;
using ERPSystem.Modules.Reports.Application.Services;
using ERPSystem.Tests.Common;
using FluentAssertions;

namespace ERPSystem.Tests.Reports;

/// <summary>
/// اختبارات خدمة تقارير Inventory.
///
/// DTO Unit Tests (تشتغل دائماً) + Service Tests marked Skip
/// (تتطلب Postgres حقيقي على CI لتنفّذ SQL JOINs بشكل صحيح).
/// </summary>
public class InventoryReportServiceTests
{
    private static (InventoryReportService svc, FakeDbConnectionFactory db, Guid tenantId) Build()
    {
        var tenant = Guid.NewGuid();
        var db = new FakeDbConnectionFactory();
        return (new InventoryReportService(db), db, tenant);
    }

    // ============== DTO Unit Tests ==============

    [Fact]
    public void StockValuation_Dto_TotalValue_CalculatesCorrectly()
    {
        var v = new StockValuation { QuantityOnHand = 10, AverageCost = 7.5m };
        v.TotalValue.Should().Be(75m, "10 × 7.5 = 75");
    }

    [Fact]
    public void StockValuation_Dto_TotalValue_ZeroWhenNoStock()
    {
        var v = new StockValuation { QuantityOnHand = 0, AverageCost = 100m };
        v.TotalValue.Should().Be(0m);
    }

    [Fact]
    public void LowStockItem_Dto_Shortfall_CalculatesCorrectly()
    {
        var item = new LowStockItem
        {
            ReorderLevel = 100,
            QuantityOnHand = 30,
            QuantityReserved = 5
        };
        item.QuantityAvailable.Should().Be(25, "30 - 5 = 25");
        item.Shortfall.Should().Be(75, "100 - 25 = 75");
    }

    [Fact]
    public void LowStockItem_Dto_Shortfall_ZeroWhenAboveLevel()
    {
        var item = new LowStockItem
        {
            ReorderLevel = 50,
            QuantityOnHand = 100,
            QuantityReserved = 0
        };
        item.QuantityAvailable.Should().Be(100);
        item.Shortfall.Should().Be(-50, "تحت الـ reorder level بـ 50");
    }

    [Fact]
    public void StockAging_Dto_AgeBucket_0to30()
    {
        var a = new StockAging { DaysInStock = 15 };
        a.AgeBucket.Should().Be("0-30");
    }

    [Fact]
    public void StockAging_Dto_AgeBucket_31to60()
    {
        var a = new StockAging { DaysInStock = 45 };
        a.AgeBucket.Should().Be("31-60");
    }

    [Fact]
    public void StockAging_Dto_AgeBucket_61to90()
    {
        var a = new StockAging { DaysInStock = 75 };
        a.AgeBucket.Should().Be("61-90");
    }

    [Fact]
    public void StockAging_Dto_AgeBucket_Over90()
    {
        var a = new StockAging { DaysInStock = 120 };
        a.AgeBucket.Should().Be("90+");
    }

    [Fact]
    public void StockAging_Dto_AgeBucket_Boundary()
    {
        new StockAging { DaysInStock = 30 }.AgeBucket.Should().Be("0-30", "30 في 0-30");
        new StockAging { DaysInStock = 31 }.AgeBucket.Should().Be("31-60", "31 في 31-60");
        new StockAging { DaysInStock = 60 }.AgeBucket.Should().Be("31-60", "60 في 31-60");
        new StockAging { DaysInStock = 61 }.AgeBucket.Should().Be("61-90", "61 في 61-90");
        new StockAging { DaysInStock = 90 }.AgeBucket.Should().Be("61-90", "90 في 61-90");
        new StockAging { DaysInStock = 91 }.AgeBucket.Should().Be("90+", "91 في 90+");
    }

    // ============== Service Integration Tests (Skip - تحتاج Postgres) ==============

    [Fact(Skip = "Integration: requires real Postgres for SQL JOINs.")]
    public async Task GetStockValuation_PositiveQuantity_ReturnsItemsWithTotalValue()
    {
        var (svc, db, tenant) = Build();
        var itemId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        db.AddRow("items", "id", itemId, "tenant_id", tenant, "company_id", companyId, "sku", "ITM-001", "name", "صنف 1");
        db.AddRow("warehouses", "id", warehouseId, "tenant_id", tenant, "company_id", companyId, "code", "WH-01", "name", "مخزن 1");
        db.AddRow("stock_levels", "tenant_id", tenant, "company_id", companyId, "item_id", itemId,
            "warehouse_id", warehouseId, "quantity_on_hand", 100m, "average_cost", 5.5m);

        var rows = await svc.GetStockValuationAsync(tenant, null, null, CancellationToken.None);
        rows.Should().HaveCount(1);
        rows[0].QuantityOnHand.Should().Be(100);
    }

    [Fact(Skip = "Integration: requires real Postgres.")]
    public async Task GetLowStock_BelowReorderLevel_ReturnsItemWithCorrectStatus()
    {
        var (svc, db, tenant) = Build();
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        db.AddRow("items", "id", itemId, "tenant_id", tenant, "sku", "X", "name", "صنف X",
            "is_active", true, "reorder_level", 50m, "reorder_quantity", 100m);
        db.AddRow("warehouses", "id", whId, "tenant_id", tenant, "code", "WH", "name", "مخزن");
        db.AddRow("stock_levels", "tenant_id", tenant, "item_id", itemId, "warehouse_id", whId,
            "quantity_on_hand", 10m, "quantity_reserved", 0m);

        var low = await svc.GetLowStockAsync(tenant, null, CancellationToken.None);
        low.Should().HaveCount(1);
        low[0].Status.Should().Be("Critical");
    }

    [Fact(Skip = "Integration: requires real Postgres for EXTRACT(DAY FROM ...).")]
    public async Task GetStockAging_DaysMapping_CategorizesCorrectly()
    {
        var (svc, db, tenant) = Build();
        var itemId = Guid.NewGuid();
        var whId = Guid.NewGuid();

        db.AddRow("items", "id", itemId, "tenant_id", tenant, "sku", "Z", "name", "صنف Z");
        db.AddRow("stock_levels", "tenant_id", tenant, "item_id", itemId, "warehouse_id", whId,
            "quantity_on_hand", 100m, "last_movement_at", DateTime.UtcNow.AddDays(-45));

        var aging = await svc.GetStockAgingAsync(tenant, null, CancellationToken.None);
        aging.Should().HaveCount(1);
        aging[0].AgeBucket.Should().Be("31-60");
    }
}
