using ERPSystem.Modules.Projects.Application;
using ERPSystem.Modules.Projects.Application.Services;
using ERPSystem.Shared.MultiTenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

[ApiController]
[Route("api/resources")]
[Authorize]
public class ResourcesController : ControllerBase
{
    private readonly IResourceService _service;
    private readonly ITenantContext _tenant;
    private readonly IValidator<CreateResourceRequest> _createV;
    public ResourcesController(IResourceService s, ITenantContext t, IValidator<CreateResourceRequest> createV)
    { _service = s; _tenant = t; _createV = createV; }
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
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateResourceRequest req, CancellationToken ct)
    {
        var v = await _createV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _service.CreateAsync(TenantId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateResourceRequest req, CancellationToken ct)
    {
        var r = await _service.UpdateAsync(TenantId, id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    private static ValidationProblemDetails ValidationProblem(FluentValidation.Results.ValidationResult v) =>
        new(v.Errors.GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    private static ProblemDetails Problem<T>(ProjectResult<T> r) => new()
    {
        Title = "Resource Error", Status = StatusCodes.Status400BadRequest, Detail = r.Error
    };
}
