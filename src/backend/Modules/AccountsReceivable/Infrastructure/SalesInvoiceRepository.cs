using Dapper;
using ERPSystem.Modules.AccountsReceivable.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.AccountsReceivable.Infrastructure;

/// <summary>تنفيذ ISalesInvoiceRepository عبر Dapper — يحسب Outstanding على الـ read.</summary>
public sealed class SalesInvoiceRepository : ISalesInvoiceRepository
{
    private readonly IDbConnectionFactory _db;
    public SalesInvoiceRepository(IDbConnectionFactory db) => _db = db;

    private const string SelInv = @"id, tenant_id AS TenantId, company_id AS CompanyId, customer_id AS CustomerId,
        invoice_number AS InvoiceNumber, invoice_date AS InvoiceDate, due_date AS DueDate,
        currency_code AS CurrencyCode, exchange_rate AS ExchangeRate,
        subtotal, tax_amount AS TaxAmount, total_amount AS TotalAmount, paid_amount AS PaidAmount,
        (total_amount - paid_amount) AS Outstanding,
        status, notes, project_id AS ProjectId,
        posted_at AS PostedAt, posted_by AS PostedBy, journal_entry_id AS JournalEntryId,
        created_at AS CreatedAt, created_by AS CreatedBy, updated_at AS UpdatedAt, updated_by AS UpdatedBy";

    private const string SelLine = @"id, tenant_id AS TenantId, sales_invoice_id AS SalesInvoiceId,
        item_id AS ItemId, description, line_number AS LineNumber,
        quantity, unit_price AS UnitPrice, tax_rate AS TaxRate, line_total AS LineTotal";

    public async Task<SalesInvoice?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var inv = await conn.QueryFirstOrDefaultAsync<SalesInvoice>(new CommandDefinition(
            $"SELECT {SelInv} FROM sales_invoices WHERE id = @Id LIMIT 1",
            new { Id = id }, cancellationToken: ct));
        if (inv != null)
        {
            inv.Lines = (await GetLinesAsync(inv.Id, ct)).ToList();
        }
        return inv;
    }

    public async Task<SalesInvoice?> GetByInvoiceNumberAsync(Guid tenantId, string invoiceNumber, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<SalesInvoice>(new CommandDefinition(
            $"SELECT {SelInv} FROM sales_invoices WHERE tenant_id = @TenantId AND invoice_number = @InvoiceNumber LIMIT 1",
            new { TenantId = tenantId, InvoiceNumber = invoiceNumber }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<SalesInvoice>> ListAsync(Guid tenantId, Guid? customerId, SalesInvoiceStatus? status, int skip, int take, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {SelInv} FROM sales_invoices WHERE tenant_id = @TenantId";
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (customerId.HasValue) { sql += " AND customer_id = @CustomerId"; p.Add("CustomerId", customerId.Value); }
        if (status.HasValue) { sql += " AND status = @Status"; p.Add("Status", status.Value.ToString()); }
        sql += " ORDER BY created_at DESC OFFSET @Skip LIMIT @Take";
        p.Add("Skip", skip); p.Add("Take", take);
        var rows = await conn.QueryAsync<SalesInvoice>(new CommandDefinition(sql, p, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<SalesInvoice>> ListOpenByCustomerAsync(Guid tenantId, Guid customerId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $@"SELECT {SelInv} FROM sales_invoices
                     WHERE tenant_id = @TenantId AND customer_id = @CustomerId
                       AND status NOT IN ('Cancelled','Paid')
                       AND total_amount > paid_amount
                     ORDER BY invoice_date ASC, invoice_number ASC";
        var rows = await conn.QueryAsync<SalesInvoice>(new CommandDefinition(sql,
            new { TenantId = tenantId, CustomerId = customerId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<SalesInvoice>> ListAllOpenAsync(Guid tenantId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $@"SELECT {SelInv} FROM sales_invoices
                     WHERE tenant_id = @TenantId
                       AND status NOT IN ('Cancelled','Paid')
                       AND total_amount > paid_amount
                     ORDER BY customer_id, invoice_date";
        var rows = await conn.QueryAsync<SalesInvoice>(new CommandDefinition(sql,
            new { TenantId = tenantId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertAsync(SalesInvoice inv, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO sales_invoices (id, tenant_id, company_id, customer_id, invoice_number,
                                        invoice_date, due_date, currency_code, exchange_rate,
                                        subtotal, tax_amount, total_amount, paid_amount, status, notes, project_id,
                                        posted_at, posted_by, journal_entry_id,
                                        created_at, created_by, updated_at, updated_by)
            VALUES (@Id, @TenantId, @CompanyId, @CustomerId, @InvoiceNumber,
                    @InvoiceDate, @DueDate, @CurrencyCode, @ExchangeRate,
                    @Subtotal, @TaxAmount, @TotalAmount, @PaidAmount, @Status, @Notes, @ProjectId,
                    @PostedAt, @PostedBy, @JournalEntryId,
                    @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy)", new
        {
            inv.Id, inv.TenantId, inv.CompanyId, inv.CustomerId, inv.InvoiceNumber,
            inv.InvoiceDate, inv.DueDate, inv.CurrencyCode, inv.ExchangeRate,
            inv.Subtotal, inv.TaxAmount, inv.TotalAmount, inv.PaidAmount,
            Status = inv.Status.ToString(), inv.Notes, inv.ProjectId,
            inv.PostedAt, inv.PostedBy, inv.JournalEntryId,
            inv.CreatedAt, inv.CreatedBy, inv.UpdatedAt, inv.UpdatedBy
        }, cancellationToken: ct));
    }

    public async Task UpdateAsync(SalesInvoice inv, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE sales_invoices SET customer_id = @CustomerId, invoice_date = @InvoiceDate,
                                      due_date = @DueDate, currency_code = @CurrencyCode, exchange_rate = @ExchangeRate,
                                      subtotal = @Subtotal, tax_amount = @TaxAmount, total_amount = @TotalAmount,
                                      paid_amount = @PaidAmount, status = @Status, notes = @Notes, project_id = @ProjectId,
                                      posted_at = @PostedAt, posted_by = @PostedBy, journal_entry_id = @JournalEntryId,
                                      updated_at = @UpdatedAt, updated_by = @UpdatedBy
            WHERE id = @Id", new
        {
            inv.CustomerId, inv.InvoiceDate, inv.DueDate, inv.CurrencyCode, inv.ExchangeRate,
            inv.Subtotal, inv.TaxAmount, inv.TotalAmount, inv.PaidAmount,
            Status = inv.Status.ToString(), inv.Notes, inv.ProjectId,
            inv.PostedAt, inv.PostedBy, inv.JournalEntryId,
            inv.UpdatedAt, inv.UpdatedBy,
            inv.Id
        }, cancellationToken: ct));
    }

    public async Task InsertLinesAsync(Guid tenantId, Guid salesInvoiceId, IEnumerable<SalesInvoiceLine> lines, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        foreach (var l in lines)
        {
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO sales_invoice_lines (id, tenant_id, sales_invoice_id, item_id, description, line_number,
                                                  quantity, unit_price, tax_rate, line_total)
                VALUES (@Id, @TenantId, @SalesInvoiceId, @ItemId, @Description, @LineNumber,
                        @Quantity, @UnitPrice, @TaxRate, @LineTotal)",
                new
                {
                    l.Id, TenantId = tenantId, SalesInvoiceId = salesInvoiceId,
                    l.ItemId, l.Description, l.LineNumber,
                    l.Quantity, l.UnitPrice, l.TaxRate, l.LineTotal
                }, cancellationToken: ct));
        }
    }

    public async Task UpdateLinesAsync(Guid tenantId, Guid salesInvoiceId, IEnumerable<SalesInvoiceLine> lines, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM sales_invoice_lines WHERE sales_invoice_id = @InvId", new { InvId = salesInvoiceId }, cancellationToken: ct));
        await InsertLinesAsync(tenantId, salesInvoiceId, lines, ct);
    }

    public async Task<IReadOnlyList<SalesInvoiceLine>> GetLinesAsync(Guid salesInvoiceId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var rows = await conn.QueryAsync<SalesInvoiceLine>(new CommandDefinition(
            $"SELECT {SelLine} FROM sales_invoice_lines WHERE sales_invoice_id = @InvId ORDER BY line_number",
            new { InvId = salesInvoiceId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<decimal> GetTotalAllocatedAsync(Guid tenantId, Guid salesInvoiceId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sum = await conn.QueryFirstOrDefaultAsync<decimal?>(new CommandDefinition(@"
            SELECT COALESCE(SUM(ra.amount_applied), 0)
            FROM receipt_allocations ra
            INNER JOIN receipts r ON r.id = ra.receipt_id
            WHERE ra.tenant_id = @TenantId AND ra.sales_invoice_id = @InvId
              AND r.status = 'Posted'",
            new { TenantId = tenantId, InvId = salesInvoiceId }, cancellationToken: ct));
        return sum ?? 0m;
    }
}
