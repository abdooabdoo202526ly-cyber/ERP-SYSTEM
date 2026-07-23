using ERPSystem.Modules.Projects.Application;
using ERPSystem.Modules.Projects.Application.Services;
using ERPSystem.Shared.MultiTenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskService _tasks;
    private readonly ITenantContext _tenant;
    private readonly IValidator<CreateTaskRequest> _createV;
    private readonly IValidator<UpdateTaskRequest> _updateV;
    public TasksController(ITaskService tasks, ITenantContext tenant, IValidator<CreateTaskRequest> createV, IValidator<UpdateTaskRequest> updateV)
    { _tasks = tasks; _tenant = tenant; _createV = createV; _updateV = updateV; }
    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var r = await _tasks.GetByIdAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest req, CancellationToken ct)
    {
        var v = await _createV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _tasks.CreateAsync(TenantId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskRequest req, CancellationToken ct)
    {
        var v = await _updateV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _tasks.UpdateAsync(TenantId, id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var r = await _tasks.DeleteAsync(TenantId, id, ct);
        return r.Succeeded ? NoContent() : BadRequest(Problem(r));
    }

    private static ValidationProblemDetails ValidationProblem(FluentValidation.Results.ValidationResult v) =>
        new(v.Errors.GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    private static ProblemDetails Problem<T>(ProjectResult<T> r) => new()
    {
        Title = "Task Error", Status = StatusCodes.Status400BadRequest, Detail = r.Error
    };
}
