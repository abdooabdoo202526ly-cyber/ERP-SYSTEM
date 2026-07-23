using System.Security.Claims;
using ERPSystem.Modules.Inventory.Application;
using ERPSystem.Modules.Inventory.Application.Services;
using ERPSystem.Shared.MultiTenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

[ApiController]
[Route("api/inventory/warehouses")]
[Authorize]
public class WarehousesController : ControllerBase
{
    private readonly IWarehouseService _service;
    private readonly ITenantContext _tenant;
    private readonly IValidator<CreateWarehouseRequest> _createV;
    public WarehousesController(IWarehouseService s, ITenantContext t, IValidator<CreateWarehouseRequest> c)
    { _service = s; _tenant = t; _createV = c; }
    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();
    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? companyId, [FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var r = await _service.ListAsync(TenantId, companyId, includeInactive, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var r = await _service.GetByIdAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWarehouseRequest req, CancellationToken ct)
    {
        var v = await _createV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _service.CreateAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWarehouseRequest req, CancellationToken ct)
    {
        var r = await _service.UpdateAsync(TenantId, UserId, id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var r = await _service.DeactivateAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? NoContent() : BadRequest(Problem(r));
    }

    private static ValidationProblemDetails ValidationProblem(FluentValidation.Results.ValidationResult v) =>
        new(v.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    private static ProblemDetails Problem<T>(InventoryResult<T> r) => new()
    { Title = "Warehouse Error", Status = StatusCodes.Status400BadRequest, Detail = r.Error };
}
