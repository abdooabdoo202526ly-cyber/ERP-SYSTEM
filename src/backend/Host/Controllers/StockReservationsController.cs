using ERPSystem.Modules.Inventory.Application;
using ERPSystem.Modules.Inventory.Application.Services;
using ERPSystem.Shared.MultiTenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ERPSystem.Host.Controllers;

[ApiController]
[Route("api/inventory/reservations")]
[Authorize]
public class StockReservationsController : ControllerBase
{
    private readonly IStockReservationService _service;
    private readonly ITenantContext _tenant;
    private readonly IValidator<CreateReservationRequest> _createV;
    public StockReservationsController(IStockReservationService s, ITenantContext t, IValidator<CreateReservationRequest> c)
    { _service = s; _tenant = t; _createV = c; }
    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();
    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? itemId, [FromQuery] Guid? warehouseId, CancellationToken ct = default)
    {
        var r = await _service.ListAsync(TenantId, itemId, warehouseId, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReservationRequest req, CancellationToken ct)
    {
        var v = await _createV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _service.CreateAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(List), new { }, r.Value)
            : BadRequest(Problem(r));
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Release(Guid id, CancellationToken ct)
    {
        var r = await _service.ReleaseAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? NoContent() : BadRequest(Problem(r));
    }

    private static ValidationProblemDetails ValidationProblem(FluentValidation.Results.ValidationResult v) =>
        new(v.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    private static ProblemDetails Problem<T>(StockMovementResult<T> r) => new()
    { Title = "Reservation Error", Status = StatusCodes.Status400BadRequest, Detail = r.Error };
}
