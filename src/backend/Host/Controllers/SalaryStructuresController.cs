using System.Security.Claims;
using ERPSystem.Modules.Payroll.Application;
using ERPSystem.Modules.Payroll.Application.Services;
using ERPSystem.Shared.MultiTenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

/// <summary>
/// SalaryStructure API — CRUD لهياكل الرواتب (مع مكوّنات الـ lines).
/// المسار: /api/hr/salary-structures
/// يتبع نفس النمط الموحَّد: TenantId من ITenantContext + Result pattern + FluentValidation.
/// </summary>
[ApiController]
[Route("api/hr/salary-structures")]
[Authorize]
public class SalaryStructuresController : ControllerBase
{
    private readonly ISalaryStructureService _service;
    private readonly ITenantContext _tenant;
    private readonly IValidator<CreateSalaryStructureRequest> _validator;

    public SalaryStructuresController(
        ISalaryStructureService service,
        ITenantContext tenant,
        IValidator<CreateSalaryStructureRequest> validator)
    {
        _service = service; _tenant = tenant; _validator = validator;
    }

    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();
    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value);

    /// <summary>قائمة هياكل الرواتب (مع filter اختياري على الحالة).</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool includeInactive = false,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var r = await _service.ListAsync(TenantId, includeInactive, skip, take, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    /// <summary>تفاصيل هيكل راتب واحد (مع الـ lines كاملة).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var r = await _service.GetByIdAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }

    /// <summary>إنشاء هيكل راتب جديد (مع مكوّنات الـ lines).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSalaryStructureRequest req, CancellationToken ct)
    {
        var v = await _validator.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _service.CreateAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }

    /// <summary>تحديث هيكل راتب — full-replace للـ lines.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateSalaryStructureRequest req, CancellationToken ct)
    {
        var v = await _validator.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _service.UpdateAsync(TenantId, UserId, id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    /// <summary>إيقاف (soft-delete) هيكل راتب — IsActive=false.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var r = await _service.DeactivateAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? NoContent() : BadRequest(Problem(r));
    }

    // ============== Helpers ==============

    private static ValidationProblemDetails ValidationProblem(FluentValidation.Results.ValidationResult v) =>
        new(v.Errors.GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));

    private static ProblemDetails Problem<T>(PayrollResult<T> r) => new()
    {
        Title = "SalaryStructure Error",
        Status = StatusCodes.Status400BadRequest,
        Detail = r.Error,
    };
}
