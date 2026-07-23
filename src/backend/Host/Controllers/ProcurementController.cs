using System.Security.Claims;
using ERPSystem.Modules.Procurement.Application;
using ERPSystem.Modules.Procurement.Application.Services;
using ERPSystem.Modules.Procurement.Entities;
using ERPSystem.Shared.MultiTenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

/// <summary>
/// Procurement API — vendors, POs, GRs, Bills.
/// يتبع نفس نمط ItemsController/ProjectsController: TenantId من ITenantContext، UserId من JWT claims،
/// Result pattern عبر ProcurementResult&lt;T&gt;، و FluentValidation في الـ entry point.
/// </summary>
[ApiController]
[Authorize]
public class ProcurementController : ControllerBase
{
    private readonly IVendorService _vendors;
    private readonly IPurchaseOrderService _pos;
    private readonly IGoodsReceiptService _grs;
    private readonly IVendorBillService _bills;
    private readonly ITenantContext _tenant;

    private readonly IValidator<CreateVendorRequest> _createVendorV;
    private readonly IValidator<UpdateVendorRequest> _updateVendorV;
    private readonly IValidator<CreatePurchaseOrderRequest> _createPoV;
    private readonly IValidator<CreateGoodsReceiptRequest> _createGrV;
    private readonly IValidator<CreateVendorBillRequest> _createBillV;

    public ProcurementController(
        IVendorService vendors, IPurchaseOrderService pos, IGoodsReceiptService grs, IVendorBillService bills,
        ITenantContext tenant,
        IValidator<CreateVendorRequest> createVendorV, IValidator<UpdateVendorRequest> updateVendorV,
        IValidator<CreatePurchaseOrderRequest> createPoV,
        IValidator<CreateGoodsReceiptRequest> createGrV, IValidator<CreateVendorBillRequest> createBillV)
    {
        _vendors = vendors; _pos = pos; _grs = grs; _bills = bills; _tenant = tenant;
        _createVendorV = createVendorV; _updateVendorV = updateVendorV; _createPoV = createPoV;
        _createGrV = createGrV; _createBillV = createBillV;
    }

    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();
    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value);

    // ============== Vendors ==============

    [HttpGet("api/procurement/vendors")]
    public async Task<IActionResult> ListVendors(
        [FromQuery] bool includeInactive = false,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var r = await _vendors.ListAsync(TenantId, includeInactive, skip, take, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpGet("api/procurement/vendors/{id:guid}")]
    public async Task<IActionResult> GetVendor(Guid id, CancellationToken ct)
    {
        var r = await _vendors.GetByIdAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }

    [HttpPost("api/procurement/vendors")]
    public async Task<IActionResult> CreateVendor([FromBody] CreateVendorRequest req, CancellationToken ct)
    {
        var v = await _createVendorV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _vendors.CreateAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetVendor), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }

    [HttpPut("api/procurement/vendors/{id:guid}")]
    public async Task<IActionResult> UpdateVendor(Guid id, [FromBody] UpdateVendorRequest req, CancellationToken ct)
    {
        var v = await _updateVendorV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _vendors.UpdateAsync(TenantId, UserId, id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpDelete("api/procurement/vendors/{id:guid}")]
    public async Task<IActionResult> DeactivateVendor(Guid id, CancellationToken ct)
    {
        var r = await _vendors.DeactivateAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? NoContent() : BadRequest(Problem(r));
    }

    // ============== Purchase Orders ==============

    [HttpGet("api/procurement/pos")]
    public async Task<IActionResult> ListPOs(
        [FromQuery] Guid? vendorId, [FromQuery] PurchaseOrderStatus? status,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var r = await _pos.ListAsync(TenantId, vendorId, status, skip, take, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpGet("api/procurement/pos/{id:guid}")]
    public async Task<IActionResult> GetPO(Guid id, CancellationToken ct)
    {
        var r = await _pos.GetByIdAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }

    [HttpPost("api/procurement/pos")]
    public async Task<IActionResult> CreatePO([FromBody] CreatePurchaseOrderRequest req, CancellationToken ct)
    {
        var v = await _createPoV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _pos.CreateAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetPO), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }

    [HttpPut("api/procurement/pos/{id:guid}/approve")]
    public async Task<IActionResult> ApprovePO(Guid id, CancellationToken ct)
    {
        var r = await _pos.ApproveAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpPut("api/procurement/pos/{id:guid}/send")]
    public async Task<IActionResult> SendPO(Guid id, CancellationToken ct)
    {
        var r = await _pos.SendAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpPut("api/procurement/pos/{id:guid}/cancel")]
    public async Task<IActionResult> CancelPO(Guid id, CancellationToken ct)
    {
        var r = await _pos.CancelAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    // ============== Goods Receipts ==============

    [HttpGet("api/procurement/grs")]
    public async Task<IActionResult> ListGRs(
        [FromQuery] Guid? poId, [FromQuery] GoodsReceiptStatus? status,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var r = await _grs.ListAsync(TenantId, poId, status, skip, take, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpGet("api/procurement/grs/{id:guid}")]
    public async Task<IActionResult> GetGR(Guid id, CancellationToken ct)
    {
        var r = await _grs.GetByIdAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }

    [HttpPost("api/procurement/grs")]
    public async Task<IActionResult> CreateGR([FromBody] CreateGoodsReceiptRequest req, CancellationToken ct)
    {
        var v = await _createGrV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _grs.CreateAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetGR), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }

    [HttpPut("api/procurement/grs/{id:guid}/receive")]
    public async Task<IActionResult> ReceiveGR(Guid id, CancellationToken ct)
    {
        var r = await _grs.ReceiveAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpPut("api/procurement/grs/{id:guid}/cancel")]
    public async Task<IActionResult> CancelGR(Guid id, CancellationToken ct)
    {
        var r = await _grs.CancelAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    // ============== Vendor Bills ==============

    [HttpGet("api/procurement/bills")]
    public async Task<IActionResult> ListBills(
        [FromQuery] Guid? vendorId, [FromQuery] Guid? grId, [FromQuery] VendorBillStatus? status,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var r = await _bills.ListAsync(TenantId, vendorId, grId, status, skip, take, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpGet("api/procurement/bills/{id:guid}")]
    public async Task<IActionResult> GetBill(Guid id, CancellationToken ct)
    {
        var r = await _bills.GetByIdAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }

    [HttpPost("api/procurement/bills")]
    public async Task<IActionResult> CreateBill([FromBody] CreateVendorBillRequest req, CancellationToken ct)
    {
        var v = await _createBillV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _bills.CreateAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetBill), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }

    [HttpPut("api/procurement/bills/{id:guid}/post")]
    public async Task<IActionResult> PostBill(Guid id, CancellationToken ct)
    {
        var r = await _bills.PostAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    // ============== Helpers ==============

    private static ValidationProblemDetails ValidationProblem(FluentValidation.Results.ValidationResult v) =>
        new(v.Errors.GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    private static ProblemDetails Problem<T>(ProcurementResult<T> r) => new()
    {
        Title = "Procurement Error",
        Status = StatusCodes.Status400BadRequest,
        Detail = r.Error,
    };
}
