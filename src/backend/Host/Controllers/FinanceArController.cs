using System.Security.Claims;
using ERPSystem.Modules.AccountsReceivable.Application;
using ERPSystem.Modules.AccountsReceivable.Application.Services;
using ERPSystem.Modules.AccountsReceivable.Entities;
using ERPSystem.Shared.MultiTenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

/// <summary>
/// AR API — customers, sales invoices, receipts, aging report.
/// يتبع نفس نمط ProcurementController: TenantId من ITenantContext، UserId من JWT claims،
/// Result pattern عبر ArResult&lt;T&gt;، و FluentValidation في الـ entry point.
/// </summary>
[ApiController]
[Authorize]
public class FinanceArController : ControllerBase
{
    private readonly ICustomerService _customers;
    private readonly ISalesInvoiceService _invoices;
    private readonly IReceiptService _receipts;
    private readonly ITenantContext _tenant;

    private readonly IValidator<CreateCustomerRequest> _createCustomerV;
    private readonly IValidator<UpdateCustomerRequest> _updateCustomerV;
    private readonly IValidator<CreateSalesInvoiceRequest> _createInvoiceV;
    private readonly IValidator<UpdateSalesInvoiceRequest> _updateInvoiceV;
    private readonly IValidator<CreateReceiptRequest> _createReceiptV;

    public FinanceArController(
        ICustomerService customers,
        ISalesInvoiceService invoices,
        IReceiptService receipts,
        ITenantContext tenant,
        IValidator<CreateCustomerRequest> createCustomerV,
        IValidator<UpdateCustomerRequest> updateCustomerV,
        IValidator<CreateSalesInvoiceRequest> createInvoiceV,
        IValidator<UpdateSalesInvoiceRequest> updateInvoiceV,
        IValidator<CreateReceiptRequest> createReceiptV)
    {
        _customers = customers; _invoices = invoices; _receipts = receipts; _tenant = tenant;
        _createCustomerV = createCustomerV; _updateCustomerV = updateCustomerV;
        _createInvoiceV = createInvoiceV; _updateInvoiceV = updateInvoiceV; _createReceiptV = createReceiptV;
    }

    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();
    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value);

    // ============== Customers ==============

    [HttpGet("api/ar/customers")]
    public async Task<IActionResult> ListCustomers(
        [FromQuery] bool includeInactive = false,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var r = await _customers.ListAsync(TenantId, includeInactive, skip, take, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpGet("api/ar/customers/{id:guid}")]
    public async Task<IActionResult> GetCustomer(Guid id, CancellationToken ct)
    {
        var r = await _customers.GetByIdAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }

    [HttpPost("api/ar/customers")]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest req, CancellationToken ct)
    {
        var v = await _createCustomerV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _customers.CreateAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetCustomer), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }

    [HttpPut("api/ar/customers/{id:guid}")]
    public async Task<IActionResult> UpdateCustomer(Guid id, [FromBody] UpdateCustomerRequest req, CancellationToken ct)
    {
        var v = await _updateCustomerV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _customers.UpdateAsync(TenantId, UserId, id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpDelete("api/ar/customers/{id:guid}")]
    public async Task<IActionResult> DeactivateCustomer(Guid id, CancellationToken ct)
    {
        var r = await _customers.DeactivateAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? NoContent() : BadRequest(Problem(r));
    }

    // ============== Sales Invoices ==============

    [HttpGet("api/ar/sales-invoices")]
    public async Task<IActionResult> ListInvoices(
        [FromQuery] Guid? customerId, [FromQuery] SalesInvoiceStatus? status,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var r = await _invoices.ListAsync(TenantId, customerId, status, skip, take, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpGet("api/ar/sales-invoices/{id:guid}")]
    public async Task<IActionResult> GetInvoice(Guid id, CancellationToken ct)
    {
        var r = await _invoices.GetByIdAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }

    [HttpPost("api/ar/sales-invoices")]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateSalesInvoiceRequest req, CancellationToken ct)
    {
        var v = await _createInvoiceV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _invoices.CreateAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetInvoice), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }

    [HttpPut("api/ar/sales-invoices/{id:guid}")]
    public async Task<IActionResult> UpdateInvoice(Guid id, [FromBody] UpdateSalesInvoiceRequest req, CancellationToken ct)
    {
        var v = await _updateInvoiceV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _invoices.UpdateAsync(TenantId, UserId, id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpPut("api/ar/sales-invoices/{id:guid}/post")]
    public async Task<IActionResult> PostInvoice(Guid id, CancellationToken ct)
    {
        var r = await _invoices.PostAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpPut("api/ar/sales-invoices/{id:guid}/cancel")]
    public async Task<IActionResult> CancelInvoice(Guid id, CancellationToken ct)
    {
        var r = await _invoices.CancelAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    // ============== Receipts ==============

    [HttpGet("api/ar/receipts")]
    public async Task<IActionResult> ListReceipts(
        [FromQuery] Guid? customerId,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var r = await _receipts.ListAsync(TenantId, customerId, skip, take, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpGet("api/ar/receipts/{id:guid}")]
    public async Task<IActionResult> GetReceipt(Guid id, CancellationToken ct)
    {
        var r = await _receipts.GetByIdAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }

    [HttpPost("api/ar/receipts")]
    public async Task<IActionResult> CreateReceipt([FromBody] CreateReceiptRequest req, CancellationToken ct)
    {
        var v = await _createReceiptV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _receipts.CreateAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetReceipt), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }

    [HttpPut("api/ar/receipts/{id:guid}/post")]
    public async Task<IActionResult> PostReceipt(Guid id, CancellationToken ct)
    {
        var r = await _receipts.PostAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpPut("api/ar/receipts/{id:guid}/reverse")]
    public async Task<IActionResult> ReverseReceipt(Guid id, CancellationToken ct)
    {
        var r = await _receipts.ReverseAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    // ============== Aging ==============

    [HttpGet("api/ar/aging")]
    public async Task<IActionResult> GetAging([FromQuery] DateTime? asOfDate, CancellationToken ct = default)
    {
        var asOf = asOfDate ?? DateTime.UtcNow;
        var r = await _invoices.GetAgingReportAsync(TenantId, asOf, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    // ============== Helpers ==============

    private static ValidationProblemDetails ValidationProblem(FluentValidation.Results.ValidationResult v) =>
        new(v.Errors.GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    private static ProblemDetails Problem<T>(ArResult<T> r) => new()
    {
        Title = "AR Error",
        Status = StatusCodes.Status400BadRequest,
        Detail = r.Error,
    };
}
