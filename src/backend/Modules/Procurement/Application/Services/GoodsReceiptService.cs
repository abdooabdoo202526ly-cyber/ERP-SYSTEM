using ERPSystem.Modules.Procurement.Application;
using ERPSystem.Modules.Procurement.Entities;
using ERPSystem.Modules.Procurement.Infrastructure;
using ERPSystem.Modules.Companies.Infrastructure;
using ERPSystem.Modules.Inventory.Application;
using ERPSystem.Modules.Inventory.Application.Services;
using ERPSystem.Modules.Inventory.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Modules.Procurement.Application.Services;

public interface IGoodsReceiptService
{
    Task<ProcurementResult<GoodsReceiptResponse>> CreateAsync(Guid tenantId, Guid userId, CreateGoodsReceiptRequest req, CancellationToken ct);
    Task<ProcurementResult<GoodsReceiptResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<ProcurementResult<IReadOnlyList<GoodsReceiptResponse>>> ListAsync(Guid tenantId, Guid? poId, GoodsReceiptStatus? status, int skip, int take, CancellationToken ct);
    Task<ProcurementResult<GoodsReceiptResponse>> ReceiveAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
    // v1.0.27: cancel a Draft GR
    Task<ProcurementResult<bool>> CancelAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
}

public sealed class GoodsReceiptService : IGoodsReceiptService
{
    private readonly IGoodsReceiptRepository _grs;
    private readonly IPurchaseOrderRepository _pos;
    private readonly IStockMovementService _stockService;
    private readonly IDocumentSequenceRepository _seq;
    private readonly ICompanyRepository _companies;
    private readonly IVendorRepository _vendors;
    private readonly IWarehouseRepository _warehouses;
    private readonly ILogger<GoodsReceiptService> _logger;

    public GoodsReceiptService(IGoodsReceiptRepository grs, IPurchaseOrderRepository pos,
        IStockMovementService stockService, IDocumentSequenceRepository seq,
        ICompanyRepository companies, IVendorRepository vendors, IWarehouseRepository warehouses,
        ILogger<GoodsReceiptService> logger)
    { _grs = grs; _pos = pos; _stockService = stockService; _seq = seq; _companies = companies; _vendors = vendors; _warehouses = warehouses; _logger = logger; }

    public async Task<ProcurementResult<GoodsReceiptResponse>> CreateAsync(Guid tenantId, Guid userId, CreateGoodsReceiptRequest req, CancellationToken ct)
    {
        // Business rule: GR لا يُنشأ إلا لـ PO في حالة Approved أو Sent
        var po = await _pos.GetByIdAsync(req.PurchaseOrderId, ct);
        if (po == null || po.TenantId != tenantId)
            return ProcurementResult<GoodsReceiptResponse>.Fail("PO غير موجود.", ProcurementErrorCode.NotFound);
        // v1.0.31: نسمح بـ Draft أيضاً (إذا الـ user يبي يـ create GR بدون موافقة، الـ status draft يعني لم تتم الموافقة بعد)
        if (po.Status == PurchaseOrderStatus.Cancelled || po.Status == PurchaseOrderStatus.Received)
            return ProcurementResult<GoodsReceiptResponse>.Fail(
                $"لا يمكن إنشاء GR لـ PO في حالة {po.Status} (يجب Draft أو Approved أو Sent).", ProcurementErrorCode.BusinessRuleViolation);

        // التحقق من الكميات المُستلمة لا تتجاوز الكمية في PO
        var poQtyByItem = po.Lines.GroupBy(l => l.ItemId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var reqQtyByItem = req.Lines.GroupBy(l => l.ItemId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        foreach (var kv in reqQtyByItem)
        {
            if (!poQtyByItem.TryGetValue(kv.Key, out var poQty))
                return ProcurementResult<GoodsReceiptResponse>.Fail(
                    $"الصنف {kv.Key} غير موجود في الـ PO.", ProcurementErrorCode.BusinessRuleViolation);
            if (kv.Value > poQty)
                return ProcurementResult<GoodsReceiptResponse>.Fail(
                    $"الكمية المُستلمة للصنف {kv.Key} ({kv.Value}) تتجاوز الكمية في PO ({poQty}).", ProcurementErrorCode.BusinessRuleViolation);
        }

        var grNumber = await _seq.GetNextNumberAsync(tenantId, "GR", ct);

        var lineEntities = new List<GoodsReceiptLine>();
        for (int i = 0; i < req.Lines.Count; i++)
        {
            var l = req.Lines[i];
            lineEntities.Add(new GoodsReceiptLine
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                ItemId = l.ItemId, Quantity = l.Quantity, UnitCost = l.UnitCost,
                Notes = l.Notes, LineOrder = i
            });
        }

        var now = DateTime.UtcNow;
        var gr = new GoodsReceipt
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            GrNumber = grNumber, PurchaseOrderId = req.PurchaseOrderId,
            Status = GoodsReceiptStatus.Draft,
            ReceivedDate = req.ReceivedDate, WarehouseId = req.WarehouseId,
            Notes = req.Notes,
            CreatedAt = now, CreatedBy = userId, UpdatedAt = now, UpdatedBy = userId
        };
        await _grs.InsertAsync(gr, ct);
        await _grs.InsertLinesAsync(tenantId, gr.Id, lineEntities, ct);
        gr.Lines = lineEntities;
        return ProcurementResult<GoodsReceiptResponse>.Ok(MapToResponse(gr));
    }

    public async Task<ProcurementResult<GoodsReceiptResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var gr = await _grs.GetByIdAsync(id, ct);
        if (gr == null || gr.TenantId != tenantId)
            return ProcurementResult<GoodsReceiptResponse>.Fail("غير موجود.", ProcurementErrorCode.NotFound);
        return ProcurementResult<GoodsReceiptResponse>.Ok(await EnrichAsync(gr, ct));
    }

    public async Task<ProcurementResult<IReadOnlyList<GoodsReceiptResponse>>> ListAsync(Guid tenantId, Guid? poId, GoodsReceiptStatus? status, int skip, int take, CancellationToken ct)
    {
        if (take is < 1 or > 200) take = 50;
        var list = await _grs.ListAsync(tenantId, poId, status, skip, take, ct);
        if (list.Count == 0) return ProcurementResult<IReadOnlyList<GoodsReceiptResponse>>.Ok(new List<GoodsReceiptResponse>());

        // DEC-031: Batch fetch related entities (avoid N+1)
        var poIds = list.Select(g => g.PurchaseOrderId).Distinct().ToList();
        var warehouseIds = list.Select(g => g.WarehouseId).Distinct().ToList();
        var pos = await _pos.GetByIdsAsync(poIds, ct);
        var warehouses = await _warehouses.GetByIdsAsync(warehouseIds, ct);
        var vendorIds = pos.Select(p => p.VendorId).Distinct().ToList();
        var vendors = vendorIds.Count > 0 ? await _vendors.GetByIdsAsync(vendorIds, ct) : new List<Vendor>();

        var posMap = pos.ToDictionary(p => p.Id);
        var whMap = warehouses.ToDictionary(w => w.Id);
        var vendorMap = vendors.ToDictionary(v => v.Id);

        var responses = list.Select(g =>
        {
            var resp = MapToResponse(g);
            posMap.TryGetValue(g.PurchaseOrderId, out var po);
            whMap.TryGetValue(g.WarehouseId, out var wh);
            if (po != null)
            {
                resp.PoNumber = po.PoNumber;
                resp.PoStatus = po.Status.ToString();
                if (vendorMap.TryGetValue(po.VendorId, out var v))
                {
                    resp.VendorId = v.Id;
                    resp.VendorName = v.Name;
                    resp.VendorCode = v.Code;
                }
            }
            if (wh != null)
            {
                resp.WarehouseName = wh.Name;
                resp.WarehouseCode = wh.Code;
            }
            return resp;
        }).ToList();

        return ProcurementResult<IReadOnlyList<GoodsReceiptResponse>>.Ok(responses);
    }

    /// <summary>
    /// تأكيد الاستلام: GR (Draft → Received) → لكل بند StockMovement.Receive (Draft) → PostAsync
    /// (يحدّث stock_levels + moving weighted average + StockReceivedEvent → Finance JournalEntry).
    /// وأخيراً PO → Received.
    /// </summary>
    public async Task<ProcurementResult<GoodsReceiptResponse>> ReceiveAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        var gr = await _grs.GetByIdAsync(id, ct);
        if (gr == null || gr.TenantId != tenantId)
            return ProcurementResult<GoodsReceiptResponse>.Fail("غير موجود.", ProcurementErrorCode.NotFound);
        if (gr.Status != GoodsReceiptStatus.Draft)
            return ProcurementResult<GoodsReceiptResponse>.Fail(
                $"لا يمكن تأكيد استلام GR في حالة {gr.Status}.", ProcurementErrorCode.InvalidStatusTransition);

        // لكل بند: أنشئ Receive StockMovement ثم Post
        // الشركة: جلب holding company للمستأجر (مرّة واحدة لكل بند — stock_movements.company_id NOT NULL FK → companies.id)
        var holdingCompanyId = await _companies.GetHoldingCompanyIdAsync(tenantId, ct);
        if (holdingCompanyId == null)
            return ProcurementResult<GoodsReceiptResponse>.Fail(
                "لا توجد شركة قابضة (holding) للمستأجر — لا يمكن استلام GR.", ProcurementErrorCode.BusinessRuleViolation);

        foreach (var line in gr.Lines)
        {
            var receiveReq = new ReceiveStockRequest
            {
                CompanyId = holdingCompanyId.Value,
                Reference = $"{gr.GrNumber}-{line.LineOrder}",
                MovementDate = gr.ReceivedDate,
                ItemId = line.ItemId,
                WarehouseId = gr.WarehouseId,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
                SourceType = "GoodsReceipt",
                SourceId = gr.Id,
                Notes = gr.Notes
            };
            var createRes = await _stockService.CreateReceiveAsync(tenantId, userId, receiveReq, ct);
            if (!createRes.Succeeded)
                return ProcurementResult<GoodsReceiptResponse>.Fail(
                    $"فشل إنشاء حركة المخزون: {createRes.Error}", ProcurementErrorCode.Internal);

            var postRes = await _stockService.PostAsync(tenantId, userId, createRes.Value!.Id, ct);
            if (!postRes.Succeeded)
                return ProcurementResult<GoodsReceiptResponse>.Fail(
                    $"فشل ترحيل حركة المخزون: {postRes.Error}", ProcurementErrorCode.Internal);
        }

        gr.Status = GoodsReceiptStatus.Received;
        gr.UpdatedAt = DateTime.UtcNow;
        gr.UpdatedBy = userId;
        await _grs.UpdateAsync(gr, ct);

        // تحديث PO إلى Received
        var po = await _pos.GetByIdAsync(gr.PurchaseOrderId, ct);
        if (po != null && po.Status != PurchaseOrderStatus.Received)
        {
            po.Status = PurchaseOrderStatus.Received;
            po.UpdatedAt = DateTime.UtcNow;
            po.UpdatedBy = userId;
            await _pos.UpdateAsync(po, ct);
        }

        _logger.LogInformation("تم استلام GR {GrNumber} وإنشاء {Count} حركات مخزون", gr.GrNumber, gr.Lines.Count);
        return ProcurementResult<GoodsReceiptResponse>.Ok(await EnrichAsync(gr, ct));
    }

    public async Task<ProcurementResult<bool>> CancelAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        var gr = await _grs.GetByIdAsync(id, ct);
        if (gr == null || gr.TenantId != tenantId)
            return ProcurementResult<bool>.Fail("غير موجود.", ProcurementErrorCode.NotFound);
        if (gr.Status != GoodsReceiptStatus.Draft)
            return ProcurementResult<bool>.Fail(
                $"لا يمكن إلغاء GR في حالة {gr.Status}.", ProcurementErrorCode.InvalidStatusTransition);

        gr.Status = GoodsReceiptStatus.Cancelled;
        gr.UpdatedAt = DateTime.UtcNow;
        gr.UpdatedBy = userId;
        await _grs.UpdateAsync(gr, ct);
        _logger.LogInformation("تم إلغاء GR {GrNumber} ({Id})", gr.GrNumber, gr.Id);
        return ProcurementResult<bool>.Ok(true);
    }

    private async Task<GoodsReceiptResponse> EnrichAsync(GoodsReceipt gr, CancellationToken ct)
    {
        var resp = MapToResponse(gr);
        var po = await _pos.GetByIdAsync(gr.PurchaseOrderId, ct);
        var wh = await _warehouses.GetByIdAsync(gr.WarehouseId, ct);
        if (po != null)
        {
            resp.PoNumber = po.PoNumber;
            resp.PoStatus = po.Status.ToString();
            var v = await _vendors.GetByIdAsync(po.VendorId, ct);
            if (v != null)
            {
                resp.VendorId = v.Id;
                resp.VendorName = v.Name;
                resp.VendorCode = v.Code;
            }
        }
        if (wh != null)
        {
            resp.WarehouseName = wh.Name;
            resp.WarehouseCode = wh.Code;
        }
        return resp;
    }

    private static GoodsReceiptResponse MapToResponse(GoodsReceipt gr) => new()
    {
        Id = gr.Id, TenantId = gr.TenantId, GrNumber = gr.GrNumber, PurchaseOrderId = gr.PurchaseOrderId,
        Status = gr.Status, ReceivedDate = gr.ReceivedDate, WarehouseId = gr.WarehouseId, Notes = gr.Notes,
        CreatedAt = gr.CreatedAt,
        Lines = gr.Lines.Select(l => new GoodsReceiptLineResponse
        {
            Id = l.Id, ItemId = l.ItemId, Quantity = l.Quantity, UnitCost = l.UnitCost, Notes = l.Notes, LineOrder = l.LineOrder
        }).ToList()
    };
}
