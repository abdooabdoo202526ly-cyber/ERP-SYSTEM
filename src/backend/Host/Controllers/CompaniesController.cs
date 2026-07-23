using ERPSystem.Modules.Companies.Application.Services;
using ERPSystem.Shared.MultiTenancy;
using ERPSystem.Host.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

[ApiController]
[Route("api/companies")]
[Authorize]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _service;
    private readonly ITenantContext _tenant;
    private readonly ITenantCache _cache;
    private const string CachePrefix = "companies";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);
    public CompaniesController(ICompanyService service, ITenantContext tenant, ITenantCache cache)
    { _service = service; _tenant = tenant; _cache = cache; }
    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var key = $"t:{TenantId:N}:{CachePrefix}:all:{includeInactive}";
        var data = await _cache.GetOrCreateAsync(key, async () =>
        {
            var r = await _service.ListAsync(TenantId, includeInactive, ct);
            return r.Succeeded ? r.Value : (IReadOnlyList<ERPSystem.Modules.Companies.Entities.Company>)Array.Empty<ERPSystem.Modules.Companies.Entities.Company>();
        }, CacheTtl, ct);
        return Ok(data);
    }

    [HttpGet("tree")]
    public async Task<IActionResult> Tree(CancellationToken ct = default)
    {
        var key = $"t:{TenantId:N}:{CachePrefix}:tree";
        var data = await _cache.GetOrCreateAsync<ERPSystem.Modules.Companies.Application.Services.CompanyTreeNode>(key, async () =>
        {
            var r = await _service.GetTreeAsync(TenantId, ct);
            return r.Succeeded ? r.Value : new ERPSystem.Modules.Companies.Application.Services.CompanyTreeNode();
        }, CacheTtl, ct);
        return Ok(data);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var key = $"t:{TenantId:N}:{CachePrefix}:{id}";
        var data = await _cache.GetOrCreateAsync<ERPSystem.Modules.Companies.Entities.Company?>(key, async () =>
        {
            var r = await _service.GetByIdAsync(TenantId, id, ct);
            return r.Succeeded ? r.Value : null;
        }, CacheTtl, ct);
        return data is null ? NotFound() : Ok(data);
    }

    [HttpGet("{id:guid}/subsidiaries")]
    public async Task<IActionResult> GetSubsidiaries(Guid id, CancellationToken ct)
    {
        var r = await _service.GetSubsidiariesAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpPost("holding")]
    public async Task<IActionResult> CreateHolding([FromBody] CreateHoldingRequest req, CancellationToken ct)
    {
        var r = await _service.CreateHoldingAsync(TenantId, req.Code, req.Name, req.LegalName ?? req.Name, req.BaseCurrency, ct);
        if (r.Succeeded) _cache.InvalidateTenant(TenantId);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }

    [HttpPost("subsidiary")]
    public async Task<IActionResult> AddSubsidiary([FromBody] AddSubsidiaryRequest req, CancellationToken ct)
    {
        var r = await _service.AddSubsidiaryAsync(TenantId, req.ParentCompanyId, req.Code, req.Name, req.LegalName, ct);
        if (r.Succeeded) _cache.InvalidateTenant(TenantId);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCompanyRequest req, CancellationToken ct)
    {
        var r = await _service.UpdateAsync(TenantId, id, req.Name, req.LegalName, req.TaxId, req.Phone, req.Email, req.Address, req.IsActive, ct);
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

    private static ProblemDetails Problem<T>(CompanyResult<T> r) => new()
    {
        Title = "Company Error",
        Status = StatusCodes.Status400BadRequest,
        Detail = r.Error,
    };
}

public sealed class CreateHoldingRequest
{
    public string Code { get; set; } = "000";
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string BaseCurrency { get; set; } = "LYD";
}

public sealed class AddSubsidiaryRequest
{
    public Guid ParentCompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
}

// v1.0.10: edit support
public sealed class UpdateCompanyRequest
{
    public string? Name { get; set; }
    public string? LegalName { get; set; }
    public string? TaxId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool? IsActive { get; set; }
}
