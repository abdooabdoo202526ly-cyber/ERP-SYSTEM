using Dapper;
using ERPSystem.Modules.Procurement.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Procurement.Infrastructure;

/// <summary>تنفيذ IVendorBillRepository عبر Dapper.</summary>
public sealed class VendorBillRepository : IVendorBillRepository
{
    private readonly IDbConnectionFactory _db;
    public VendorBillRepository(IDbConnectionFactory db) => _db = db;

    private const string SelVb = @"id, tenant_id AS TenantId, bill_number AS BillNumber, goods_receipt_id AS GoodsReceiptId,
        vendor_id AS VendorId, status, bill_date AS BillDate, due_date AS DueDate, currency,
        sub_total AS SubTotal, tax_amount AS TaxAmount, total_amount AS TotalAmount, notes,
        journal_entry_id AS JournalEntryId, posted_at AS PostedAt,
        created_at AS CreatedAt, created_by AS CreatedBy, updated_at AS UpdatedAt, updated_by AS UpdatedBy";

    private const string SelLine = @"id, tenant_id AS TenantId, vendor_bill_id AS VendorBillId,
        item_id AS ItemId, quantity, unit_price AS UnitPrice, tax_rate AS TaxRate, sub_total AS SubTotal, line_order AS LineOrder";

    public async Task<VendorBill?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var bill = await conn.QueryFirstOrDefaultAsync<VendorBill>(new CommandDefinition(
            $"SELECT {SelVb} FROM vendor_bills WHERE id = @Id LIMIT 1",
            new { Id = id }, cancellationToken: ct));
        if (bill != null) bill.Lines = (await GetLinesAsync(bill.Id, ct)).ToList();
        return bill;
    }

    public async Task<VendorBill?> GetByBillNumberAsync(Guid tenantId, string billNumber, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<VendorBill>(new CommandDefinition(
            $"SELECT {SelVb} FROM vendor_bills WHERE tenant_id = @TenantId AND bill_number = @BillNumber LIMIT 1",
            new { TenantId = tenantId, BillNumber = billNumber }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<VendorBill>> ListAsync(Guid tenantId, Guid? vendorId, Guid? grId, VendorBillStatus? status, int skip, int take, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {SelVb} FROM vendor_bills WHERE tenant_id = @TenantId";
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (vendorId.HasValue) { sql += " AND vendor_id = @VendorId"; p.Add("VendorId", vendorId.Value); }
        if (grId.HasValue) { sql += " AND goods_receipt_id = @GrId"; p.Add("GrId", grId.Value); }
        if (status.HasValue) { sql += " AND status = @Status"; p.Add("Status", status.Value.ToString()); }
        sql += " ORDER BY created_at DESC OFFSET @Skip LIMIT @Take";
        p.Add("Skip", skip); p.Add("Take", take);
        var rows = (await conn.QueryAsync<VendorBill>(new CommandDefinition(sql, p, cancellationToken: ct))).AsList();

        // DEC-074: Load lines for each bill (BUG-003 fix — was empty before)
        // Simple N+1 pattern; can be optimized with a JOIN if needed
        if (rows.Count > 0)
        {
            var billIds = rows.Select(b => b.Id).ToList();
            var allLines = await conn.QueryAsync<VendorBillLine>(new CommandDefinition(
                $"SELECT {SelLine} FROM vendor_bill_lines WHERE vendor_bill_id = ANY(@BillIds) ORDER BY line_order",
                new { BillIds = billIds.ToArray() }, cancellationToken: ct));
            var linesByBill = allLines.GroupBy(l => l.VendorBillId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var bill in rows)
            {
                bill.Lines = linesByBill.TryGetValue(bill.Id, out var ls) ? ls : new List<VendorBillLine>();
            }
        }

        return rows;
    }

    public async Task InsertAsync(VendorBill bill, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO vendor_bills (id, tenant_id, bill_number, goods_receipt_id, vendor_id, status, bill_date, due_date,
                                      currency, sub_total, tax_amount, total_amount, notes,
                                      journal_entry_id, posted_at,
                                      created_at, created_by, updated_at, updated_by)
            VALUES (@Id, @TenantId, @BillNumber, @GoodsReceiptId, @VendorId, @Status, @BillDate, @DueDate,
                    @Currency, @SubTotal, @TaxAmount, @TotalAmount, @Notes,
                    @JournalEntryId, @PostedAt,
                    @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy)", new
        {
            bill.Id, bill.TenantId, bill.BillNumber, bill.GoodsReceiptId, bill.VendorId,
            Status = bill.Status.ToString(),
            bill.BillDate, bill.DueDate, bill.Currency,
            bill.SubTotal, bill.TaxAmount, bill.TotalAmount, bill.Notes,
            bill.JournalEntryId, bill.PostedAt,
            bill.CreatedAt, bill.CreatedBy, bill.UpdatedAt, bill.UpdatedBy
        }, cancellationToken: ct));
    }

    public async Task UpdateAsync(VendorBill bill, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE vendor_bills SET status = @Status, bill_date = @BillDate, due_date = @DueDate,
                                    currency = @Currency, sub_total = @SubTotal, tax_amount = @TaxAmount,
                                    total_amount = @TotalAmount, notes = @Notes,
                                    journal_entry_id = @JournalEntryId, posted_at = @PostedAt,
                                    updated_at = @UpdatedAt, updated_by = @UpdatedBy
            WHERE id = @Id", new
        {
            bill.Id, Status = bill.Status.ToString(), bill.BillDate, bill.DueDate, bill.Currency,
            bill.SubTotal, bill.TaxAmount, bill.TotalAmount, bill.Notes,
            bill.JournalEntryId, bill.PostedAt,
            bill.UpdatedAt, bill.UpdatedBy
        }, cancellationToken: ct));
    }

    public async Task InsertLinesAsync(Guid tenantId, Guid billId, IEnumerable<VendorBillLine> lines, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        foreach (var l in lines)
        {
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO vendor_bill_lines (id, tenant_id, vendor_bill_id, item_id, quantity, unit_price, tax_rate, sub_total, line_order)
                VALUES (@Id, @TenantId, @VendorBillId, @ItemId, @Quantity, @UnitPrice, @TaxRate, @SubTotal, @LineOrder)",
                new
                {
                    l.Id, TenantId = tenantId, VendorBillId = billId,
                    l.ItemId, l.Quantity, l.UnitPrice, l.TaxRate, l.SubTotal, l.LineOrder
                }, cancellationToken: ct));
        }
    }

    public async Task<IReadOnlyList<VendorBillLine>> GetLinesAsync(Guid billId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var rows = await conn.QueryAsync<VendorBillLine>(new CommandDefinition(
            $"SELECT {SelLine} FROM vendor_bill_lines WHERE vendor_bill_id = @BillId ORDER BY line_order",
            new { BillId = billId }, cancellationToken: ct));
        return rows.AsList();
    }
}
