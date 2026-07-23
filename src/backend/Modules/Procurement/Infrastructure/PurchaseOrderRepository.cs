using Dapper;
using ERPSystem.Modules.Procurement.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Procurement.Infrastructure;

/// <summary>تنفيذ IPurchaseOrderRepository عبر Dapper.</summary>
public sealed class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly IDbConnectionFactory _db;
    public PurchaseOrderRepository(IDbConnectionFactory db) => _db = db;

    private const string SelPo = @"id, tenant_id AS TenantId, po_number AS PoNumber, vendor_id AS VendorId,
        project_id AS ProjectId, cost_center_id AS CostCenterId,
        status, order_date AS OrderDate, expected_date AS ExpectedDate, currency,
        sub_total AS SubTotal, tax_amount AS TaxAmount, total_amount AS TotalAmount, notes,
        approved_at AS ApprovedAt, approved_by AS ApprovedBy, sent_at AS SentAt,
        created_at AS CreatedAt, created_by AS CreatedBy, updated_at AS UpdatedAt, updated_by AS UpdatedBy";

    private const string SelLine = @"id, tenant_id AS TenantId, purchase_order_id AS PurchaseOrderId,
        item_id AS ItemId, quantity, unit_price AS UnitPrice, tax_rate AS TaxRate, sub_total AS SubTotal, line_order AS LineOrder";

    public async Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var po = await conn.QueryFirstOrDefaultAsync<PurchaseOrder>(new CommandDefinition(
            $"SELECT {SelPo} FROM purchase_orders WHERE id = @Id LIMIT 1",
            new { Id = id }, cancellationToken: ct));
        if (po != null)
        {
            po.Lines = (await GetLinesAsync(po.Id, ct)).ToList();
        }
        return po;
    }

    public async Task<PurchaseOrder?> GetByPoNumberAsync(Guid tenantId, string poNumber, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<PurchaseOrder>(new CommandDefinition(
            $"SELECT {SelPo} FROM purchase_orders WHERE tenant_id = @TenantId AND po_number = @PoNumber LIMIT 1",
            new { TenantId = tenantId, PoNumber = poNumber }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<PurchaseOrder>> ListAsync(Guid tenantId, Guid? vendorId, PurchaseOrderStatus? status, int skip, int take, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {SelPo} FROM purchase_orders WHERE tenant_id = @TenantId";
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (vendorId.HasValue) { sql += " AND vendor_id = @VendorId"; p.Add("VendorId", vendorId.Value); }
        if (status.HasValue) { sql += " AND status = @Status"; p.Add("Status", status.Value.ToString()); }
        sql += " ORDER BY created_at DESC OFFSET @Skip LIMIT @Take";
        p.Add("Skip", skip); p.Add("Take", take);
        var rows = await conn.QueryAsync<PurchaseOrder>(new CommandDefinition(sql, p, cancellationToken: ct));
        return rows.AsList();
    }
    public async Task<IReadOnlyList<PurchaseOrder>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return new List<PurchaseOrder>();
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var rows = await conn.QueryAsync<PurchaseOrder>(new CommandDefinition(
            $"SELECT {SelPo} FROM purchase_orders WHERE id = ANY(@Ids)",
            new { Ids = idList.ToArray() }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertAsync(PurchaseOrder po, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO purchase_orders (id, tenant_id, po_number, vendor_id, project_id, cost_center_id, status, order_date, expected_date,
                                         currency, sub_total, tax_amount, total_amount, notes,
                                         approved_at, approved_by, sent_at,
                                         created_at, created_by, updated_at, updated_by)
            VALUES (@Id, @TenantId, @PoNumber, @VendorId, @ProjectId, @CostCenterId, @Status, @OrderDate, @ExpectedDate,
                    @Currency, @SubTotal, @TaxAmount, @TotalAmount, @Notes,
                    @ApprovedAt, @ApprovedBy, @SentAt,
                    @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy)", new
        {
            po.Id, po.TenantId, po.PoNumber, po.VendorId, po.ProjectId, po.CostCenterId,
            Status = po.Status.ToString(),
            po.OrderDate, po.ExpectedDate, po.Currency,
            po.SubTotal, po.TaxAmount, po.TotalAmount, po.Notes,
            po.ApprovedAt, po.ApprovedBy, po.SentAt,
            po.CreatedAt, po.CreatedBy, po.UpdatedAt, po.UpdatedBy
        }, cancellationToken: ct));
    }

    public async Task UpdateAsync(PurchaseOrder po, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE purchase_orders SET vendor_id = @VendorId, status = @Status, order_date = @OrderDate,
                                       expected_date = @ExpectedDate, currency = @Currency,
                                       sub_total = @SubTotal, tax_amount = @TaxAmount, total_amount = @TotalAmount,
                                       notes = @Notes, approved_at = @ApprovedAt, approved_by = @ApprovedBy,
                                       sent_at = @SentAt, updated_at = @UpdatedAt, updated_by = @UpdatedBy
            WHERE id = @Id", new
        {
            po.Id, po.VendorId, Status = po.Status.ToString(),
            po.OrderDate, po.ExpectedDate, po.Currency,
            po.SubTotal, po.TaxAmount, po.TotalAmount, po.Notes,
            po.ApprovedAt, po.ApprovedBy, po.SentAt,
            po.UpdatedAt, po.UpdatedBy
        }, cancellationToken: ct));
    }

    public async Task InsertLinesAsync(Guid tenantId, Guid poId, IEnumerable<PurchaseOrderLine> lines, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        foreach (var l in lines)
        {
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO purchase_order_lines (id, tenant_id, purchase_order_id, item_id, quantity, unit_price, tax_rate, sub_total, line_order)
                VALUES (@Id, @TenantId, @PurchaseOrderId, @ItemId, @Quantity, @UnitPrice, @TaxRate, @SubTotal, @LineOrder)",
                new
                {
                    l.Id, TenantId = tenantId, PurchaseOrderId = poId,
                    l.ItemId, l.Quantity, l.UnitPrice, l.TaxRate, l.SubTotal, l.LineOrder
                }, cancellationToken: ct));
        }
    }

    public async Task UpdateLinesAsync(Guid tenantId, Guid poId, IEnumerable<PurchaseOrderLine> lines, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM purchase_order_lines WHERE purchase_order_id = @PoId", new { PoId = poId }, cancellationToken: ct));
        await InsertLinesAsync(tenantId, poId, lines, ct);
    }

    public async Task<IReadOnlyList<PurchaseOrderLine>> GetLinesAsync(Guid poId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var rows = await conn.QueryAsync<PurchaseOrderLine>(new CommandDefinition(
            $"SELECT {SelLine} FROM purchase_order_lines WHERE purchase_order_id = @PoId ORDER BY line_order",
            new { PoId = poId }, cancellationToken: ct));
        return rows.AsList();
    }
}
