using ERPSystem.Modules.HR.Application;
using ERPSystem.Modules.HR.Application.Services;
using ERPSystem.Modules.HR.Entities;
using ERPSystem.Modules.HR.Infrastructure;
using FluentAssertions;

namespace ERPSystem.Tests.HR;

public class EmployeeServiceTests
{
    private static (EmployeeService svc, FakeEmployeeRepository repo, FakeHRSequence seq) Build()
    {
        var repo = new FakeEmployeeRepository();
        var seq = new FakeHRSequence();
        return (new EmployeeService(repo, seq), repo, seq);
    }

    // ---------- CreateAsync ----------

    [Fact]
    public async Task Create_GeneratesSequentialEmployeeNumber_AndDefaultsToActive()
    {
        var (svc, _, seq) = Build();
        var tenantId = Guid.NewGuid();

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateEmployeeRequest
        {
            FullName = "أحمد المنصوري",
            Email = "ahmed@example.com",
            Phone = "0911234567",
            NationalId = "1234567890",
            JobTitle = "محاسب",
            HireDate = new DateTime(2026, 1, 1),
            BaseSalary = 1_500m
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.EmployeeNumber.Should().StartWith("EMP-");
        r.Value!.IsActive.Should().BeTrue();
        r.Value!.FullName.Should().Be("أحمد المنصوري");
        r.Value!.BaseSalary.Should().Be(1_500m);
    }

    [Fact]
    public async Task Create_DuplicateEmail_Fails()
    {
        var repo = new FakeEmployeeRepository();
        var svc = new EmployeeService(repo, new FakeHRSequence());
        var tenantId = Guid.NewGuid();

        await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateEmployeeRequest
        {
            FullName = "A", Email = "dup@example.com", HireDate = DateTime.UtcNow, BaseSalary = 1_000m
        }, CancellationToken.None);

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateEmployeeRequest
        {
            FullName = "B", Email = "dup@example.com", HireDate = DateTime.UtcNow, BaseSalary = 2_000m
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(HRErrorCode.AlreadyExists);
    }

    [Fact]
    public async Task Create_DuplicateEmail_DifferentTenant_Allowed()
    {
        var repo = new FakeEmployeeRepository();
        var svc = new EmployeeService(repo, new FakeHRSequence());

        var r1 = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateEmployeeRequest
        {
            FullName = "A", Email = "shared@example.com", HireDate = DateTime.UtcNow, BaseSalary = 1_000m
        }, CancellationToken.None);
        var r2 = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateEmployeeRequest
        {
            FullName = "B", Email = "shared@example.com", HireDate = DateTime.UtcNow, BaseSalary = 1_000m
        }, CancellationToken.None);

        r1.Succeeded.Should().BeTrue();
        r2.Succeeded.Should().BeTrue("multi-tenancy: نفس الإيميل مسموح في tenant آخر");
    }

    [Fact]
    public async Task Create_NoEmail_Allowed()
    {
        var (svc, _, _) = Build();
        var r = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateEmployeeRequest
        {
            FullName = "بدون إيميل", HireDate = DateTime.UtcNow, BaseSalary = 1_000m
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.Email.Should().BeNull();
    }

    [Fact]
    public async Task Create_TrimsFullName()
    {
        var (svc, _, _) = Build();
        var r = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateEmployeeRequest
        {
            FullName = "  محمد علي  ", HireDate = DateTime.UtcNow, BaseSalary = 1_000m
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.FullName.Should().Be("محمد علي");
    }

    [Fact]
    public async Task Create_TwoEmployeesInSameTenant_UniqueEmployeeNumbers()
    {
        var (svc, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var r1 = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateEmployeeRequest
        {
            FullName = "A", HireDate = DateTime.UtcNow, BaseSalary = 1m
        }, CancellationToken.None);
        var r2 = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateEmployeeRequest
        {
            FullName = "B", HireDate = DateTime.UtcNow, BaseSalary = 1m
        }, CancellationToken.None);

        r1.Value!.EmployeeNumber.Should().NotBe(r2.Value!.EmployeeNumber);
    }

    // ---------- UpdateAsync ----------

    [Fact]
    public async Task Update_ChangesFields_PreservesEmployeeNumber()
    {
        var (svc, repo, _) = Build();
        var tenantId = Guid.NewGuid();
        var originalNumber = "EMP-FIXED-001";
        var emp = NewEmployee(tenantId, employeeNumber: originalNumber, email: "old@example.com");
        repo.Add(emp);

        var r = await svc.UpdateAsync(tenantId, Guid.NewGuid(), emp.Id, new UpdateEmployeeRequest
        {
            FullName = "اسم جديد", Email = "new@example.com", BaseSalary = 2_500m, IsActive = true
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.EmployeeNumber.Should().Be(originalNumber, "الرقم لا يتغير");
        r.Value!.FullName.Should().Be("اسم جديد");
        r.Value!.BaseSalary.Should().Be(2_500m);
    }

    [Fact]
    public async Task Update_NonExistent_Fails()
    {
        var (svc, _, _) = Build();
        var r = await svc.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new UpdateEmployeeRequest
        {
            FullName = "X", BaseSalary = 1m, IsActive = true
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(HRErrorCode.NotFound);
    }

    [Fact]
    public async Task Update_WrongTenant_Fails()
    {
        var (svc, repo, _) = Build();
        var emp = NewEmployee(Guid.NewGuid(), employeeNumber: "X");
        repo.Add(emp);

        var r = await svc.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), emp.Id, new UpdateEmployeeRequest
        {
            FullName = "X", BaseSalary = 1m, IsActive = true
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(HRErrorCode.NotFound);
    }

    [Fact]
    public async Task Update_EmailToExistingOne_Fails()
    {
        var (svc, repo, _) = Build();
        var tenantId = Guid.NewGuid();
        var e1 = NewEmployee(tenantId, employeeNumber: "E1", email: "a@x.com");
        var e2 = NewEmployee(tenantId, employeeNumber: "E2", email: "b@x.com");
        repo.Add(e1); repo.Add(e2);

        var r = await svc.UpdateAsync(tenantId, Guid.NewGuid(), e1.Id, new UpdateEmployeeRequest
        {
            FullName = "X", Email = "b@x.com", BaseSalary = 1m, IsActive = true
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(HRErrorCode.AlreadyExists);
    }

    [Fact]
    public async Task Update_KeepingSameEmail_Allowed()
    {
        var (svc, repo, _) = Build();
        var tenantId = Guid.NewGuid();
        var e = NewEmployee(tenantId, employeeNumber: "E", email: "same@x.com");
        repo.Add(e);

        var r = await svc.UpdateAsync(tenantId, Guid.NewGuid(), e.Id, new UpdateEmployeeRequest
        {
            FullName = "X", Email = "same@x.com", BaseSalary = 1m, IsActive = true
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
    }

    // ---------- GetByIdAsync ----------

    [Fact]
    public async Task GetById_WrongTenant_Fails()
    {
        var (svc, repo, _) = Build();
        var emp = NewEmployee(Guid.NewGuid(), employeeNumber: "X");
        repo.Add(emp);

        var r = await svc.GetByIdAsync(Guid.NewGuid(), emp.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(HRErrorCode.NotFound);
    }

    [Fact]
    public async Task GetById_NotFound_Fails()
    {
        var (svc, _, _) = Build();
        var r = await svc.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(HRErrorCode.NotFound);
    }

    [Fact]
    public async Task GetById_CorrectTenant_Returns()
    {
        var (svc, repo, _) = Build();
        var tenantId = Guid.NewGuid();
        var emp = NewEmployee(tenantId, employeeNumber: "E-001", fullName: "اسم");
        repo.Add(emp);

        var r = await svc.GetByIdAsync(tenantId, emp.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.FullName.Should().Be("اسم");
    }

    // ---------- ListAsync ----------

    [Fact]
    public async Task List_FiltersByDepartment()
    {
        var (svc, repo, _) = Build();
        var tenantId = Guid.NewGuid();
        var deptA = Guid.NewGuid();
        var deptB = Guid.NewGuid();
        repo.Add(NewEmployee(tenantId, employeeNumber: "A1", departmentId: deptA));
        repo.Add(NewEmployee(tenantId, employeeNumber: "A2", departmentId: deptA));
        repo.Add(NewEmployee(tenantId, employeeNumber: "B1", departmentId: deptB));

        var r = await svc.ListAsync(tenantId, deptA, includeInactive: true, skip: 0, take: 50, CancellationToken.None);
        r.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task List_DefaultsToActiveOnly_UnlessIncludeInactive()
    {
        var (svc, repo, _) = Build();
        var tenantId = Guid.NewGuid();
        repo.Add(NewEmployee(tenantId, employeeNumber: "A", isActive: true));
        repo.Add(NewEmployee(tenantId, employeeNumber: "B", isActive: false));

        var activeOnly = await svc.ListAsync(tenantId, null, includeInactive: false, skip: 0, take: 50, CancellationToken.None);
        activeOnly.Value!.Should().HaveCount(1);

        var all = await svc.ListAsync(tenantId, null, includeInactive: true, skip: 0, take: 50, CancellationToken.None);
        all.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task List_ClampingTakeToFifty_WhenOutOfRange()
    {
        var (svc, repo, _) = Build();
        var tenantId = Guid.NewGuid();
        for (var i = 0; i < 60; i++)
            repo.Add(NewEmployee(tenantId, employeeNumber: $"E-{i:D3}"));

        var r = await svc.ListAsync(tenantId, null, true, 0, 999, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Count.Should().BeLessThanOrEqualTo(50, "take > 200 يُقص إلى 50");
    }

    [Fact]
    public async Task List_ClampingTakeToFifty_WhenZero()
    {
        var (svc, repo, _) = Build();
        var tenantId = Guid.NewGuid();
        repo.Add(NewEmployee(tenantId, employeeNumber: "E1"));

        var r = await svc.ListAsync(tenantId, null, true, 0, 0, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Count.Should().BeLessThanOrEqualTo(50, "take < 1 يُقص إلى 50");
    }

    // ---------- DeactivateAsync ----------

    [Fact]
    public async Task Deactivate_SetsIsActiveFalse_AndTerminationDate()
    {
        var (svc, repo, _) = Build();
        var tenantId = Guid.NewGuid();
        var emp = NewEmployee(tenantId, employeeNumber: "X");
        repo.Add(emp);

        var r = await svc.DeactivateAsync(tenantId, Guid.NewGuid(), emp.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();

        var fetched = await svc.GetByIdAsync(tenantId, emp.Id, CancellationToken.None);
        fetched.Value!.IsActive.Should().BeFalse();
        fetched.Value!.TerminationDate.Should().NotBeNull();
    }

    [Fact]
    public async Task Deactivate_PreservesExistingTerminationDate()
    {
        var (svc, repo, _) = Build();
        var tenantId = Guid.NewGuid();
        var existing = new DateTime(2026, 5, 1);
        var emp = NewEmployee(tenantId, employeeNumber: "X", terminationDate: existing);
        repo.Add(emp);

        var r = await svc.DeactivateAsync(tenantId, Guid.NewGuid(), emp.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();

        var fetched = await svc.GetByIdAsync(tenantId, emp.Id, CancellationToken.None);
        fetched.Value!.TerminationDate.Should().Be(existing, "تاريخ التركيب لا يُغيَّر لو موجود مسبقاً");
    }

    [Fact]
    public async Task Deactivate_NotFound_Fails()
    {
        var (svc, _, _) = Build();
        var r = await svc.DeactivateAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(HRErrorCode.NotFound);
    }

    // ---------- helpers ----------

    private static Employee NewEmployee(Guid tenantId, string employeeNumber, string? email = null,
        Guid? departmentId = null, bool isActive = true, string fullName = "موظف", DateTime? terminationDate = null)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeNumber = employeeNumber,
            FullName = fullName,
            Email = email,
            DepartmentId = departmentId,
            HireDate = new DateTime(2024, 1, 1),
            TerminationDate = terminationDate,
            BaseSalary = 1_000m,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
}

// ============== Fakes ==============
// (انظر Fakes.cs للـ FakeEmployeeRepository و FakeHRSequence — مُشاركة مع LeaveServiceTests)
