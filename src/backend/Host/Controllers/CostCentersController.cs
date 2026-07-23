using ERPSystem.Modules.Companies.Application.Services;
using ERPSystem.Modules.Companies.Entities;
using ERPSystem.Shared.MultiTenancy;
using ERPSystem.Host.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

[ApiController]
[Route("api/cost-centers")]
[Authorize]
public class CostCentersController : ControllerBase
{
    private readonly ICostCenterService _service;
    private readonly ITenantContext _tenant;
    private readonly ITenantCache _cache;
    private const string CachePrefix = "costcenters";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);
    public CostCentersController(ICostCenterService service, ITenantContext tenant, ITenantCache cache)
    { _service = service; _tenant = tenant; _cache = cache; }
    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId,
        [FromQuery] CostCenterType? type,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var key = $"t:{TenantId:N}:{CachePrefix}:all:{companyId}:{type}:{includeInactive}";
        var data = await _cache.GetOrCreateAsync(key, async () =>
        {
            var r = await _service.ListAsync(TenantId, companyId, type, includeInactive, ct);
            return r.Succeeded ? r.Value : (IReadOnlyList<ERPSystem.Modules.Companies.Entities.CostCenter>)Array.Empty<ERPSystem.Modules.Companies.Entities.CostCenter>();
        }, CacheTtl, ct);
        return Ok(data);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var key = $"t:{TenantId:N}:{CachePrefix}:{id}";
        var data = await _cache.GetOrCreateAsync<ERPSystem.Modules.Companies.Entities.CostCenter?>(key, async () =>
        {
            var r = await _service.GetByIdAsync(TenantId, id, ct);
            return r.Succeeded ? r.Value : null;
        }, CacheTtl, ct);
        return data is null ? NotFound() : Ok(data);
    }

    [HttpGet("{id:guid}/children")]
    public async Task<IActionResult> GetChildren(Guid id, CancellationToken ct)
    {
        var r = await _service.GetChildrenAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpGet("{id:guid}/budget-status")]
    public async Task<IActionResult> BudgetStatus(Guid id, [FromQuery] DateTime? asOf, CancellationToken ct)
    {
        var r = await _service.GetBudgetStatusAsync(TenantId, id, asOf, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCostCenterRequest req, CancellationToken ct)
    {
        var r = await _service.CreateAsync(TenantId, req, ct);
        if (r.Succeeded) _cache.InvalidateTenant(TenantId);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateCostCenterRequest req, CancellationToken ct)
    {
        var r = await _service.UpdateAsync(TenantId, id, req, ct);
        if (r.Succeeded) _cache.InvalidateTenant(TenantId);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var r = await _service.DeactivateAsync(TenantId, id, ct);
        if (r.Succeeded) _cache.InvalidateTenant(TenantId);
        return r.Succeeded ? NoContent() : BadRequest(Problem(r));
    }

    private static ProblemDetails Problem<T>(CostCenterResult<T> r) => new()
    {
        Title = "CostCenter Error",
        Status = StatusCodes.Status400BadRequest,
        Detail = r.Error,
    };
}
