using System.Security.Claims;
using ERPSystem.Modules.Projects.Application;
using ERPSystem.Modules.Projects.Application.Services;
using ERPSystem.Modules.Projects.Entities;
using ERPSystem.Shared.MultiTenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projects;
    private readonly ITaskService _tasks;
    private readonly IBudgetService _budgets;
    private readonly IResourceAssignmentService _assignments;
    private readonly ITenantContext _tenant;
    private readonly IValidator<CreateProjectRequest> _createV;
    private readonly IValidator<UpdateProjectRequest> _updateV;

    public ProjectsController(IProjectService projects, ITaskService tasks, IBudgetService budgets,
        IResourceAssignmentService assignments, ITenantContext tenant,
        IValidator<CreateProjectRequest> createV, IValidator<UpdateProjectRequest> updateV)
    {
        _projects = projects; _tasks = tasks; _budgets = budgets; _assignments = assignments; _tenant = tenant;
        _createV = createV; _updateV = updateV;
    }

    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();
    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? companyId,
        [FromQuery] ProjectStatus? status,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var r = await _projects.ListAsync(TenantId, companyId, status, includeInactive, skip, take, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var r = await _projects.GetByIdAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest req, CancellationToken ct)
    {
        var v = await _createV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _projects.CreateAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetById), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectRequest req, CancellationToken ct)
    {
        var v = await _updateV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _projects.UpdateAsync(TenantId, UserId, id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeStatusRequest req, CancellationToken ct)
    {
        var r = await _projects.ChangeStatusAsync(TenantId, UserId, id, req.Status, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var r = await _projects.DeactivateAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? NoContent() : BadRequest(Problem(r));
    }

    [HttpGet("{id:guid}/tasks")]
    public async Task<IActionResult> GetTasks(Guid id, CancellationToken ct)
    {
        var r = await _tasks.ListByProjectAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpGet("{id:guid}/budget")]
    public async Task<IActionResult> GetBudget(Guid id, CancellationToken ct)
    {
        var r = await _budgets.GetByProjectAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }

    [HttpPost("{id:guid}/budget/recalculate")]
    public async Task<IActionResult> RecalculateBudget(Guid id, CancellationToken ct)
    {
        var r = await _budgets.RecalculateSpentAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }

    [HttpGet("{id:guid}/assignments")]
    public async Task<IActionResult> GetAssignments(Guid id, CancellationToken ct)
    {
        var r = await _assignments.ListByProjectAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpPost("{id:guid}/assignments")]
    public async Task<IActionResult> AddAssignment(Guid id, [FromBody] CreateAssignmentRequest req, CancellationToken ct)
    {
        req.ProjectId = id; // override from route
        var r = await _assignments.CreateAsync(TenantId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetAssignments), new { id = r.Value!.ProjectId }, r.Value)
            : BadRequest(Problem(r));
    }

    [HttpDelete("{id:guid}/assignments/{assignmentId:guid}")]
    public async Task<IActionResult> RemoveAssignment(Guid id, Guid assignmentId, CancellationToken ct)
    {
        var r = await _assignments.DeleteAsync(TenantId, assignmentId, ct);
        return r.Succeeded ? NoContent() : BadRequest(Problem(r));
    }

    private static ValidationProblemDetails ValidationProblem(FluentValidation.Results.ValidationResult v) =>
        new(v.Errors.GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    private static ProblemDetails Problem<T>(ProjectResult<T> r) => new()
    {
        Title = "Project Error",
        Status = StatusCodes.Status400BadRequest,
        Detail = r.Error,
    };
}

public sealed class ChangeStatusRequest
{
    public ProjectStatus Status { get; set; }
}
