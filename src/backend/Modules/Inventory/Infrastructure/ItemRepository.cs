using Dapper;
using ERPSystem.Modules.Inventory.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Inventory.Infrastructure;

public sealed class ItemRepository : IItemRepository
{
    private readonly IDbConnectionFactory _db;
    public ItemRepository(IDbConnectionFactory db) => _db = db;
    private const string Sel = @"id, tenant_id AS TenantId, company_id AS CompanyId, sku, barcode, name, description,
        category_id AS CategoryId, unit_of_measure_id AS UnitOfMeasureId, item_type AS ItemType,
        costing_method AS CostingMethod, average_cost AS AverageCost, standard_cost AS StandardCost,
        inventory_account_id AS InventoryAccountId, cogs_account_id AS CogsAccountId,
        sales_account_id AS SalesAccountId, reorder_level AS ReorderLevel, reorder_quantity AS ReorderQuantity,
        is_active AS IsActive, created_at AS CreatedAt, created_by AS CreatedBy,
        updated_at AS UpdatedAt, updated_by AS UpdatedBy";

    public async Task<Item?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Item>(new CommandDefinition(
            $"SELECT {Sel} FROM items WHERE id = @Id LIMIT 1", new { Id = id }, cancellationToken: ct));
    }
    public async Task<Item?> GetBySkuAsync(Guid tenantId, string sku, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Item>(new CommandDefinition(
            $"SELECT {Sel} FROM items WHERE tenant_id = @TenantId AND LOWER(sku) = LOWER(@Sku) LIMIT 1",
            new { TenantId = tenantId, Sku = sku }, cancellationToken: ct));
    }
    public async Task<Item?> GetByBarcodeAsync(Guid tenantId, string barcode, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Item>(new CommandDefinition(
            $"SELECT {Sel} FROM items WHERE tenant_id = @TenantId AND barcode = @Barcode LIMIT 1",
            new { TenantId = tenantId, Barcode = barcode }, cancellationToken: ct));
    }
    public async Task<IReadOnlyList<Item>> ListAsync(Guid tenantId, Guid? companyId, Guid? categoryId, bool includeInactive, int skip, int take, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {Sel} FROM items WHERE tenant_id = @TenantId";
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (companyId.HasValue) { sql += " AND company_id = @CompanyId"; p.Add("CompanyId", companyId.Value); }
        if (categoryId.HasValue) { sql += " AND category_id = @CategoryId"; p.Add("CategoryId", categoryId.Value); }
        if (!includeInactive) sql += " AND is_active = true";
        sql += " ORDER BY sku OFFSET @Skip LIMIT @Take";
        p.Add("Skip", skip); p.Add("Take", take);
        var rows = await conn.QueryAsync<Item>(new CommandDefinition(sql, p, cancellationToken: ct));
        return rows.AsList();
    }
    public async Task InsertAsync(Item item, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO items (id, tenant_id, company_id, sku, barcode, name, description, category_id, unit_of_measure_id,
                              item_type, costing_method, average_cost, standard_cost,
                              inventory_account_id, cogs_account_id, sales_account_id,
                              reorder_level, reorder_quantity, is_active, created_at, created_by, updated_at, updated_by)
            VALUES (@Id, @TenantId, @CompanyId, @Sku, @Barcode, @Name, @Description, @CategoryId, @UnitOfMeasureId,
                    @ItemType, @CostingMethod, @AverageCost, @StandardCost,
                    @InventoryAccountId, @CogsAccountId, @SalesAccountId,
                    @ReorderLevel, @ReorderQuantity, @IsActive, @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy)", item, cancellationToken: ct));
    }
    public async Task UpdateAsync(Item item, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE items SET barcode = @Barcode, name = @Name, description = @Description, category_id = @CategoryId,
                            unit_of_measure_id = @UnitOfMeasureId, costing_method = @CostingMethod,
                            standard_cost = @StandardCost, inventory_account_id = @InventoryAccountId,
                            cogs_account_id = @CogsAccountId, sales_account_id = @SalesAccountId,
                            reorder_level = @ReorderLevel, reorder_quantity = @ReorderQuantity,
                            is_active = @IsActive, updated_at = @UpdatedAt, updated_by = @UpdatedBy
            WHERE id = @Id", item, cancellationToken: ct));
    }
}
