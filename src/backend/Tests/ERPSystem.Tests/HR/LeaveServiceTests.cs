using ERPSystem.Modules.HR.Application;
using ERPSystem.Modules.HR.Application.Services;
using ERPSystem.Modules.HR.Entities;
using ERPSystem.Modules.HR.Infrastructure;
using FluentAssertions;

namespace ERPSystem.Tests.HR;

public class LeaveServiceTests
{
    private static (LeaveRequestService svc,
                    FakeEmployeeRepository employees,
                    FakeLeaveRepository leaves)
        Build()
    {
        var employees = new FakeEmployeeRepository();
        var leaves = new FakeLeaveRepository();
        return (new LeaveRequestService(leaves, employees), employees, leaves);
    }

    // ---------- CreateAsync ----------

    [Fact]
    public async Task Create_DefaultsToPending_ComputesTotalDaysInclusive()
    {
        var (svc, employees, _) = Build();
        var tenantId = Guid.NewGuid();
        var emp = NewEmployee(tenantId);
        employees.Add(emp);

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            LeaveType = LeaveType.Annual,
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 5),  // 5 أيام شاملة البداية والنهاية
            Reason = "إجازة عائلية"
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(LeaveStatus.Pending);
        r.Value!.TotalDays.Should().Be(5);
        r.Value!.LeaveType.Should().Be(LeaveType.Annual);
    }

    [Fact]
    public async Task Create_SingleDayLeave_TotalDaysIsOne()
    {
        var (svc, employees, _) = Build();
        var tenantId = Guid.NewGuid();
        var emp = NewEmployee(tenantId);
        employees.Add(emp);

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            LeaveType = LeaveType.Sick,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 1)
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.TotalDays.Should().Be(1);
    }

    [Fact]
    public async Task Create_NonExistentEmployee_Fails()
    {
        var (svc, _, _) = Build();
        var r = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateLeaveRequestDto
        {
            EmployeeId = Guid.NewGuid(),
            LeaveType = LeaveType.Annual,
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 5)
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(HRErrorCode.NotFound);
    }

    [Fact]
    public async Task Create_WrongTenantEmployee_Fails()
    {
        var (svc, employees, _) = Build();
        var emp = NewEmployee(Guid.NewGuid());  // tenant آخر
        employees.Add(emp);

        var r = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            LeaveType = LeaveType.Annual,
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2026, 6, 5)
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(HRErrorCode.NotFound);
    }

    [Fact]
    public async Task Create_OverlapWithApprovedLeave_Fails()
    {
        var (svc, employees, leaves) = Build();
        var tenantId = Guid.NewGuid();
        var emp = NewEmployee(tenantId);
        employees.Add(emp);

        leaves.Add(NewLeave(tenantId, emp.Id,
            start: new DateTime(2026, 6, 1),
            end: new DateTime(2026, 6, 10),
            status: LeaveStatus.Approved));

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            LeaveType = LeaveType.Annual,
            StartDate = new DateTime(2026, 6, 5),   // داخل الفترة المعتمدة
            EndDate = new DateTime(2026, 6, 7)
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(HRErrorCode.BusinessRuleViolation);
    }

    [Fact]
    public async Task Create_OverlapWithPendingLeave_Allowed()
    {
        var (svc, employees, leaves) = Build();
        var tenantId = Guid.NewGuid();
        var emp = NewEmployee(tenantId);
        employees.Add(emp);

        leaves.Add(NewLeave(tenantId, emp.Id,
            start: new DateTime(2026, 6, 1),
            end: new DateTime(2026, 6, 10),
            status: LeaveStatus.Pending));  // Pending — لا تعارض

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            LeaveType = LeaveType.Annual,
            StartDate = new DateTime(2026, 6, 5),
            EndDate = new DateTime(2026, 6, 7)
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue("التعارض فقط مع إجازة Approved، وليس Pending");
    }

    [Fact]
    public async Task Create_AdjacentPeriod_Allowed()
    {
        var (svc, employees, leaves) = Build();
        var tenantId = Guid.NewGuid();
        var emp = NewEmployee(tenantId);
        employees.Add(emp);

        leaves.Add(NewLeave(tenantId, emp.Id,
            start: new DateTime(2026, 6, 1),
            end: new DateTime(2026, 6, 5),
            status: LeaveStatus.Approved));

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            LeaveType = LeaveType.Annual,
            StartDate = new DateTime(2026, 6, 6),   // بعد الأولى بيوم
            EndDate = new DateTime(2026, 6, 10)
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Create_OverlapWithDifferentEmployee_Allowed()
    {
        var (svc, employees, leaves) = Build();
        var tenantId = Guid.NewGuid();
        var emp1 = NewEmployee(tenantId);
        var emp2 = NewEmployee(tenantId);
        employees.Add(emp1); employees.Add(emp2);

        leaves.Add(NewLeave(tenantId, emp2.Id,
            start: new DateTime(2026, 6, 1),
            end: new DateTime(2026, 6, 10),
            status: LeaveStatus.Approved));

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateLeaveRequestDto
        {
            EmployeeId = emp1.Id,
            LeaveType = LeaveType.Annual,
            StartDate = new DateTime(2026, 6, 5),
            EndDate = new DateTime(2026, 6, 7)
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue("تعارض موظف آخر لا يخصّ emp1");
    }

    // ---------- ApproveAsync ----------

    [Fact]
    public async Task Approve_FromPending_TransitionsToApproved_RecordsApprover()
    {
        var (svc, employees, leaves) = Build();
        var tenantId = Guid.NewGuid();
        var emp = NewEmployee(tenantId);
        employees.Add(emp);
        var approver = Guid.NewGuid();

        var leave = NewLeave(tenantId, emp.Id, status: LeaveStatus.Pending);
        leaves.Add(leave);

        var r = await svc.ApproveAsync(tenantId, approver, leave.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(LeaveStatus.Approved);
        r.Value!.ApproverId.Should().Be(approver);
        r.Value!.ApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Approve_AlreadyApproved_Fails()
    {
        var (svc, employees, leaves) = Build();
        var tenantId = Guid.NewGuid();
        var emp = NewEmployee(tenantId);
        employees.Add(emp);
        var leave = NewLeave(tenantId, emp.Id, status: LeaveStatus.Approved);
        leaves.Add(leave);

        var r = await svc.ApproveAsync(tenantId, Guid.NewGuid(), leave.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(HRErrorCode.InvalidStatusTransition);
    }

    [Fact]
    public async Task Approve_RejectedLeave_Fails()
    {
        var (svc, employees, leaves) = Build();
        var tenantId = Guid.NewGuid();
        var emp = NewEmployee(tenantId);
        employees.Add(emp);
        var leave = NewLeave(tenantId, emp.Id, status: LeaveStatus.Rejected);
        leaves.Add(leave);

        var r = await svc.ApproveAsync(tenantId, Guid.NewGuid(), leave.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(HRErrorCode.InvalidStatusTransition);
    }

    [Fact]
    public async Task Approve_NotFound_Fails()
    {
        var (svc, _, _) = Build();
        var r = await svc.ApproveAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(HRErrorCode.NotFound);
    }

    // ---------- RejectAsync ----------

    [Fact]
    public async Task Reject_FromPending_TransitionsToRejected()
    {
        var (svc, employees, leaves) = Build();
        var tenantId = Guid.NewGuid();
        var emp = NewEmployee(tenantId);
        employees.Add(emp);
        var approver = Guid.NewGuid();

        var leave = NewLeave(tenantId, emp.Id, status: LeaveStatus.Pending);
        leaves.Add(leave);

        var r = await svc.RejectAsync(tenantId, approver, leave.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(LeaveStatus.Rejected);
        r.Value!.ApproverId.Should().Be(approver);
    }

    [Fact]
    public async Task Reject_AlreadyApproved_Fails()
    {
        var (svc, employees, leaves) = Build();
        var tenantId = Guid.NewGuid();
        var emp = NewEmployee(tenantId);
        employees.Add(emp);
        var leave = NewLeave(tenantId, emp.Id, status: LeaveStatus.Approved);
        leaves.Add(leave);

        var r = await svc.RejectAsync(tenantId, Guid.NewGuid(), leave.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(HRErrorCode.InvalidStatusTransition);
    }

    [Fact]
    public async Task Reject_NotFound_Fails()
    {
        var (svc, _, _) = Build();
        var r = await svc.RejectAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(HRErrorCode.NotFound);
    }

    // ---------- GetByIdAsync / ListAsync ----------

    [Fact]
    public async Task GetById_WrongTenant_Fails()
    {
        var (svc, employees, leaves) = Build();
        var emp = NewEmployee(Guid.NewGuid());
        employees.Add(emp);
        var leave = NewLeave(Guid.NewGuid(), emp.Id);
        leaves.Add(leave);

        var r = await svc.GetByIdAsync(Guid.NewGuid(), leave.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task List_FiltersByEmployee_AndStatus()
    {
        var (svc, employees, leaves) = Build();
        var tenantId = Guid.NewGuid();
        var emp1 = NewEmployee(tenantId);
        var emp2 = NewEmployee(tenantId);
        employees.Add(emp1); employees.Add(emp2);

        leaves.Add(NewLeave(tenantId, emp1.Id, status: LeaveStatus.Pending));
        leaves.Add(NewLeave(tenantId, emp1.Id, status: LeaveStatus.Approved));
        leaves.Add(NewLeave(tenantId, emp2.Id, status: LeaveStatus.Pending));

        var emp1Pending = await svc.ListAsync(tenantId, emp1.Id, LeaveStatus.Pending, 0, 50, CancellationToken.None);
        emp1Pending.Value!.Should().HaveCount(1);

        var emp1All = await svc.ListAsync(tenantId, emp1.Id, null, 0, 50, CancellationToken.None);
        emp1All.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task List_ClampingTakeToFifty_WhenOutOfRange()
    {
        var (svc, employees, leaves) = Build();
        var tenantId = Guid.NewGuid();
        var emp = NewEmployee(tenantId);
        employees.Add(emp);
        for (var i = 0; i < 60; i++)
            leaves.Add(NewLeave(tenantId, emp.Id));

        var r = await svc.ListAsync(tenantId, null, null, 0, 999, CancellationToken.None);
        r.Value!.Count.Should().BeLessThanOrEqualTo(50);
    }

    // ---------- End-to-end workflow ----------

    [Fact]
    public async Task Workflow_CreateApproveApproveAgain_Fails()
    {
        var (svc, employees, leaves) = Build();
        var tenantId = Guid.NewGuid();
        var emp = NewEmployee(tenantId);
        employees.Add(emp);

        var c = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateLeaveRequestDto
        {
            EmployeeId = emp.Id,
            LeaveType = LeaveType.Annual,
            StartDate = new DateTime(2026, 8, 1),
            EndDate = new DateTime(2026, 8, 3)
        }, CancellationToken.None);

        var a1 = await svc.ApproveAsync(tenantId, Guid.NewGuid(), c.Value!.Id, CancellationToken.None);
        a1.Succeeded.Should().BeTrue();

        var a2 = await svc.ApproveAsync(tenantId, Guid.NewGuid(), c.Value!.Id, CancellationToken.None);
        a2.Succeeded.Should().BeFalse();
        a2.ErrorCode.Should().Be(HRErrorCode.InvalidStatusTransition);
    }

    // ---------- helpers ----------

    private static Employee NewEmployee(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        EmployeeNumber = $"EMP-{Guid.NewGuid().ToString()[..6]}",
        FullName = "موظف",
        HireDate = new DateTime(2024, 1, 1),
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = Guid.NewGuid()
    };

    private static LeaveRequest NewLeave(Guid tenantId, Guid employeeId,
        DateTime? start = null, DateTime? end = null, LeaveStatus status = LeaveStatus.Pending)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeId = employeeId,
            LeaveType = LeaveType.Annual,
            StartDate = start ?? new DateTime(2026, 6, 1),
            EndDate = end ?? new DateTime(2026, 6, 5),
            TotalDays = 5,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
}

// ============== Fakes ==============
// (انظر Fakes.cs للـ FakeEmployeeRepository و FakeLeaveRepository — مُشاركة مع EmployeeServiceTests)
