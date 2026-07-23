using System.Security.Claims;
using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Application.Services;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Shared.MultiTenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

[ApiController]
[Route("api/finance/posting-rules")]
[Authorize]
public class PostingRulesController : ControllerBase
{
    private readonly IPostingRulesService _service;
    private readonly IValidator<CreatePostingRuleRequest> _validator;
    private readonly ITenantContext _tenantContext;

    public PostingRulesController(
        IPostingRulesService service,
        IValidator<CreatePostingRuleRequest> validator,
        ITenantContext tenantContext)
    {
        _service = service;
        _validator = validator;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!_tenantContext.IsResolved) return Unauthorized();
        var r = await _service.ListAsync(_tenantContext.TenantId!.Value, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!_tenantContext.IsResolved) return Unauthorized();
        var r = await _service.GetByIdAsync(_tenantContext.TenantId!.Value, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreatePostingRuleRequest request, CancellationToken ct)
    {
        if (!_tenantContext.IsResolved) return Unauthorized();
        var v = await _validator.ValidateAsync(request, ct);
        if (!v.IsValid) return ValidationProblem(new ValidationProblemDetails(
            v.Errors.GroupBy(e => e.PropertyName).ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray())));
        var r = await _service.UpdateAsync(_tenantContext.TenantId!.Value, id, request, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePostingRuleRequest request, CancellationToken ct)
    {
        if (!_tenantContext.IsResolved) return Unauthorized();
        var v = await _validator.ValidateAsync(request, ct);
        if (!v.IsValid) return ValidationProblem(new ValidationProblemDetails(
            v.Errors.GroupBy(e => e.PropertyName).ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray())));

        var r = await _service.CreateAsync(_tenantContext.TenantId!.Value, request, ct);
        return r.Succeeded ? CreatedAtAction(nameof(List), new { }, r.Value) : BadRequest(Problem(r));
    }

    /// <summary>محاكاة/تشغيل حدث على القواعد (للاختبار والتجريب)</summary>
    [HttpPost("trigger/{eventType}")]
    public async Task<IActionResult> Trigger(TriggeringEvent eventType, [FromBody] EventPayload payload, CancellationToken ct)
    {
        if (!_tenantContext.IsResolved) return Unauthorized();
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized();

        var count = await _service.ApplyRulesAsync(_tenantContext.TenantId!.Value, uid, eventType, payload, ct);
        return Ok(new { eventType, entriesCreated = count });
    }

    private static ProblemDetails Problem<T>(FinanceResult<T> r) => new()
    {
        Title = "Posting Rule Error",
        Status = StatusCodes.Status400BadRequest,
        Detail = r.Error,
    };
}
