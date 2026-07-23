using Dapper;
using ERPSystem.Modules.Procurement.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Procurement.Infrastructure;

/// <summary>تنفيذ IGoodsReceiptRepository عبر Dapper.</summary>
public sealed class GoodsReceiptRepository : IGoodsReceiptRepository
{
    private readonly IDbConnectionFactory _db;
    public GoodsReceiptRepository(IDbConnectionFactory db) => _db = db;

    private const string SelGr = @"id, tenant_id AS TenantId, gr_number AS GrNumber, purchase_order_id AS PurchaseOrderId,
        status, received_date AS ReceivedDate, warehouse_id AS WarehouseId, notes,
        created_at AS CreatedAt, created_by AS CreatedBy, updated_at AS UpdatedAt, updated_by AS UpdatedBy";

    private const string SelLine = @"id, tenant_id AS TenantId, goods_receipt_id AS GoodsReceiptId,
        item_id AS ItemId, quantity, unit_cost AS UnitCost, notes, line_order AS LineOrder";

    public async Task<GoodsReceipt?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var gr = await conn.QueryFirstOrDefaultAsync<GoodsReceipt>(new CommandDefinition(
            $"SELECT {SelGr} FROM goods_receipts WHERE id = @Id LIMIT 1",
            new { Id = id }, cancellationToken: ct));
        if (gr != null) gr.Lines = (await GetLinesAsync(gr.Id, ct)).ToList();
        return gr;
    }

    public async Task<GoodsReceipt?> GetByGrNumberAsync(Guid tenantId, string grNumber, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<GoodsReceipt>(new CommandDefinition(
            $"SELECT {SelGr} FROM goods_receipts WHERE tenant_id = @TenantId AND gr_number = @GrNumber LIMIT 1",
            new { TenantId = tenantId, GrNumber = grNumber }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<GoodsReceipt>> ListAsync(Guid tenantId, Guid? poId, GoodsReceiptStatus? status, int skip, int take, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {SelGr} FROM goods_receipts WHERE tenant_id = @TenantId";
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (poId.HasValue) { sql += " AND purchase_order_id = @PoId"; p.Add("PoId", poId.Value); }
        if (status.HasValue) { sql += " AND status = @Status"; p.Add("Status", status.Value.ToString()); }
        sql += " ORDER BY created_at DESC OFFSET @Skip LIMIT @Take";
        p.Add("Skip", skip); p.Add("Take", take);
        var rows = await conn.QueryAsync<GoodsReceipt>(new CommandDefinition(sql, p, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertAsync(GoodsReceipt gr, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO goods_receipts (id, tenant_id, gr_number, purchase_order_id, status, received_date,
                                        warehouse_id, notes, created_at, created_by, updated_at, updated_by)
            VALUES (@Id, @TenantId, @GrNumber, @PurchaseOrderId, @Status, @ReceivedDate,
                    @WarehouseId, @Notes, @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy)", new
        {
            gr.Id, gr.TenantId, gr.GrNumber, gr.PurchaseOrderId,
            Status = gr.Status.ToString(),
            gr.ReceivedDate, gr.WarehouseId, gr.Notes,
            gr.CreatedAt, gr.CreatedBy, gr.UpdatedAt, gr.UpdatedBy
        }, cancellationToken: ct));
    }

    public async Task UpdateAsync(GoodsReceipt gr, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE goods_receipts SET status = @Status, received_date = @ReceivedDate,
                                      warehouse_id = @WarehouseId, notes = @Notes,
                                      updated_at = @UpdatedAt, updated_by = @UpdatedBy
            WHERE id = @Id", new
        {
            gr.Id, Status = gr.Status.ToString(), gr.ReceivedDate, gr.WarehouseId, gr.Notes,
            gr.UpdatedAt, gr.UpdatedBy
        }, cancellationToken: ct));
    }

    public async Task InsertLinesAsync(Guid tenantId, Guid grId, IEnumerable<GoodsReceiptLine> lines, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        foreach (var l in lines)
        {
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO goods_receipt_lines (id, tenant_id, goods_receipt_id, item_id, quantity, unit_cost, notes, line_order)
                VALUES (@Id, @TenantId, @GoodsReceiptId, @ItemId, @Quantity, @UnitCost, @Notes, @LineOrder)",
                new
                {
                    l.Id, TenantId = tenantId, GoodsReceiptId = grId,
                    l.ItemId, l.Quantity, l.UnitCost, l.Notes, l.LineOrder
                }, cancellationToken: ct));
        }
    }

    public async Task<IReadOnlyList<GoodsReceiptLine>> GetLinesAsync(Guid grId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var rows = await conn.QueryAsync<GoodsReceiptLine>(new CommandDefinition(
            $"SELECT {SelLine} FROM goods_receipt_lines WHERE goods_receipt_id = @GrId ORDER BY line_order",
            new { GrId = grId }, cancellationToken: ct));
        return rows.AsList();
    }
}
