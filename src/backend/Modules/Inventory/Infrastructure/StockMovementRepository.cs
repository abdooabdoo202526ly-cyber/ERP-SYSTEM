using Dapper;
using ERPSystem.Modules.Inventory.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Inventory.Infrastructure;

public sealed class StockMovementRepository : IStockMovementRepository
{
    private readonly IDbConnectionFactory _db;
    public StockMovementRepository(IDbConnectionFactory db) => _db = db;
    private const string Sel = @"id, tenant_id AS TenantId, company_id AS CompanyId, reference, type, movement_date AS MovementDate,
        item_id AS ItemId, warehouse_id AS WarehouseId, quantity, unit_cost AS UnitCost,
        project_id AS ProjectId, cost_center_id AS CostCenterId, destination_warehouse_id AS DestinationWarehouseId,
        source_type AS SourceType, source_id AS SourceId, notes, status,
        created_at AS CreatedAt, created_by AS CreatedBy, posted_at AS PostedAt, reversed_by_movement_id AS ReversedByMovementId";

    public async Task<StockMovement?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<StockMovement>(new CommandDefinition(
            $"SELECT {Sel} FROM stock_movements WHERE id = @Id LIMIT 1", new { Id = id }, cancellationToken: ct));
    }

    public async Task<StockMovement?> GetByReferenceAsync(Guid tenantId, string reference, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<StockMovement>(new CommandDefinition(
            $"SELECT {Sel} FROM stock_movements WHERE tenant_id = @TenantId AND reference = @Reference LIMIT 1",
            new { TenantId = tenantId, Reference = reference }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<StockMovement>> ListAsync(Guid tenantId, Guid? companyId, StockMovementType? type, StockMovementStatus? status, int skip, int take, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {Sel} FROM stock_movements WHERE tenant_id = @TenantId";
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (companyId.HasValue) { sql += " AND company_id = @CompanyId"; p.Add("CompanyId", companyId.Value); }
        if (type.HasValue) { sql += " AND type = @Type"; p.Add("Type", (int)type.Value); }
        if (status.HasValue) { sql += " AND status = @Status"; p.Add("Status", (int)status.Value); }
        sql += " ORDER BY movement_date DESC, reference DESC OFFSET @Skip LIMIT @Take";
        p.Add("Skip", skip); p.Add("Take", take);
        var rows = await conn.QueryAsync<StockMovement>(new CommandDefinition(sql, p, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertAsync(StockMovement movement, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO stock_movements (id, tenant_id, company_id, reference, type, movement_date,
                                          item_id, warehouse_id, quantity, unit_cost, project_id, cost_center_id,
                                          destination_warehouse_id, source_type, source_id, notes, status,
                                          created_at, created_by, posted_at, reversed_by_movement_id)
            VALUES (@Id, @TenantId, @CompanyId, @Reference, @Type, @MovementDate,
                    @ItemId, @WarehouseId, @Quantity, @UnitCost, @ProjectId, @CostCenterId,
                    @DestinationWarehouseId, @SourceType, @SourceId, @Notes, @Status,
                    @CreatedAt, @CreatedBy, @PostedAt, @ReversedByMovementId)", movement, cancellationToken: ct));
    }

    public async Task UpdateAsync(StockMovement movement, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE stock_movements SET status = @Status, posted_at = @PostedAt,
                                       reversed_by_movement_id = @ReversedByMovementId
            WHERE id = @Id", movement, cancellationToken: ct));
    }
}
