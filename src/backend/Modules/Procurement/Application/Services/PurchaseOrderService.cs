using ERPSystem.Modules.Companies.Infrastructure;
using ERPSystem.Modules.Projects.Infrastructure;
using ERPSystem.Modules.Procurement.Application;
using ERPSystem.Modules.Procurement.Entities;
using ERPSystem.Modules.Procurement.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Modules.Procurement.Application.Services;

public interface IPurchaseOrderService
{
    Task<ProcurementResult<PurchaseOrderResponse>> CreateAsync(Guid tenantId, Guid userId, CreatePurchaseOrderRequest req, CancellationToken ct);
    Task<ProcurementResult<PurchaseOrderResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<ProcurementResult<IReadOnlyList<PurchaseOrderResponse>>> ListAsync(Guid tenantId, Guid? vendorId, PurchaseOrderStatus? status, int skip, int take, CancellationToken ct);
    Task<ProcurementResult<PurchaseOrderResponse>> ApproveAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
    Task<ProcurementResult<PurchaseOrderResponse>> SendAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
    // v1.0.31: cancel PO
    Task<ProcurementResult<bool>> CancelAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
}

public sealed class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IPurchaseOrderRepository _pos;
    private readonly IVendorRepository _vendors;
    private readonly IDocumentSequenceRepository _seq;
    private readonly IProjectRepository _projects;
    private readonly ICostCenterRepository _costCenters;
    private readonly ILogger<PurchaseOrderService> _logger;

    public PurchaseOrderService(
        IPurchaseOrderRepository pos,
        IVendorRepository vendors,
        IDocumentSequenceRepository seq,
        IProjectRepository projects,
        ICostCenterRepository costCenters,
        ILogger<PurchaseOrderService> logger)
    {
        _pos = pos; _vendors = vendors; _seq = seq; _projects = projects; _costCenters = costCenters; _logger = logger;
    }

    public async Task<ProcurementResult<PurchaseOrderResponse>> CreateAsync(Guid tenantId, Guid userId, CreatePurchaseOrderRequest req, CancellationToken ct)
    {
        // التحقق من وجود المورّد
        var vendor = await _vendors.GetByIdAsync(req.VendorId, ct);
        if (vendor == null || vendor.TenantId != tenantId)
            return ProcurementResult<PurchaseOrderResponse>.Fail("المورّد غير موجود.", ProcurementErrorCode.NotFound);
        if (!vendor.IsActive)
            return ProcurementResult<PurchaseOrderResponse>.Fail("المورّد غير نشط.", ProcurementErrorCode.BusinessRuleViolation);

        // v1.0.34: تحديد مركز التكلفة (يدوي/مشتق/افتراضي)
        Guid? projectId = req.ProjectId;
        Guid? costCenterId = req.CostCenterId;
        string? projectName = null;
        if (projectId.HasValue)
        {
            var proj = await _projects.GetByIdAsync(projectId.Value, ct);
            if (proj == null || proj.TenantId != tenantId)
                return ProcurementResult<PurchaseOrderResponse>.Fail("المشروع غير موجود.", ProcurementErrorCode.NotFound);
            projectName = proj.Name;
            // إذا لم يحدد المستخدم cost center، خذه من المشروع
            if (!costCenterId.HasValue)
                costCenterId = proj.CostCenterId;
        }
        if (!costCenterId.HasValue)
        {
            // افتراضي: PROJ-DEFAULT
            var def = await _costCenters.GetByCodeAsync(tenantId, "PROJ-DEFAULT", ct);
            if (def != null) costCenterId = def.Id;
        }
        // بعد المحاولات الثلاث، يجب أن يكون لدينا cost center
        if (!costCenterId.HasValue)
            return ProcurementResult<PurchaseOrderResponse>.Fail(
                "يجب تحديد مركز التكلفة. إذا حددت مشروعاً، سيتم اشتقاقه منه تلقائياً.",
                ProcurementErrorCode.BusinessRuleViolation);

        // توليد رقم PO تلقائي
        var poNumber = await _seq.GetNextNumberAsync(tenantId, "PO", ct);

        // حساب المبالغ
        decimal subTotal = 0, taxAmount = 0;
        var lineEntities = new List<PurchaseOrderLine>();
        for (int i = 0; i < req.Lines.Count; i++)
        {
            var l = req.Lines[i];
            var lineSub = l.Quantity * l.UnitPrice;
            var lineTax = lineSub * l.TaxRate;
            subTotal += lineSub;
            taxAmount += lineTax;
            lineEntities.Add(new PurchaseOrderLine
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                ItemId = l.ItemId, Quantity = l.Quantity, UnitPrice = l.UnitPrice,
                TaxRate = l.TaxRate, SubTotal = lineSub, LineOrder = i
            });
        }
        var total = subTotal + taxAmount;

        var now = DateTime.UtcNow;
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            PoNumber = poNumber, VendorId = req.VendorId,
            ProjectId = projectId, CostCenterId = costCenterId,
            Status = PurchaseOrderStatus.Draft,
            OrderDate = req.OrderDate, ExpectedDate = req.ExpectedDate,
            Currency = req.Currency.ToUpperInvariant(),
            SubTotal = subTotal, TaxAmount = taxAmount, TotalAmount = total,
            Notes = req.Notes,
            CreatedAt = now, CreatedBy = userId, UpdatedAt = now, UpdatedBy = userId
        };

        await _pos.InsertAsync(po, ct);
        await _pos.InsertLinesAsync(tenantId, po.Id, lineEntities, ct);
        po.Lines = lineEntities;

        _logger.LogInformation("تم إنشاء PO {PoNumber} بقيمة {Total} للمستأجر {TenantId}", poNumber, total, tenantId);
        return ProcurementResult<PurchaseOrderResponse>.Ok(await MapToResponseAsync(po, ct));
    }

    public async Task<ProcurementResult<PurchaseOrderResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var po = await _pos.GetByIdAsync(id, ct);
        if (po == null || po.TenantId != tenantId)
            return ProcurementResult<PurchaseOrderResponse>.Fail("غير موجود.", ProcurementErrorCode.NotFound);
        return ProcurementResult<PurchaseOrderResponse>.Ok(await MapToResponseAsync(po, ct));
    }

    public async Task<ProcurementResult<IReadOnlyList<PurchaseOrderResponse>>> ListAsync(Guid tenantId, Guid? vendorId, PurchaseOrderStatus? status, int skip, int take, CancellationToken ct)
    {
        if (take is < 1 or > 200) take = 50;
        var list = await _pos.ListAsync(tenantId, vendorId, status, skip, take, ct);
        // اجلب أسماء الموردين + مراكز التكلفة + المشاريع في 3 استعلامات
        var vendorIds = list.Select(p => p.VendorId).Distinct().ToArray();
        var ccIds = list.Where(p => p.CostCenterId.HasValue).Select(p => p.CostCenterId!.Value).Distinct().ToArray();
        var projIds = list.Where(p => p.ProjectId.HasValue).Select(p => p.ProjectId!.Value).Distinct().ToArray();
        var vendorMap = vendorIds.Length > 0 ? (await _vendors.GetByIdsAsync(vendorIds, ct)).ToDictionary(v => v.Id) : new Dictionary<Guid, Entities.Vendor>();
        var ccMap = ccIds.Length > 0 ? (await _costCenters.GetByIdsAsync(ccIds, ct)).ToDictionary(c => c.Id) : new Dictionary<Guid, ERPSystem.Modules.Companies.Entities.CostCenter>();
        var projMap = projIds.Length > 0 ? (await _projects.GetByIdsAsync(projIds, ct)).ToDictionary(p => p.Id) : new Dictionary<Guid, ERPSystem.Modules.Projects.Entities.Project>();

        var resp = new List<PurchaseOrderResponse>();
        foreach (var po in list)
        {
            vendorMap.TryGetValue(po.VendorId, out var v);
            string? projectName = po.ProjectId.HasValue && projMap.TryGetValue(po.ProjectId.Value, out var pr) ? pr.Name : null;
            string? costCenterName = po.CostCenterId.HasValue && ccMap.TryGetValue(po.CostCenterId.Value, out var cc) ? cc.Name : null;
            resp.Add(MapToResponse(po, v?.Name, projectName, costCenterName));
        }
        return ProcurementResult<IReadOnlyList<PurchaseOrderResponse>>.Ok(resp);
    }

    public async Task<ProcurementResult<PurchaseOrderResponse>> ApproveAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        var po = await _pos.GetByIdAsync(id, ct);
        if (po == null || po.TenantId != tenantId)
            return ProcurementResult<PurchaseOrderResponse>.Fail("غير موجود.", ProcurementErrorCode.NotFound);

        // Business rule: يمكن الموافقة فقط من Draft أو Pending
        if (po.Status != PurchaseOrderStatus.Draft && po.Status != PurchaseOrderStatus.Pending)
            return ProcurementResult<PurchaseOrderResponse>.Fail(
                $"لا يمكن الموافقة على PO في حالة {po.Status}.", ProcurementErrorCode.InvalidStatusTransition);

        po.Status = PurchaseOrderStatus.Approved;
        po.ApprovedAt = DateTime.UtcNow;
        po.ApprovedBy = userId;
        po.UpdatedAt = DateTime.UtcNow;
        po.UpdatedBy = userId;
        await _pos.UpdateAsync(po, ct);
        _logger.LogInformation("تمت الموافقة على PO {PoNumber} من المستخدم {UserId}", po.PoNumber, userId);
        return ProcurementResult<PurchaseOrderResponse>.Ok(MapToResponse(po));
    }

    public async Task<ProcurementResult<PurchaseOrderResponse>> SendAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        var po = await _pos.GetByIdAsync(id, ct);
        if (po == null || po.TenantId != tenantId)
            return ProcurementResult<PurchaseOrderResponse>.Fail("غير موجود.", ProcurementErrorCode.NotFound);

        // Business rule: يمكن الإرسال فقط بعد الموافقة
        if (po.Status != PurchaseOrderStatus.Approved)
            return ProcurementResult<PurchaseOrderResponse>.Fail(
                $"لا يمكن إرسال PO في حالة {po.Status} (يجب أن يكون Approved).", ProcurementErrorCode.InvalidStatusTransition);

        po.Status = PurchaseOrderStatus.Sent;
        po.SentAt = DateTime.UtcNow;
        po.UpdatedAt = DateTime.UtcNow;
        po.UpdatedBy = userId;
        await _pos.UpdateAsync(po, ct);
        _logger.LogInformation("تم إرسال PO {PoNumber} للمورّد", po.PoNumber);
        return ProcurementResult<PurchaseOrderResponse>.Ok(MapToResponse(po));
    }

    public async Task<ProcurementResult<bool>> CancelAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        var po = await _pos.GetByIdAsync(id, ct);
        if (po == null || po.TenantId != tenantId)
            return ProcurementResult<bool>.Fail("غير موجود.", ProcurementErrorCode.NotFound);
        if (po.Status == PurchaseOrderStatus.Cancelled)
            return ProcurementResult<bool>.Ok(true); // idempotent
        if (po.Status == PurchaseOrderStatus.Received)
            return ProcurementResult<bool>.Fail("لا يمكن إلغاء PO مُستلَم.", ProcurementErrorCode.InvalidStatusTransition);

        po.Status = PurchaseOrderStatus.Cancelled;
        po.UpdatedAt = DateTime.UtcNow;
        po.UpdatedBy = userId;
        await _pos.UpdateAsync(po, ct);
        _logger.LogInformation("تم إلغاء PO {PoNumber}", po.PoNumber);
        return ProcurementResult<bool>.Ok(true);
    }

    private static PurchaseOrderResponse MapToResponse(PurchaseOrder po, string? vendorName = null, string? projectName = null, string? costCenterName = null) => new()
    {
        Id = po.Id, TenantId = po.TenantId, PoNumber = po.PoNumber, VendorId = po.VendorId,
        VendorName = vendorName,
        ProjectId = po.ProjectId, ProjectName = projectName,
        CostCenterId = po.CostCenterId, CostCenterName = costCenterName,
        Status = po.Status, OrderDate = po.OrderDate, ExpectedDate = po.ExpectedDate,
        Currency = po.Currency, SubTotal = po.SubTotal, TaxAmount = po.TaxAmount, TotalAmount = po.TotalAmount,
        Notes = po.Notes, ApprovedAt = po.ApprovedAt, ApprovedBy = po.ApprovedBy, SentAt = po.SentAt,
        CreatedAt = po.CreatedAt,
        Lines = po.Lines.Select(l => new PurchaseOrderLineResponse
        {
            Id = l.Id, ItemId = l.ItemId, Quantity = l.Quantity, UnitPrice = l.UnitPrice,
            TaxRate = l.TaxRate, SubTotal = l.SubTotal, LineOrder = l.LineOrder
        }).ToList()
    };

    private async Task<PurchaseOrderResponse> MapToResponseAsync(PurchaseOrder po, CancellationToken ct)
    {
        var v = await _vendors.GetByIdAsync(po.VendorId, ct);
        string? projectName = null;
        if (po.ProjectId.HasValue)
        {
            var p = await _projects.GetByIdAsync(po.ProjectId.Value, ct);
            projectName = p?.Name;
        }
        string? costCenterName = null;
        if (po.CostCenterId.HasValue)
        {
            var c = await _costCenters.GetByIdAsync(po.CostCenterId.Value, ct);
            costCenterName = c?.Name;
        }
        return MapToResponse(po, v?.Name, projectName, costCenterName);
    }
}
