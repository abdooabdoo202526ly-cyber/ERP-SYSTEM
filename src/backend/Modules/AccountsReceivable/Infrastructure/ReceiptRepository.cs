using Dapper;
using ERPSystem.Modules.AccountsReceivable.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.AccountsReceivable.Infrastructure;

/// <summary>تنفيذ IReceiptRepository عبر Dapper.</summary>
public sealed class ReceiptRepository : IReceiptRepository
{
    private readonly IDbConnectionFactory _db;
    public ReceiptRepository(IDbConnectionFactory db) => _db = db;

    private const string SelR = @"id, tenant_id AS TenantId, company_id AS CompanyId, customer_id AS CustomerId,
        receipt_number AS ReceiptNumber, receipt_date AS ReceiptDate, amount,
        currency_code AS CurrencyCode, payment_method AS PaymentMethod, notes,
        posted_at AS PostedAt, posted_by AS PostedBy, journal_entry_id AS JournalEntryId,
        created_at AS CreatedAt, created_by AS CreatedBy, updated_at AS UpdatedAt, updated_by AS UpdatedBy";

    private const string SelA = @"id, tenant_id AS TenantId, receipt_id AS ReceiptId,
        sales_invoice_id AS SalesInvoiceId, amount_applied AS AmountApplied";

    public async Task<Receipt?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var r = await conn.QueryFirstOrDefaultAsync<Receipt>(new CommandDefinition(
            $"SELECT {SelR} FROM receipts WHERE id = @Id LIMIT 1",
            new { Id = id }, cancellationToken: ct));
        if (r != null)
        {
            r.Allocations = (await GetAllocationsAsync(r.Id, ct)).ToList();
        }
        return r;
    }

    public async Task<Receipt?> GetByReceiptNumberAsync(Guid tenantId, string receiptNumber, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Receipt>(new CommandDefinition(
            $"SELECT {SelR} FROM receipts WHERE tenant_id = @TenantId AND receipt_number = @ReceiptNumber LIMIT 1",
            new { TenantId = tenantId, ReceiptNumber = receiptNumber }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Receipt>> ListAsync(Guid tenantId, Guid? customerId, int skip, int take, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {SelR} FROM receipts WHERE tenant_id = @TenantId";
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (customerId.HasValue) { sql += " AND customer_id = @CustomerId"; p.Add("CustomerId", customerId.Value); }
        sql += " ORDER BY created_at DESC OFFSET @Skip LIMIT @Take";
        p.Add("Skip", skip); p.Add("Take", take);
        var rows = await conn.QueryAsync<Receipt>(new CommandDefinition(sql, p, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertAsync(Receipt r, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO receipts (id, tenant_id, company_id, customer_id, receipt_number, receipt_date, amount,
                                  currency_code, payment_method, notes,
                                  posted_at, posted_by, journal_entry_id,
                                  created_at, created_by, updated_at, updated_by)
            VALUES (@Id, @TenantId, @CompanyId, @CustomerId, @ReceiptNumber, @ReceiptDate, @Amount,
                    @CurrencyCode, @PaymentMethod, @Notes,
                    @PostedAt, @PostedBy, @JournalEntryId,
                    @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy)", new
        {
            r.Id, r.TenantId, r.CompanyId, r.CustomerId, r.ReceiptNumber, r.ReceiptDate, r.Amount,
            r.CurrencyCode, r.PaymentMethod, r.Notes,
            r.PostedAt, r.PostedBy, r.JournalEntryId,
            r.CreatedAt, r.CreatedBy, r.UpdatedAt, r.UpdatedBy
        }, cancellationToken: ct));
    }

    public async Task UpdateAsync(Receipt r, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE receipts SET customer_id = @CustomerId, receipt_date = @ReceiptDate, amount = @Amount,
                               currency_code = @CurrencyCode, payment_method = @PaymentMethod, notes = @Notes,
                               posted_at = @PostedAt, posted_by = @PostedBy, journal_entry_id = @JournalEntryId,
                               updated_at = @UpdatedAt, updated_by = @UpdatedBy
            WHERE id = @Id", new
        {
            r.CustomerId, r.ReceiptDate, r.Amount, r.CurrencyCode, r.PaymentMethod, r.Notes,
            r.PostedAt, r.PostedBy, r.JournalEntryId,
            r.UpdatedAt, r.UpdatedBy,
            r.Id
        }, cancellationToken: ct));
    }

    public async Task InsertAllocationsAsync(Guid tenantId, Guid receiptId, IEnumerable<ReceiptAllocation> allocations, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        foreach (var a in allocations)
        {
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO receipt_allocations (id, tenant_id, receipt_id, sales_invoice_id, amount_applied)
                VALUES (@Id, @TenantId, @ReceiptId, @SalesInvoiceId, @AmountApplied)",
                new { a.Id, TenantId = tenantId, ReceiptId = receiptId, a.SalesInvoiceId, a.AmountApplied },
                cancellationToken: ct));
        }
    }

    public async Task<IReadOnlyList<ReceiptAllocation>> GetAllocationsAsync(Guid receiptId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var rows = await conn.QueryAsync<ReceiptAllocation>(new CommandDefinition(
            $"SELECT {SelA} FROM receipt_allocations WHERE receipt_id = @Rid",
            new { Rid = receiptId }, cancellationToken: ct));
        return rows.AsList();
    }
}
