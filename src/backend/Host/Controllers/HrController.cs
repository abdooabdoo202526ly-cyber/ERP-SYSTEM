using System.Security.Claims;
using ERPSystem.Modules.HR.Application;
using ERPSystem.Modules.HR.Application.Services;
using ERPSystem.Modules.HR.Entities;
using ERPSystem.Modules.Payroll.Application;
using ERPSystem.Modules.Payroll.Application.Services;
using ERPSystem.Modules.Payroll.Domain.Entities;
using ERPSystem.Shared.MultiTenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

/// <summary>
/// HR API — Departments, Employees, Attendance, LeaveRequests.
/// يتبع نفس النمط الموحّد: TenantId من ITenantContext + Result pattern + FluentValidation.
/// </summary>
[ApiController]
[Authorize]
public class HrController : ControllerBase
{
    private readonly IDepartmentService _depts;
    private readonly IEmployeeService _employees;
    private readonly IAttendanceService _attendance;
    private readonly ILeaveRequestService _leaves;
    private readonly ITenantContext _tenant;

    // ============ Phase 4: Payroll + EOS ============
    private readonly IPayrollService _payroll;
    private readonly IEosService _eos;

    private readonly IValidator<CreateDepartmentRequest> _createDeptV;
    private readonly IValidator<UpdateDepartmentRequest> _updateDeptV;
    private readonly IValidator<CreateEmployeeRequest> _createEmpV;
    private readonly IValidator<UpdateEmployeeRequest> _updateEmpV;
    private readonly IValidator<CheckInOutRequest> _checkV;
    private readonly IValidator<CreateLeaveRequestDto> _createLeaveV;
    private readonly IValidator<UpdateLeaveRequestDto> _updateLeaveV;
    private readonly IValidator<CreatePayrollRunRequest> _createPayrollV;

    public HrController(
        IDepartmentService depts, IEmployeeService employees, IAttendanceService attendance, ILeaveRequestService leaves,
        ITenantContext tenant,
        IPayrollService payroll, IEosService eos,
        IValidator<CreateDepartmentRequest> createDeptV, IValidator<UpdateDepartmentRequest> updateDeptV,
        IValidator<CreateEmployeeRequest> createEmpV, IValidator<UpdateEmployeeRequest> updateEmpV,
        IValidator<CheckInOutRequest> checkV, IValidator<CreateLeaveRequestDto> createLeaveV,
        IValidator<UpdateLeaveRequestDto> updateLeaveV,
        IValidator<CreatePayrollRunRequest> createPayrollV)
    {
        _depts = depts; _employees = employees; _attendance = attendance; _leaves = leaves; _tenant = tenant;
        _payroll = payroll; _eos = eos;
        _createDeptV = createDeptV; _updateDeptV = updateDeptV;
        _createEmpV = createEmpV; _updateEmpV = updateEmpV;
        _checkV = checkV; _createLeaveV = createLeaveV; _updateLeaveV = updateLeaveV;
        _createPayrollV = createPayrollV;
    }

    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();
    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value);
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    // ============== Departments ==============

    [HttpGet("api/hr/departments")]
    public async Task<IActionResult> ListDepartments([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var r = await _depts.ListAsync(TenantId, includeInactive, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpGet("api/hr/departments/{id:guid}")]
    public async Task<IActionResult> GetDepartment(Guid id, CancellationToken ct)
    {
        var r = await _depts.GetByIdAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }

    [HttpPost("api/hr/departments")]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentRequest req, CancellationToken ct)
    {
        var v = await _createDeptV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _depts.CreateAsync(TenantId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetDepartment), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }

    [HttpPut("api/hr/departments/{id:guid}")]
    public async Task<IActionResult> UpdateDepartment(Guid id, [FromBody] UpdateDepartmentRequest req, CancellationToken ct)
    {
        var v = await _updateDeptV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _depts.UpdateAsync(TenantId, id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpDelete("api/hr/departments/{id:guid}")]
    public async Task<IActionResult> DeactivateDepartment(Guid id, CancellationToken ct)
    {
        var r = await _depts.DeactivateAsync(TenantId, id, ct);
        return r.Succeeded ? NoContent() : BadRequest(Problem(r));
    }

    // ============== Employees ==============

    [HttpGet("api/hr/employees")]
    public async Task<IActionResult> ListEmployees(
        [FromQuery] Guid? departmentId, [FromQuery] bool includeInactive = false,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var r = await _employees.ListAsync(TenantId, departmentId, includeInactive, skip, take, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpGet("api/hr/employees/{id:guid}")]
    public async Task<IActionResult> GetEmployee(Guid id, CancellationToken ct)
    {
        var r = await _employees.GetByIdAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }

    [HttpPost("api/hr/employees")]
    public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeRequest req, CancellationToken ct)
    {
        var v = await _createEmpV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _employees.CreateAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetEmployee), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }

    [HttpPut("api/hr/employees/{id:guid}")]
    public async Task<IActionResult> UpdateEmployee(Guid id, [FromBody] UpdateEmployeeRequest req, CancellationToken ct)
    {
        var v = await _updateEmpV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _employees.UpdateAsync(TenantId, UserId, id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpDelete("api/hr/employees/{id:guid}")]
    public async Task<IActionResult> DeactivateEmployee(Guid id, CancellationToken ct)
    {
        var r = await _employees.DeactivateAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? NoContent() : BadRequest(Problem(r));
    }

    // ============== Attendance ==============

    [HttpGet("api/hr/attendance")]
    public async Task<IActionResult> ListAttendance(
        [FromQuery] Guid? employeeId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var r = await _attendance.ListAsync(TenantId, employeeId, from, to, skip, take, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpPost("api/hr/attendance")]
    public async Task<IActionResult> RecordAttendance([FromBody] CheckInOutRequest req, CancellationToken ct)
    {
        var v = await _checkV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _attendance.RecordAsync(TenantId, req, ClientIp, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetAttendance), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }

    [HttpGet("api/hr/attendance/{id:guid}")]
    public async Task<IActionResult> GetAttendance(Guid id, CancellationToken ct)
    {
        var r = await _attendance.GetByIdAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }

    // T4-HR-Details: تعديل سجل حضور (نفس اليوم فقط).
    [HttpPut("api/hr/attendance/{id:guid}")]
    public async Task<IActionResult> UpdateAttendance(Guid id, [FromBody] UpdateAttendanceRequest req, CancellationToken ct)
    {
        var r = await _attendance.UpdateAsync(TenantId, id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    // T4-HR-Details: حذف سجل حضور (نفس اليوم فقط).
    [HttpDelete("api/hr/attendance/{id:guid}")]
    public async Task<IActionResult> DeleteAttendance(Guid id, CancellationToken ct)
    {
        var r = await _attendance.DeleteAsync(TenantId, id, ct);
        return r.Succeeded ? NoContent() : BadRequest(Problem(r));
    }

    // ============== Leave Requests ==============

    [HttpGet("api/hr/leaves")]
    public async Task<IActionResult> ListLeaves(
        [FromQuery] Guid? employeeId, [FromQuery] LeaveStatus? status,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var r = await _leaves.ListAsync(TenantId, employeeId, status, skip, take, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpGet("api/hr/leaves/{id:guid}")]
    public async Task<IActionResult> GetLeave(Guid id, CancellationToken ct)
    {
        var r = await _leaves.GetByIdAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(Problem(r));
    }

    [HttpPost("api/hr/leaves")]
    public async Task<IActionResult> CreateLeave([FromBody] CreateLeaveRequestDto req, CancellationToken ct)
    {
        var v = await _createLeaveV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _leaves.CreateAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetLeave), new { id = r.Value!.Id }, r.Value)
            : BadRequest(Problem(r));
    }

    // T4-HR-Details: تعديل طلب إجازة (متاح فقط إذا الحالة Pending — يُفرض في الـ service).
    [HttpPut("api/hr/leaves/{id:guid}")]
    public async Task<IActionResult> UpdateLeave(Guid id, [FromBody] UpdateLeaveRequestDto req, CancellationToken ct)
    {
        var v = await _updateLeaveV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _leaves.UpdateAsync(TenantId, UserId, id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpPut("api/hr/leaves/{id:guid}/approve")]
    public async Task<IActionResult> ApproveLeave(Guid id, CancellationToken ct)
    {
        var r = await _leaves.ApproveAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    [HttpPut("api/hr/leaves/{id:guid}/reject")]
    public async Task<IActionResult> RejectLeave(Guid id, CancellationToken ct)
    {
        var r = await _leaves.RejectAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(Problem(r));
    }

    // ============== Payroll (Phase 4) ==============

    /// <summary>قائمة دورات الرواتب (مع filter اختياري على الحالة).</summary>
    [HttpGet("api/hr/payroll/runs")]
    public async Task<IActionResult> ListPayrollRuns(
        [FromQuery] PayrollRunStatus? status,
        [FromQuery] int skip = 0, [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var r = await _payroll.ListRunsAsync(TenantId, status, skip, take, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(PayrollProblem(r));
    }

    /// <summary>تفاصيل دورة رواتب واحدة.</summary>
    [HttpGet("api/hr/payroll/runs/{id:guid}")]
    public async Task<IActionResult> GetPayrollRun(Guid id, CancellationToken ct)
    {
        var r = await _payroll.GetRunAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(PayrollProblem(r));
    }

    /// <summary>إنشاء دورة رواتب جديدة (Draft).</summary>
    [HttpPost("api/hr/payroll/runs")]
    public async Task<IActionResult> CreatePayrollRun([FromBody] CreatePayrollRunRequest req, CancellationToken ct)
    {
        var v = await _createPayrollV.ValidateAsync(req, ct);
        if (!v.IsValid) return BadRequest(ValidationProblem(v));
        var r = await _payroll.CreateRunAsync(TenantId, UserId, req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(GetPayrollRunItems), new { id = r.Value!.Id }, r.Value)
            : BadRequest(PayrollProblem(r));
    }

    /// <summary>معالجة الدورة: يحسب payslip لكل موظف نشط ويحدّث الحالة إلى Processing.</summary>
    [HttpPost("api/hr/payroll/runs/{id:guid}/process")]
    public async Task<IActionResult> ProcessPayrollRun(Guid id, CancellationToken ct)
    {
        var r = await _payroll.ProcessRunAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(PayrollProblem(r));
    }

    /// <summary>ترحيل الدورة: ينشئ JournalEntry (Dr Salary / Cr Cash) ويحدّث الحالة إلى Posted.</summary>
    [HttpPost("api/hr/payroll/runs/{id:guid}/post")]
    public async Task<IActionResult> PostPayrollRun(Guid id, CancellationToken ct)
    {
        var r = await _payroll.PostRunAsync(TenantId, UserId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(PayrollProblem(r));
    }

    /// <summary>قائمة payslips الدورة.</summary>
    [HttpGet("api/hr/payroll/runs/{id:guid}/items")]
    public async Task<IActionResult> GetPayrollRunItems(Guid id, CancellationToken ct)
    {
        var r = await _payroll.GetItemsAsync(TenantId, id, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(PayrollProblem(r));
    }

    /// <summary>تفاصيل payslip موظف واحد ضمن الدورة.</summary>
    [HttpGet("api/hr/payroll/runs/{id:guid}/items/{empId:guid}/payslip")]
    public async Task<IActionResult> GetPayrollPayslip(Guid id, Guid empId, CancellationToken ct)
    {
        var r = await _payroll.GetPayslipAsync(TenantId, id, empId, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(PayrollProblem(r));
    }

    /// <summary>حساب مستحقات نهاية الخدمة (EOS) لموظف.</summary>
    [HttpGet("api/hr/payroll/eos/{empId:guid}")]
    public async Task<IActionResult> CalculateEos(Guid empId, [FromQuery] DateTime? terminationDate, CancellationToken ct)
    {
        var r = await _eos.CalculateEosAsync(TenantId, empId, terminationDate, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(PayrollProblem(r));
    }

    // ============== Helpers ==============

    private static ValidationProblemDetails ValidationProblem(FluentValidation.Results.ValidationResult v) =>
        new(v.Errors.GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    private static ProblemDetails Problem<T>(HRResult<T> r) => new()
    {
        Title = "HR Error",
        Status = StatusCodes.Status400BadRequest,
        Detail = r.Error,
    };
    private static ProblemDetails PayrollProblem<T>(PayrollResult<T> r) => new()
    {
        Title = "Payroll Error",
        Status = StatusCodes.Status400BadRequest,
        Detail = r.Error,
    };
}
