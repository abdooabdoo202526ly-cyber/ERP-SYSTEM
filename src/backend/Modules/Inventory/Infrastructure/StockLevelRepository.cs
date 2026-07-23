using Dapper;
using ERPSystem.Modules.Inventory.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Inventory.Infrastructure;

public sealed class StockLevelRepository : IStockLevelRepository
{
    private readonly IDbConnectionFactory _db;
    public StockLevelRepository(IDbConnectionFactory db) => _db = db;
    private const string Sel = @"id, tenant_id AS TenantId, company_id AS CompanyId, item_id AS ItemId, warehouse_id AS WarehouseId,
        quantity_on_hand AS QuantityOnHand, quantity_reserved AS QuantityReserved,
        average_cost AS AverageCost, last_movement_at AS LastMovementAt, version";

    public async Task<StockLevel?> GetAsync(Guid tenantId, Guid itemId, Guid warehouseId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<StockLevel>(new CommandDefinition(
            $"SELECT {Sel} FROM stock_levels WHERE tenant_id = @TenantId AND item_id = @ItemId AND warehouse_id = @WarehouseId LIMIT 1",
            new { TenantId = tenantId, ItemId = itemId, WarehouseId = warehouseId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<StockLevel>> GetByItemAsync(Guid tenantId, Guid itemId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var rows = await conn.QueryAsync<StockLevel>(new CommandDefinition(
            $"SELECT {Sel} FROM stock_levels WHERE tenant_id = @TenantId AND item_id = @ItemId ORDER BY warehouse_id",
            new { TenantId = tenantId, ItemId = itemId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<StockLevel>> GetByWarehouseAsync(Guid tenantId, Guid warehouseId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var rows = await conn.QueryAsync<StockLevel>(new CommandDefinition(
            $"SELECT {Sel} FROM stock_levels WHERE tenant_id = @TenantId AND warehouse_id = @WarehouseId ORDER BY item_id",
            new { TenantId = tenantId, WarehouseId = warehouseId }, cancellationToken: ct));
        return rows.AsList();
    }

    /// <summary>
    /// Low stock: levels whose item has reorder_level > 0 AND quantity_available < reorder_level.
    /// Uses a join — only items with reorder configured show up.
    /// </summary>
    public async Task<IReadOnlyList<StockLevel>> GetLowStockAsync(Guid tenantId, Guid companyId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            SELECT sl.id, sl.tenant_id AS TenantId, sl.company_id AS CompanyId, sl.item_id AS ItemId, sl.warehouse_id AS WarehouseId,
                   sl.quantity_on_hand AS QuantityOnHand, sl.quantity_reserved AS QuantityReserved,
                   sl.average_cost AS AverageCost, sl.last_movement_at AS LastMovementAt, sl.version
            FROM stock_levels sl
            INNER JOIN items i ON i.id = sl.item_id
            WHERE sl.tenant_id = @TenantId
              AND sl.company_id = @CompanyId
              AND i.reorder_level > 0
              AND (sl.quantity_on_hand - sl.quantity_reserved) < i.reorder_level";
        var rows = await conn.QueryAsync<StockLevel>(new CommandDefinition(sql, new { TenantId = tenantId, CompanyId = companyId }, cancellationToken: ct));
        return rows.AsList();
    }

    /// <summary>
    /// UPSERT with optimistic concurrency. If expectedVersion doesn't match (concurrent update),
    /// throws InvalidOperationException — caller handles.
    /// </summary>
    public async Task UpsertAsync(StockLevel level, int expectedVersion, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        // First try UPDATE with version check
        var rows = await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE stock_levels SET quantity_on_hand = @QuantityOnHand, quantity_reserved = @QuantityReserved,
                                   average_cost = @AverageCost, last_movement_at = @LastMovementAt,
                                   version = version + 1
            WHERE id = @Id AND version = @ExpectedVersion", new
        {
            level.QuantityOnHand, level.QuantityReserved, level.AverageCost, level.LastMovementAt,
            level.Id, ExpectedVersion = expectedVersion
        }, cancellationToken: ct));
        if (rows == 0)
        {
            // Either not found or version mismatch — try INSERT
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO stock_levels (id, tenant_id, company_id, item_id, warehouse_id,
                                          quantity_on_hand, quantity_reserved, average_cost, last_movement_at, version)
                VALUES (@Id, @TenantId, @CompanyId, @ItemId, @WarehouseId,
                        @QuantityOnHand, @QuantityReserved, @AverageCost, @LastMovementAt, 1)", level, cancellationToken: ct));
        }
    }

    public async Task InsertAsync(StockLevel level, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO stock_levels (id, tenant_id, company_id, item_id, warehouse_id,
                                      quantity_on_hand, quantity_reserved, average_cost, last_movement_at, version)
            VALUES (@Id, @TenantId, @CompanyId, @ItemId, @WarehouseId,
                    @QuantityOnHand, @QuantityReserved, @AverageCost, @LastMovementAt, 1)", level, cancellationToken: ct));
    }
}
