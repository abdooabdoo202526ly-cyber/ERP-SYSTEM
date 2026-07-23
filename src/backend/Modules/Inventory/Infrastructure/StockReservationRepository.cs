using Dapper;
using ERPSystem.Modules.Inventory.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Inventory.Infrastructure;

public sealed class StockReservationRepository : IStockReservationRepository
{
    private readonly IDbConnectionFactory _db;
    public StockReservationRepository(IDbConnectionFactory db) => _db = db;
    private const string Sel = @"id, tenant_id AS TenantId, item_id AS ItemId, warehouse_id AS WarehouseId,
        quantity, reference_type AS ReferenceType, reference_id AS ReferenceId,
        expires_at AS ExpiresAt, created_at AS CreatedAt, created_by AS CreatedBy";

    public async Task<StockReservation?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<StockReservation>(new CommandDefinition(
            $"SELECT {Sel} FROM stock_reservations WHERE id = @Id LIMIT 1", new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<StockReservation>> ListAsync(Guid tenantId, Guid? itemId, Guid? warehouseId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {Sel} FROM stock_reservations WHERE tenant_id = @TenantId";
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (itemId.HasValue) { sql += " AND item_id = @ItemId"; p.Add("ItemId", itemId.Value); }
        if (warehouseId.HasValue) { sql += " AND warehouse_id = @WarehouseId"; p.Add("WarehouseId", warehouseId.Value); }
        sql += " ORDER BY expires_at";
        var rows = await conn.QueryAsync<StockReservation>(new CommandDefinition(sql, p, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<StockReservation>> GetByReferenceAsync(Guid tenantId, string referenceType, Guid referenceId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var rows = await conn.QueryAsync<StockReservation>(new CommandDefinition(
            $"SELECT {Sel} FROM stock_reservations WHERE tenant_id = @TenantId AND reference_type = @ReferenceType AND reference_id = @ReferenceId",
            new { TenantId = tenantId, ReferenceType = referenceType, ReferenceId = referenceId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertAsync(StockReservation r, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO stock_reservations (id, tenant_id, item_id, warehouse_id, quantity,
                                            reference_type, reference_id, expires_at, created_at, created_by)
            VALUES (@Id, @TenantId, @ItemId, @WarehouseId, @Quantity,
                    @ReferenceType, @ReferenceId, @ExpiresAt, @CreatedAt, @CreatedBy)", r, cancellationToken: ct));
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM stock_reservations WHERE id = @Id", new { Id = id }, cancellationToken: ct));
    }
}
