using ERPSystem.Modules.Inventory.Application;
using ERPSystem.Modules.Inventory.Application.Services;
using ERPSystem.Shared.MultiTenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

[ApiController]
[Route("api/inventory/categories")]
[Authorize]
public class ItemCategoriesController : ControllerBase
{
    private readonly IItemCategoryService _service;
    private readonly ITenantContext _tenant;
    private readonly IValidator<CreateItemCategoryRequest> _createV;
    public ItemCategoriesController(IItemCategoryService s, ITenantContext t, IValidator<CreateItemCategoryRequest> c)
    { _service = s; _tenant = t; _createV = c; }
    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var r = await _service.ListAsync(TenantId, includeInactive, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var r = await _service.GetByIdAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }
    [HttpGet("{id:guid}/children")]
    public async Task<IActionResult> GetChildren(Guid id, CancellationToken ct)
    {
        var r = await _service.GetChildrenAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateItemCategoryRequest req, CancellationToken ct)
    {
        var v = await _createV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _service.CreateAsync(TenantId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateItemCategoryRequest req, CancellationToken ct)
    {
        var r = await _service.UpdateAsync(TenantId, id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    // v1.0.24: soft-delete (set IsActive=false)
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var r = await _service.DeactivateAsync(TenantId, id, ct);
        return r.Succeeded ? NoContent() : BadRequest(Problem(r));
    }

    private static ValidationProblemDetails ValidationProblem(FluentValidation.Results.ValidationResult v) =>
        new(v.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    private static ProblemDetails Problem<T>(InventoryResult<T> r) => new()
    { Title = "Category Error", Status = StatusCodes.Status400BadRequest, Detail = r.Error };
}
