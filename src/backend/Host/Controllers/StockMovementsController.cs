using System.Security.Claims;
using ERPSystem.Modules.Inventory.Application;
using ERPSystem.Modules.Inventory.Application.Services;
using ERPSystem.Modules.Inventory.Entities;
using ERPSystem.Shared.MultiTenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

[ApiController]
[Route("api/inventory/movements")]
[Authorize]
public class StockMovementsController : ControllerBase
{
    private readonly IStockMovementService _service;
    private readonly ITenantContext _tenant;
    private readonly IValidator<ReceiveStockRequest> _recvV;
    private readonly IValidator<IssueStockRequest> _issueV;
    private readonly IValidator<TransferStockRequest> _trfV;
    private readonly IValidator<AdjustStockRequest> _adjV;
    public StockMovementsController(
        IStockMovementService s, ITenantContext t,
        IValidator<ReceiveStockRequest> r, IValidator<IssueStockRequest> i,
        IValidator<TransferStockRequest> tr, IValidator<AdjustStockRequest> a)
    { _service = s; _tenant = t; _recvV = r; _issueV = i; _trfV = tr; _adjV = a; }
    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();
    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId,
        [FromQuery] StockMovementType? type,
        [FromQuery] StockMovementStatus? status,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var r = await _service.ListAsync(TenantId, companyId, type, status, skip, take, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var r = await _service.GetByIdAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }
    [HttpPost("receive")]
    public async Task<IActionResult> Receive([FromBody] ReceiveStockRequest req, CancellationToken ct)
    {
        var v = await _recvV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _service.CreateReceiveAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }
    [HttpPost("issue")]
    public async Task<IActionResult> Issue([FromBody] IssueStockRequest req, CancellationToken ct)
    {
        var v = await _issueV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _service.CreateIssueAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }
    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferStockRequest req, CancellationToken ct)
    {
        var v = await _trfV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _service.CreateTransferAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }
    [HttpPost("adjust")]
    public async Task<IActionResult> Adjust([FromBody] AdjustStockRequest req, CancellationToken ct)
    {
        var v = await _adjV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _service.CreateAdjustAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }
    [HttpPost("{id:guid}/post")]
    public async Task<IActionResult> Post(Guid id, CancellationToken ct)
    {
        var r = await _service.PostAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }
    [HttpPost("{id:guid}/reverse")]
    public async Task<IActionResult> Reverse(Guid id, [FromBody] ReverseRequest? body, CancellationToken ct)
    {
        var r = await _service.ReverseAsync(TenantId, UserId, id, body?.Reason, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    private static ValidationProblemDetails ValidationProblem(FluentValidation.Results.ValidationResult v) =>
        new(v.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    private static ProblemDetails Problem<T>(StockMovementResult<T> r) => new()
    { Title = "Stock Error", Status = StatusCodes.Status400BadRequest, Detail = r.Error };
}
public sealed class ReverseRequest { public string? Reason { get; set; } }
