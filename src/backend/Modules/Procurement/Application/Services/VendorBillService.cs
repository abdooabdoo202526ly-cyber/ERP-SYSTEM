using Dapper;
using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Application.Services;
using ERPSystem.Modules.Procurement.Application;
using ERPSystem.Modules.Procurement.Entities;
using ERPSystem.Modules.Procurement.Infrastructure;
using ERPSystem.Shared.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Modules.Procurement.Application.Services;

public interface IVendorBillService
{
    Task<ProcurementResult<VendorBillResponse>> CreateAsync(Guid tenantId, Guid userId, CreateVendorBillRequest req, CancellationToken ct);
    Task<ProcurementResult<VendorBillResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<ProcurementResult<IReadOnlyList<VendorBillResponse>>> ListAsync(Guid tenantId, Guid? vendorId, Guid? grId, VendorBillStatus? status, int skip, int take, CancellationToken ct);
    Task<ProcurementResult<VendorBillResponse>> PostAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
}

public sealed class VendorBillService : IVendorBillService
{
    private readonly IVendorBillRepository _bills;
    private readonly IGoodsReceiptRepository _grs;
    private readonly IPurchaseOrderRepository _pos;
    private readonly IDocumentSequenceRepository _seq;
    private readonly IJournalEntryService _journalSvc;  // DEC-075: AP posting
    private readonly IDbConnectionFactory _db;          // DEC-075: account lookup
    private readonly ILogger<VendorBillService> _logger;

    public VendorBillService(IVendorBillRepository bills, IGoodsReceiptRepository grs, IPurchaseOrderRepository pos,
        IDocumentSequenceRepository seq, IJournalEntryService journalSvc, IDbConnectionFactory db,
        ILogger<VendorBillService> logger)
    {
        _bills = bills; _grs = grs; _pos = pos; _seq = seq;
        _journalSvc = journalSvc; _db = db; _logger = logger;
    }

    public async Task<ProcurementResult<VendorBillResponse>> CreateAsync(Guid tenantId, Guid userId, CreateVendorBillRequest req, CancellationToken ct)
    {
        // Business rule: Bill لا يُنشأ إلا لـ GR في حالة Received
        var gr = await _grs.GetByIdAsync(req.GoodsReceiptId, ct);
        if (gr == null || gr.TenantId != tenantId)
            return ProcurementResult<VendorBillResponse>.Fail("GR غير موجود.", ProcurementErrorCode.NotFound);
        // v1.0.31: نسمح بـ Draft أيضاً (مرونة)
        if (gr.Status == GoodsReceiptStatus.Cancelled)
            return ProcurementResult<VendorBillResponse>.Fail(
                $"لا يمكن إنشاء Bill لـ GR في حالة {gr.Status} (يجب Draft أو Received).", ProcurementErrorCode.BusinessRuleViolation);

        // VendorId يُجلب من PO المرتبط بالـ GR (denormalized على الـ Bill للـ queries السريعة)
        var po = await _pos.GetByIdAsync(gr.PurchaseOrderId, ct);
        var vendorId = po?.VendorId ?? Guid.Empty;

        var billNumber = await _seq.GetNextNumberAsync(tenantId, "BILL", ct);

        decimal subTotal = 0, taxAmount = 0;
        var lineEntities = new List<VendorBillLine>();
        for (int i = 0; i < req.Lines.Count; i++)
        {
            var l = req.Lines[i];
            var lineSub = l.Quantity * l.UnitPrice;
            var lineTax = lineSub * l.TaxRate;
            subTotal += lineSub; taxAmount += lineTax;
            lineEntities.Add(new VendorBillLine
            {
                Id = Guid.NewGuid(), TenantId = tenantId, VendorId = vendorId,
                ItemId = l.ItemId, Quantity = l.Quantity, UnitPrice = l.UnitPrice,
                TaxRate = l.TaxRate, SubTotal = lineSub, LineOrder = i
            });
        }
        var total = subTotal + taxAmount;

        var now = DateTime.UtcNow;
        var bill = new VendorBill
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            BillNumber = billNumber, GoodsReceiptId = gr.Id, VendorId = vendorId,
            Status = VendorBillStatus.Draft,
            BillDate = req.BillDate, DueDate = req.DueDate,
            Currency = req.Currency.ToUpperInvariant(),
            SubTotal = subTotal, TaxAmount = taxAmount, TotalAmount = total,
            Notes = req.Notes,
            CreatedAt = now, CreatedBy = userId, UpdatedAt = now, UpdatedBy = userId
        };
        await _bills.InsertAsync(bill, ct);
        await _bills.InsertLinesAsync(tenantId, bill.Id, lineEntities, ct);
        bill.Lines = lineEntities;
        return ProcurementResult<VendorBillResponse>.Ok(MapToResponse(bill));
    }

    public async Task<ProcurementResult<VendorBillResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var b = await _bills.GetByIdAsync(id, ct);
        if (b == null || b.TenantId != tenantId)
            return ProcurementResult<VendorBillResponse>.Fail("غير موجود.", ProcurementErrorCode.NotFound);
        return ProcurementResult<VendorBillResponse>.Ok(MapToResponse(b));
    }

    public async Task<ProcurementResult<IReadOnlyList<VendorBillResponse>>> ListAsync(Guid tenantId, Guid? vendorId, Guid? grId, VendorBillStatus? status, int skip, int take, CancellationToken ct)
    {
        if (take is < 1 or > 200) take = 50;
        var list = await _bills.ListAsync(tenantId, vendorId, grId, status, skip, take, ct);
        return ProcurementResult<IReadOnlyList<VendorBillResponse>>.Ok(list.Select(MapToResponse).ToList());
    }

    /// <summary>
    /// ترحيل Bill (Draft → Posted) — DEC-075: الآن ينشئ JournalEntry تلقائياً.
    /// Dr Inventory (1240) / Cr Accounts Payable (2210)
    /// </summary>
    public async Task<ProcurementResult<VendorBillResponse>> PostAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        var b = await _bills.GetByIdAsync(id, ct);
        if (b == null || b.TenantId != tenantId)
            return ProcurementResult<VendorBillResponse>.Fail("غير موجود.", ProcurementErrorCode.NotFound);
        if (b.Status != VendorBillStatus.Draft)
            return ProcurementResult<VendorBillResponse>.Fail(
                $"لا يمكن ترحيل Bill في حالة {b.Status}.", ProcurementErrorCode.InvalidStatusTransition);

        // DEC-075: Idempotency check — skip if already posted with JE
        if (b.JournalEntryId.HasValue && b.JournalEntryId != Guid.Empty)
        {
            _logger.LogInformation("Bill {BillNumber} already has JE {JE}, skipping post", b.BillNumber, b.JournalEntryId);
            return ProcurementResult<VendorBillResponse>.Ok(MapToResponse(b));
        }

        // DEC-075: Look up account IDs for Inventory (1240) and AP (2210)
        Guid? inventoryAcctId = null, apAcctId = null;
        try
        {
            using var conn = await _db.CreateOltpConnectionAsync(ct);
            var accts = await conn.QueryAsync<(string Code, Guid AcctId)>(new CommandDefinition(
                "SELECT code, id FROM accounts WHERE tenant_id = @T AND code IN ('1240', '2210')",
                new { T = tenantId }, cancellationToken: ct));
            foreach (var (code, acctId) in accts)
            {
                if (code == "1240") inventoryAcctId = acctId;
                if (code == "2210") apAcctId = acctId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEC-075: failed to look up accounts for bill posting");
        }

        if (inventoryAcctId == null || apAcctId == null)
        {
            _logger.LogWarning("DEC-075: missing accounts (1240={Inv}, 2210={AP}); posting bill {N} WITHOUT JE",
                inventoryAcctId, apAcctId, b.BillNumber);
            // Fall back to status-only update (legacy behavior)
            b.Status = VendorBillStatus.Posted;
            b.PostedAt = DateTime.UtcNow;
            b.UpdatedAt = DateTime.UtcNow;
            b.UpdatedBy = userId;
            await _bills.UpdateAsync(b, ct);
            return ProcurementResult<VendorBillResponse>.Ok(MapToResponse(b));
        }

        // DEC-075: Create JournalEntry — Dr Inventory, Cr AP
        var je = await _journalSvc.CreateDraftAsync(tenantId, userId, new PostJournalEntryRequest
        {
            EntryDate = b.BillDate,
            Description = $"Vendor Bill {b.BillNumber}",
            Reference = $"BILL-{b.BillNumber}",
            Lines = new List<PostJournalLineRequest>
            {
                new() { AccountId = inventoryAcctId.Value, Debit = b.TotalAmount, Credit = 0m,
                        Description = $"Inventory received — Bill {b.BillNumber}" },
                new() { AccountId = apAcctId.Value, Debit = 0m, Credit = b.TotalAmount,
                        Description = $"A/P to vendor — Bill {b.BillNumber}" }
            }
        }, ct);

        if (!je.Succeeded)
        {
            _logger.LogError("DEC-075: JE creation failed for bill {N}: {Err}", b.BillNumber, je.Error);
            return ProcurementResult<VendorBillResponse>.Fail(
                $"فشل إنشاء القيد المحاسبي: {je.Error}", ProcurementErrorCode.BusinessRuleViolation);
        }

        // Post the JE
        var postJe = await _journalSvc.PostAsync(tenantId, userId, je.Value!.Id, ct);
        if (!postJe.Succeeded)
        {
            _logger.LogError("DEC-075: JE post failed for bill {N}: {Err}", b.BillNumber, postJe.Error);
            return ProcurementResult<VendorBillResponse>.Fail(
                $"فشل ترحيل القيد: {postJe.Error}", ProcurementErrorCode.BusinessRuleViolation);
        }

        // Update bill status + link to JE
        b.Status = VendorBillStatus.Posted;
        b.JournalEntryId = je.Value!.Id;
        b.PostedAt = DateTime.UtcNow;
        b.UpdatedAt = DateTime.UtcNow;
        b.UpdatedBy = userId;
        await _bills.UpdateAsync(b, ct);

        _logger.LogInformation("DEC-075: Bill {N} posted — Dr Inv={D}, Cr AP={C}, JE={JE}",
            b.BillNumber, b.TotalAmount, b.TotalAmount, je.Value.Id);
        return ProcurementResult<VendorBillResponse>.Ok(MapToResponse(b));
    }

    private static VendorBillResponse MapToResponse(VendorBill b) => new()
    {
        Id = b.Id, TenantId = b.TenantId, BillNumber = b.BillNumber, GoodsReceiptId = b.GoodsReceiptId,
        VendorId = b.VendorId, Status = b.Status, BillDate = b.BillDate, DueDate = b.DueDate,
        Currency = b.Currency, SubTotal = b.SubTotal, TaxAmount = b.TaxAmount, TotalAmount = b.TotalAmount,
        Notes = b.Notes, JournalEntryId = b.JournalEntryId, PostedAt = b.PostedAt, CreatedAt = b.CreatedAt,
        Lines = b.Lines.Select(l => new VendorBillLineResponse
        {
            Id = l.Id, ItemId = l.ItemId, Quantity = l.Quantity, UnitPrice = l.UnitPrice,
            TaxRate = l.TaxRate, SubTotal = l.SubTotal, LineOrder = l.LineOrder
        }).ToList()
    };
}
