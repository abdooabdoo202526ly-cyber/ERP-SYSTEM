using ERPSystem.Modules.HR.Application;
using ERPSystem.Modules.HR.Entities;
using ERPSystem.Modules.HR.Infrastructure;

namespace ERPSystem.Modules.HR.Application.Services;

public sealed class HRResult<T>
{
    public bool Succeeded { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public HRErrorCode? ErrorCode { get; init; }
    public static HRResult<T> Ok(T v) => new() { Succeeded = true, Value = v };
    public static HRResult<T> Fail(string e, HRErrorCode c) => new() { Succeeded = false, Error = e, ErrorCode = c };
}

public enum HRErrorCode
{
    NotFound, AlreadyExists, ValidationError, InvalidStatusTransition, BusinessRuleViolation, Internal
}

public interface IDepartmentService
{
    Task<HRResult<DepartmentResponse>> CreateAsync(Guid tenantId, CreateDepartmentRequest req, CancellationToken ct);
    Task<HRResult<DepartmentResponse>> UpdateAsync(Guid tenantId, Guid id, UpdateDepartmentRequest req, CancellationToken ct);
    Task<HRResult<DepartmentResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<HRResult<IReadOnlyList<DepartmentResponse>>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct);
    Task<HRResult<bool>> DeactivateAsync(Guid tenantId, Guid id, CancellationToken ct);
}

public sealed class DepartmentService : IDepartmentService
{
    private readonly IDepartmentRepository _repo;
    public DepartmentService(IDepartmentRepository repo) => _repo = repo;

    public async Task<HRResult<DepartmentResponse>> CreateAsync(Guid tenantId, CreateDepartmentRequest req, CancellationToken ct)
    {
        if (await _repo.GetByCodeAsync(tenantId, req.Code, ct) != null)
            return HRResult<DepartmentResponse>.Fail("كود القسم مستخدم.", HRErrorCode.AlreadyExists);
        if (req.ParentId.HasValue)
        {
            var parent = await _repo.GetByIdAsync(req.ParentId.Value, ct);
            if (parent == null || parent.TenantId != tenantId)
                return HRResult<DepartmentResponse>.Fail("القسم الأب غير موجود.", HRErrorCode.NotFound);
        }
        var now = DateTime.UtcNow;
        var d = new Department
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            Code = req.Code.Trim(), Name = req.Name.Trim(),
            ParentId = req.ParentId, ManagerId = req.ManagerId,
            IsActive = true, CreatedAt = now, UpdatedAt = now
        };
        await _repo.InsertAsync(d, ct);
        return HRResult<DepartmentResponse>.Ok(MapToResponse(d));
    }

    public async Task<HRResult<DepartmentResponse>> UpdateAsync(Guid tenantId, Guid id, UpdateDepartmentRequest req, CancellationToken ct)
    {
        var d = await _repo.GetByIdAsync(id, ct);
        if (d == null || d.TenantId != tenantId)
            return HRResult<DepartmentResponse>.Fail("غير موجود.", HRErrorCode.NotFound);
        d.Name = req.Name.Trim();
        d.ParentId = req.ParentId;
        d.ManagerId = req.ManagerId;
        d.IsActive = req.IsActive;
        d.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(d, ct);
        return HRResult<DepartmentResponse>.Ok(MapToResponse(d));
    }

    public async Task<HRResult<DepartmentResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var d = await _repo.GetByIdAsync(id, ct);
        if (d == null || d.TenantId != tenantId)
            return HRResult<DepartmentResponse>.Fail("غير موجود.", HRErrorCode.NotFound);
        return HRResult<DepartmentResponse>.Ok(MapToResponse(d));
    }

    public async Task<HRResult<IReadOnlyList<DepartmentResponse>>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct)
    {
        var list = await _repo.ListAsync(tenantId, includeInactive, ct);
        return HRResult<IReadOnlyList<DepartmentResponse>>.Ok(list.Select(MapToResponse).ToList());
    }

    public async Task<HRResult<bool>> DeactivateAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var d = await _repo.GetByIdAsync(id, ct);
        if (d == null || d.TenantId != tenantId)
            return HRResult<bool>.Fail("غير موجود.", HRErrorCode.NotFound);
        d.IsActive = false;
        d.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(d, ct);
        return HRResult<bool>.Ok(true);
    }

    private static DepartmentResponse MapToResponse(Department d) => new()
    {
        Id = d.Id, TenantId = d.TenantId, Code = d.Code, Name = d.Name,
        ParentId = d.ParentId, ManagerId = d.ManagerId, IsActive = d.IsActive
    };
}

public interface IEmployeeService
{
    Task<HRResult<EmployeeResponse>> CreateAsync(Guid tenantId, Guid userId, CreateEmployeeRequest req, CancellationToken ct);
    Task<HRResult<EmployeeResponse>> UpdateAsync(Guid tenantId, Guid userId, Guid id, UpdateEmployeeRequest req, CancellationToken ct);
    Task<HRResult<EmployeeResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<HRResult<IReadOnlyList<EmployeeResponse>>> ListAsync(Guid tenantId, Guid? departmentId, bool includeInactive, int skip, int take, CancellationToken ct);
    Task<HRResult<bool>> DeactivateAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
}

public sealed class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _repo;
    private readonly IHRDocumentSequenceRepository _seq;
    public EmployeeService(IEmployeeRepository repo, IHRDocumentSequenceRepository seq) { _repo = repo; _seq = seq; }

    public async Task<HRResult<EmployeeResponse>> CreateAsync(Guid tenantId, Guid userId, CreateEmployeeRequest req, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(req.Email) && await _repo.GetByEmailAsync(tenantId, req.Email, ct) != null)
            return HRResult<EmployeeResponse>.Fail("البريد الإلكتروني مستخدم.", HRErrorCode.AlreadyExists);

        var empNumber = await _seq.GetNextEmployeeNumberAsync(tenantId, ct);
        var now = DateTime.UtcNow;
        var e = new Employee
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            EmployeeNumber = empNumber, FullName = req.FullName.Trim(),
            Email = req.Email, Phone = req.Phone, NationalId = req.NationalId,
            DepartmentId = req.DepartmentId, JobTitle = req.JobTitle,
            HireDate = req.HireDate, BaseSalary = req.BaseSalary,
            IsActive = true, CreatedAt = now, CreatedBy = userId, UpdatedAt = now, UpdatedBy = userId
        };
        await _repo.InsertAsync(e, ct);
        return HRResult<EmployeeResponse>.Ok(MapToResponse(e));
    }

    public async Task<HRResult<EmployeeResponse>> UpdateAsync(Guid tenantId, Guid userId, Guid id, UpdateEmployeeRequest req, CancellationToken ct)
    {
        var e = await _repo.GetByIdAsync(id, ct);
        if (e == null || e.TenantId != tenantId)
            return HRResult<EmployeeResponse>.Fail("غير موجود.", HRErrorCode.NotFound);
        if (!string.IsNullOrEmpty(req.Email) && !string.Equals(e.Email, req.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (await _repo.GetByEmailAsync(tenantId, req.Email, ct) != null)
                return HRResult<EmployeeResponse>.Fail("البريد الإلكتروني مستخدم.", HRErrorCode.AlreadyExists);
        }
        e.FullName = req.FullName.Trim();
        e.Email = req.Email; e.Phone = req.Phone; e.NationalId = req.NationalId;
        e.DepartmentId = req.DepartmentId; e.JobTitle = req.JobTitle;
        e.TerminationDate = req.TerminationDate;
        e.BaseSalary = req.BaseSalary;
        e.IsActive = req.IsActive;
        e.UpdatedAt = DateTime.UtcNow; e.UpdatedBy = userId;
        await _repo.UpdateAsync(e, ct);
        return HRResult<EmployeeResponse>.Ok(MapToResponse(e));
    }

    public async Task<HRResult<EmployeeResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var e = await _repo.GetByIdAsync(id, ct);
        if (e == null || e.TenantId != tenantId)
            return HRResult<EmployeeResponse>.Fail("غير موجود.", HRErrorCode.NotFound);
        return HRResult<EmployeeResponse>.Ok(MapToResponse(e));
    }

    public async Task<HRResult<IReadOnlyList<EmployeeResponse>>> ListAsync(Guid tenantId, Guid? departmentId, bool includeInactive, int skip, int take, CancellationToken ct)
    {
        if (take is < 1 or > 200) take = 50;
        var list = await _repo.ListAsync(tenantId, departmentId, includeInactive, skip, take, ct);
        return HRResult<IReadOnlyList<EmployeeResponse>>.Ok(list.Select(MapToResponse).ToList());
    }

    public async Task<HRResult<bool>> DeactivateAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        var e = await _repo.GetByIdAsync(id, ct);
        if (e == null || e.TenantId != tenantId)
            return HRResult<bool>.Fail("غير موجود.", HRErrorCode.NotFound);
        e.IsActive = false;
        e.TerminationDate = e.TerminationDate ?? DateTime.UtcNow;
        e.UpdatedAt = DateTime.UtcNow; e.UpdatedBy = userId;
        await _repo.UpdateAsync(e, ct);
        return HRResult<bool>.Ok(true);
    }

    private static EmployeeResponse MapToResponse(Employee e) => new()
    {
        Id = e.Id, TenantId = e.TenantId, EmployeeNumber = e.EmployeeNumber, FullName = e.FullName,
        Email = e.Email, Phone = e.Phone, NationalId = e.NationalId,
        DepartmentId = e.DepartmentId, JobTitle = e.JobTitle,
        HireDate = e.HireDate, TerminationDate = e.TerminationDate,
        BaseSalary = e.BaseSalary, IsActive = e.IsActive
    };
}

public interface IAttendanceService
{
    Task<HRResult<AttendanceResponse>> RecordAsync(Guid tenantId, CheckInOutRequest req, string? ipAddress, CancellationToken ct);
    Task<HRResult<AttendanceResponse>> UpdateAsync(Guid tenantId, Guid id, UpdateAttendanceRequest req, CancellationToken ct);
    Task<HRResult<bool>> DeleteAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<HRResult<AttendanceResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<HRResult<IReadOnlyList<AttendanceResponse>>> ListAsync(Guid tenantId, Guid? employeeId, DateTime? from, DateTime? to, int skip, int take, CancellationToken ct);
}

public sealed class AttendanceService : IAttendanceService
{
    private readonly IAttendanceRepository _repo;
    private readonly IEmployeeRepository _employees;
    public AttendanceService(IAttendanceRepository repo, IEmployeeRepository employees) { _repo = repo; _employees = employees; }

    public async Task<HRResult<AttendanceResponse>> RecordAsync(Guid tenantId, CheckInOutRequest req, string? ipAddress, CancellationToken ct)
    {
        var emp = await _employees.GetByIdAsync(req.EmployeeId, ct);
        if (emp == null || emp.TenantId != tenantId)
            return HRResult<AttendanceResponse>.Fail("الموظف غير موجود.", HRErrorCode.NotFound);

        // Business Rule: لا تكرار من نفس النوع متتالياً
        var last = await _repo.GetLastForEmployeeAsync(tenantId, req.EmployeeId, ct);
        if (last != null && last.Type == req.Type && (DateTime.UtcNow - last.Timestamp).TotalHours < 12)
            return HRResult<AttendanceResponse>.Fail(
                $"لا يمكن تسجيل {req.Type} متتالي بدون النوع المعاكس.", HRErrorCode.BusinessRuleViolation);

        var att = new Attendance
        {
            Id = Guid.NewGuid(), TenantId = tenantId, EmployeeId = req.EmployeeId,
            Type = req.Type, Timestamp = DateTime.UtcNow, Notes = req.Notes, IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        };
        await _repo.InsertAsync(att, ct);
        return HRResult<AttendanceResponse>.Ok(MapToResponse(att));
    }

    public async Task<HRResult<AttendanceResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var a = await _repo.GetByIdAsync(id, ct);
        if (a == null || a.TenantId != tenantId)
            return HRResult<AttendanceResponse>.Fail("غير موجود.", HRErrorCode.NotFound);
        return HRResult<AttendanceResponse>.Ok(MapToResponse(a));
    }

    // T4-HR-Details: تعديل سجل حضور — متاح فقط في نفس اليوم (24h من Timestamp).
    // نسمح فقط بتعديل Notes (لا يمكن تغيير Type/EmployeeId/Timestamp لأسباب تدقيقية).
    public async Task<HRResult<AttendanceResponse>> UpdateAsync(Guid tenantId, Guid id, UpdateAttendanceRequest req, CancellationToken ct)
    {
        var a = await _repo.GetByIdAsync(id, ct);
        if (a == null || a.TenantId != tenantId)
            return HRResult<AttendanceResponse>.Fail("غير موجود.", HRErrorCode.NotFound);
        // Business Rule: التعديل مسموح في نفس اليوم (UTC date) فقط.
        var today = DateTime.UtcNow.Date;
        if (a.Timestamp.Date != today)
            return HRResult<AttendanceResponse>.Fail(
                "لا يمكن تعديل سجل حضور قديم — التعديل مسموح في نفس اليوم فقط.", HRErrorCode.BusinessRuleViolation);

        a.Notes = req.Notes;
        await _repo.UpdateAsync(a, ct);
        return HRResult<AttendanceResponse>.Ok(MapToResponse(a));
    }

    // T4-HR-Details: حذف سجل حضور — متاح فقط في نفس اليوم.
    public async Task<HRResult<bool>> DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var a = await _repo.GetByIdAsync(id, ct);
        if (a == null || a.TenantId != tenantId)
            return HRResult<bool>.Fail("غير موجود.", HRErrorCode.NotFound);
        var today = DateTime.UtcNow.Date;
        if (a.Timestamp.Date != today)
            return HRResult<bool>.Fail(
                "لا يمكن حذف سجل حضور قديم — الحذف مسموح في نفس اليوم فقط.", HRErrorCode.BusinessRuleViolation);
        await _repo.DeleteAsync(id, ct);
        return HRResult<bool>.Ok(true);
    }

    public async Task<HRResult<IReadOnlyList<AttendanceResponse>>> ListAsync(Guid tenantId, Guid? employeeId, DateTime? from, DateTime? to, int skip, int take, CancellationToken ct)
    {
        if (take is < 1 or > 200) take = 50;
        var list = await _repo.ListAsync(tenantId, employeeId, from, to, skip, take, ct);
        return HRResult<IReadOnlyList<AttendanceResponse>>.Ok(list.Select(MapToResponse).ToList());
    }

    private static AttendanceResponse MapToResponse(Attendance a) => new()
    {
        Id = a.Id, EmployeeId = a.EmployeeId, Type = a.Type, Timestamp = a.Timestamp,
        Notes = a.Notes, IpAddress = a.IpAddress
    };
}

public interface ILeaveRequestService
{
    Task<HRResult<LeaveRequestResponse>> CreateAsync(Guid tenantId, Guid userId, CreateLeaveRequestDto req, CancellationToken ct);
    Task<HRResult<LeaveRequestResponse>> UpdateAsync(Guid tenantId, Guid userId, Guid id, UpdateLeaveRequestDto req, CancellationToken ct);
    Task<HRResult<LeaveRequestResponse>> ApproveAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
    Task<HRResult<LeaveRequestResponse>> RejectAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
    Task<HRResult<LeaveRequestResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<HRResult<IReadOnlyList<LeaveRequestResponse>>> ListAsync(Guid tenantId, Guid? employeeId, LeaveStatus? status, int skip, int take, CancellationToken ct);
}

public sealed class LeaveRequestService : ILeaveRequestService
{
    private readonly ILeaveRequestRepository _repo;
    private readonly IEmployeeRepository _employees;
    public LeaveRequestService(ILeaveRequestRepository repo, IEmployeeRepository employees) { _repo = repo; _employees = employees; }

    public async Task<HRResult<LeaveRequestResponse>> CreateAsync(Guid tenantId, Guid userId, CreateLeaveRequestDto req, CancellationToken ct)
    {
        var emp = await _employees.GetByIdAsync(req.EmployeeId, ct);
        if (emp == null || emp.TenantId != tenantId)
            return HRResult<LeaveRequestResponse>.Fail("الموظف غير موجود.", HRErrorCode.NotFound);

        // Business Rule: لا تتعارض مع إجازة Approved أخرى
        if (await _repo.HasOverlappingApprovedAsync(req.EmployeeId, req.StartDate, req.EndDate, ct))
            return HRResult<LeaveRequestResponse>.Fail("يوجد إجازة معتمدة أخرى للموظف في نفس الفترة.", HRErrorCode.BusinessRuleViolation);

        var totalDays = (int)(req.EndDate.Date - req.StartDate.Date).TotalDays + 1;
        var now = DateTime.UtcNow;
        var leave = new LeaveRequest
        {
            Id = Guid.NewGuid(), TenantId = tenantId, EmployeeId = req.EmployeeId,
            LeaveType = req.LeaveType, StartDate = req.StartDate, EndDate = req.EndDate,
            TotalDays = totalDays, Status = LeaveStatus.Pending,
            Reason = req.Reason, Notes = req.Notes,
            CreatedAt = now, CreatedBy = userId, UpdatedAt = now
        };
        await _repo.InsertAsync(leave, ct);
        return HRResult<LeaveRequestResponse>.Ok(MapToResponse(leave));
    }

    public async Task<HRResult<LeaveRequestResponse>> UpdateAsync(Guid tenantId, Guid userId, Guid id, UpdateLeaveRequestDto req, CancellationToken ct)
    {
        var l = await _repo.GetByIdAsync(id, ct);
        if (l == null || l.TenantId != tenantId)
            return HRResult<LeaveRequestResponse>.Fail("غير موجود.", HRErrorCode.NotFound);
        // T4-HR-Details: التعديل متاح فقط إذا الطلب بانتظار الموافقة.
        if (l.Status != LeaveStatus.Pending)
            return HRResult<LeaveRequestResponse>.Fail(
                $"لا يمكن تعديل طلب في حالة {l.Status}.", HRErrorCode.InvalidStatusTransition);
        if (req.EndDate.Date < req.StartDate.Date)
            return HRResult<LeaveRequestResponse>.Fail(
                "تاريخ النهاية يجب أن يكون بعد أو يساوي تاريخ البداية.", HRErrorCode.ValidationError);

        // Business Rule: لا تتعارض مع إجازة Approved أخرى (بعد التحديث)
        if (await _repo.HasOverlappingApprovedExcludingAsync(l.EmployeeId, req.StartDate, req.EndDate, l.Id, ct))
            return HRResult<LeaveRequestResponse>.Fail("يوجد إجازة معتمدة أخرى للموظف في نفس الفترة.", HRErrorCode.BusinessRuleViolation);

        var totalDays = (int)(req.EndDate.Date - req.StartDate.Date).TotalDays + 1;
        l.LeaveType = req.LeaveType;
        l.StartDate = req.StartDate;
        l.EndDate = req.EndDate;
        l.TotalDays = totalDays;
        l.Reason = req.Reason;
        l.Notes = req.Notes;
        l.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(l, ct);
        return HRResult<LeaveRequestResponse>.Ok(MapToResponse(l));
    }

    public async Task<HRResult<LeaveRequestResponse>> ApproveAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        var l = await _repo.GetByIdAsync(id, ct);
        if (l == null || l.TenantId != tenantId)
            return HRResult<LeaveRequestResponse>.Fail("غير موجود.", HRErrorCode.NotFound);
        if (l.Status != LeaveStatus.Pending)
            return HRResult<LeaveRequestResponse>.Fail($"لا يمكن الموافقة على إجازة في حالة {l.Status}.", HRErrorCode.InvalidStatusTransition);
        l.Status = LeaveStatus.Approved;
        l.ApproverId = userId;
        l.ApprovedAt = DateTime.UtcNow;
        l.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(l, ct);
        return HRResult<LeaveRequestResponse>.Ok(MapToResponse(l));
    }

    public async Task<HRResult<LeaveRequestResponse>> RejectAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        var l = await _repo.GetByIdAsync(id, ct);
        if (l == null || l.TenantId != tenantId)
            return HRResult<LeaveRequestResponse>.Fail("غير موجود.", HRErrorCode.NotFound);
        if (l.Status != LeaveStatus.Pending)
            return HRResult<LeaveRequestResponse>.Fail($"لا يمكن رفض إجازة في حالة {l.Status}.", HRErrorCode.InvalidStatusTransition);
        l.Status = LeaveStatus.Rejected;
        l.ApproverId = userId;
        l.ApprovedAt = DateTime.UtcNow;
        l.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(l, ct);
        return HRResult<LeaveRequestResponse>.Ok(MapToResponse(l));
    }

    public async Task<HRResult<LeaveRequestResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var l = await _repo.GetByIdAsync(id, ct);
        if (l == null || l.TenantId != tenantId)
            return HRResult<LeaveRequestResponse>.Fail("غير موجود.", HRErrorCode.NotFound);
        return HRResult<LeaveRequestResponse>.Ok(MapToResponse(l));
    }

    public async Task<HRResult<IReadOnlyList<LeaveRequestResponse>>> ListAsync(Guid tenantId, Guid? employeeId, LeaveStatus? status, int skip, int take, CancellationToken ct)
    {
        if (take is < 1 or > 200) take = 50;
        var list = await _repo.ListAsync(tenantId, employeeId, status, skip, take, ct);
        return HRResult<IReadOnlyList<LeaveRequestResponse>>.Ok(list.Select(MapToResponse).ToList());
    }

    private static LeaveRequestResponse MapToResponse(LeaveRequest l) => new()
    {
        Id = l.Id, EmployeeId = l.EmployeeId, LeaveType = l.LeaveType,
        StartDate = l.StartDate, EndDate = l.EndDate, TotalDays = l.TotalDays,
        Status = l.Status, Reason = l.Reason, ApproverId = l.ApproverId,
        ApprovedAt = l.ApprovedAt, Notes = l.Notes
    };
}
