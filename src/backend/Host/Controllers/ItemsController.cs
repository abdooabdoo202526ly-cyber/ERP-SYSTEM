using System.Security.Claims;
using ERPSystem.Modules.Inventory.Application;
using ERPSystem.Modules.Inventory.Application.Services;
using ERPSystem.Shared.MultiTenancy;
using ERPSystem.Host.Utilities;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

[ApiController]
[Route("api/inventory/items")]
[Authorize]
public class ItemsController : ControllerBase
{
    private readonly IItemService _service;
    private readonly ITenantContext _tenant;
    private readonly IValidator<CreateItemRequest> _createV;
    private readonly IValidator<UpdateItemRequest> _updateV;
    private readonly ITenantCache _cache;
    private const string CachePrefix = "items";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    public ItemsController(IItemService s, ITenantContext t, IValidator<CreateItemRequest> c, IValidator<UpdateItemRequest> u, ITenantCache cache)
    { _service = s; _tenant = t; _createV = c; _updateV = u; _cache = cache; }
    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();
    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId,
        [FromQuery] Guid? categoryId,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var key = $"t:{TenantId:N}:{CachePrefix}:list:{companyId}:{categoryId}:{includeInactive}:{skip}:{take}";
        var data = await _cache.GetOrCreateAsync(key, async () =>
        {
            var r = await _service.ListAsync(TenantId, companyId, categoryId, includeInactive, skip, take, ct);
            return r.Succeeded ? r.Value : Array.Empty<ItemResponse>();
        }, CacheTtl, ct);
        return Ok(data);
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var key = $"t:{TenantId:N}:{CachePrefix}:{id}";
        var data = await _cache.GetOrCreateAsync(key, async () =>
        {
            var r = await _service.GetByIdAsync(TenantId, id, ct);
            return r.Succeeded ? r.Value : null;
        }, CacheTtl, ct);
        return data is null ? NotFound() : Ok(data);
    }
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateItemRequest req, CancellationToken ct)
    {
        var v = await _createV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _service.CreateAsync(TenantId, UserId, req, ct);
        if (r.Succeeded) _cache.InvalidateTenant(TenantId);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateItemRequest req, CancellationToken ct)
    {
        var v = await _updateV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _service.UpdateAsync(TenantId, UserId, id, req, ct);
        if (r.Succeeded) _cache.InvalidateTenant(TenantId);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var r = await _service.DeactivateAsync(TenantId, UserId, id, ct);
        if (r.Succeeded) _cache.InvalidateTenant(TenantId);
        return r.Succeeded ? NoContent() : BadRequest(Problem(r));
    }

    private static ValidationProblemDetails ValidationProblem(FluentValidation.Results.ValidationResult v) =>
        new(v.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    private static ProblemDetails Problem<T>(InventoryResult<T> r) => new()
    {
        Title = "Inventory Error", Status = StatusCodes.Status400BadRequest, Detail = r.Error
    };
}
