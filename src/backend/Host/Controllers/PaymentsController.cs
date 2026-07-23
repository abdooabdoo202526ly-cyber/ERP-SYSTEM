using System.Security.Claims;
using ERPSystem.Modules.Payments.Application;
using ERPSystem.Modules.Payments.Application.Services;
using ERPSystem.Modules.Payments.Entities;
using ERPSystem.Shared.MultiTenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

/// <summary>
/// Payments API — سندات الدفع (AP + AR).
/// يتبع نفس نمط ProcurementController: TenantId من ITenantContext، UserId من JWT claims،
/// PaymentResult&lt;T&gt; + FluentValidation في entry point.
/// </summary>
[ApiController]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _payments;
    private readonly ITenantContext _tenant;
    private readonly IValidator<CreatePaymentRequest> _createV;
    private readonly IValidator<AllocatePaymentRequest> _allocV;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IPaymentService payments, ITenantContext tenant,
        IValidator<CreatePaymentRequest> createV,
        IValidator<AllocatePaymentRequest> allocV,
        ILogger<PaymentsController> logger)
    {
        _payments = payments; _tenant = tenant; _createV = createV; _allocV = allocV; _logger = logger;
    }

    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();
    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value);

    [HttpGet("api/payments")]
    public async Task<IActionResult> List(
        [FromQuery] string? partyType, [FromQuery] Guid? partyId, [FromQuery] PaymentStatus? status,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        try
        {
            var r = await _payments.ListAsync(TenantId, partyType, partyId, status, skip, take, ct);
            return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payments list failed | tenantId={TenantId} partyType={PartyType}", TenantId, partyType);
            return StatusCode(500, new { error = "PaymentsListFailed", message = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    [HttpGet("api/payments/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var r = await _payments.GetByIdAsync(TenantId, id, ct);
            return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payments getById failed | id={Id}", id);
            return StatusCode(500, new { error = "PaymentsGetFailed", message = ex.Message });
        }
    }

    [HttpPost("api/payments")]
    public async Task<IActionResult> Create([FromBody] CreatePaymentRequest req, CancellationToken ct)
    {
        var v = await _createV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _payments.CreateAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }

    [HttpPost("api/payments/{id:guid}/post")]
    public async Task<IActionResult> Post(Guid id, CancellationToken ct)
    {
        var r = await _payments.PostAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpPost("api/payments/{id:guid}/allocate")]
    public async Task<IActionResult> Allocate(Guid id, [FromBody] AllocatePaymentRequest req, CancellationToken ct)
    {
        var v = await _allocV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _payments.AllocateAsync(TenantId, UserId, id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    private static ValidationProblemDetails ValidationProblem(FluentValidation.Results.ValidationResult v) =>
        new(v.Errors.GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));

    private static ProblemDetails Problem<T>(PaymentResult<T> r) => new()
    {
        Title = "Payment Error",
        Status = StatusCodes.Status400BadRequest,
        Detail = r.Error,
    };
}
