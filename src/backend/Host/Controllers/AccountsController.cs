using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Application.Services;
using ERPSystem.Shared.MultiTenancy;
using ERPSystem.Host.Utilities;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

[ApiController]
[Route("api/finance/accounts")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IChartOfAccountsService _service;
    private readonly ITenantContext _tenantContext;
    private readonly IValidator<CreateAccountRequest> _validator;
    private readonly ITenantCache _cache;
    private const string CachePrefix = "accounts";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public AccountsController(
        IChartOfAccountsService service,
        ITenantContext tenantContext,
        IValidator<CreateAccountRequest> validator,
        ITenantCache cache)
    {
        _service = service;
        _tenantContext = tenantContext;
        _validator = validator;
        _cache = cache;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AccountResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        if (!_tenantContext.IsResolved) return Unauthorized();
        var tid = _tenantContext.TenantId!.Value;
        var key = $"t:{tid:N}:{CachePrefix}:all:{includeInactive}";
        var data = await _cache.GetOrCreateAsync(key, async () =>
        {
            var r = await _service.ListAsync(tid, includeInactive, ct);
            return r.Succeeded ? r.Value : Array.Empty<AccountResponse>();
        }, CacheTtl, ct);
        return Ok(data);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!_tenantContext.IsResolved) return Unauthorized();
        var tid = _tenantContext.TenantId!.Value;
        var key = $"t:{tid:N}:{CachePrefix}:{id}";
        var data = await _cache.GetOrCreateAsync(key, async () =>
        {
            var r = await _service.GetByIdAsync(tid, id, ct);
            return r.Succeeded ? r.Value : null;
        }, CacheTtl, ct);
        return data is null ? NotFound() : Ok(data);
    }

    [HttpGet("by-code/{code}")]
    public async Task<IActionResult> GetByCode(string code, CancellationToken ct)
    {
        if (!_tenantContext.IsResolved) return Unauthorized();
        var r = await _service.GetByCodeAsync(_tenantContext.TenantId!.Value, code, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }

    [HttpPost]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request, CancellationToken ct)
    {
        if (!_tenantContext.IsResolved) return Unauthorized();
        var v = await _validator.ValidateAsync(request, ct);
        if (!v.IsValid) return ValidationProblem(new ValidationProblemDetails(
            v.Errors.GroupBy(e => e.PropertyName).ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray())));

        var r = await _service.CreateAsync(_tenantContext.TenantId!.Value, request, ct);
        if (r.Succeeded)
        {
            _cache.InvalidateTenant(_tenantContext.TenantId!.Value);
            return CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, r.Value);
        }
        return BadRequest(Problem(r));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccountRequest request, CancellationToken ct)
    {
        if (!_tenantContext.IsResolved) return Unauthorized();
        var r = await _service.UpdateAsync(_tenantContext.TenantId!.Value, id, request, ct);
        if (r.Succeeded)
        {
            _cache.InvalidateTenant(_tenantContext.TenantId!.Value);
            return Ok(r.Value);
        }
        return BadRequest(Problem(r));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!_tenantContext.IsResolved) return Unauthorized();
        var r = await _service.DeleteAsync(_tenantContext.TenantId!.Value, id, ct);
        if (r.Succeeded)
        {
            _cache.InvalidateTenant(_tenantContext.TenantId!.Value);
            return NoContent();
        }
        return BadRequest(Problem(r));
    }

    private static ProblemDetails Problem<T>(FinanceResult<T> r) => new()
    {
        Title = "Finance Error",
        Status = StatusCodes.Status400BadRequest,
        Detail = r.Error,
    };
}
