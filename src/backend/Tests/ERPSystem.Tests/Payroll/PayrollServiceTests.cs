using ERPSystem.Tests.Fakes;
using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Application.Services;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Finance.Infrastructure;
using ERPSystem.Modules.HR.Entities;
using ERPSystem.Modules.HR.Infrastructure;
using ERPSystem.Modules.Payroll.Application;
using ERPSystem.Modules.Payroll.Application.Services;
using ERPSystem.Modules.Payroll.Domain.Calculators;
using ERPSystem.Modules.Payroll.Domain.Entities;
using ERPSystem.Modules.Payroll.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERPSystem.Tests.Payroll;

public class PayrollServiceTests
{
    private static (PayrollService svc,
                    FakePayrollRepository runs,
                    FakeEmployeeRepository employees,
                    FakeAccountRepository accounts,
                    FakeJournalService journal)
        Build(Guid tenantId)
    {
        var runs = new FakePayrollRepository();
        var employees = new FakeEmployeeRepository();
        var accounts = new FakeAccountRepository();
        var journal = new FakeJournalService();

        // Default CoA: 4200 Salary Expense + 1210 Cash
        accounts.Add(new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "4200",
            Name = "G&A Expenses (Salary)", Type = AccountType.Expense,
            NormalBalance = NormalBalance.Debit, IsPostable = true, IsActive = true
        });
        accounts.Add(new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "1210",
            Name = "Cash", Type = AccountType.Asset,
            NormalBalance = NormalBalance.Debit, IsPostable = true, IsActive = true
        });

        var svc = new PayrollService(
            runs, runs, employees,  // runs impls both IPayrollRepository and ISalaryStructureRepository
            new LibyaTaxCalculator(), new SocialInsuranceCalculator(),
            journal, accounts, new FakeDefaultAccountsHelper(), NullLogger<PayrollService>.Instance);

        return (svc, runs, employees, accounts, journal);
    }

    private static Guid NewTenant() => Guid.NewGuid();

    // ---------- CreateRunAsync ----------

    [Fact]
    public async Task CreateRun_DefaultsToDraft_WithCorrectPeriod()
    {
        var tenantId = NewTenant();
        var (svc, runs, _, _, _) = Build(tenantId);

        var start = new DateTime(2026, 6, 1);
        var end = new DateTime(2026, 6, 30);
        var r = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = start, PeriodEnd = end, Notes = "يونيو 2026"
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(PayrollRunStatus.Draft);
        r.Value!.PeriodStart.Should().Be(start);
        r.Value!.PeriodEnd.Should().Be(end);
        r.Value!.Notes.Should().Be("يونيو 2026");
        r.Value!.ItemsCount.Should().Be(0);
        runs.Runs.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateRun_PeriodEndBeforeStart_Fails()
    {
        var (svc, _, _, _, _) = Build(NewTenant());
        var r = await svc.CreateRunAsync(Guid.NewGuid(), Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 30),
            PeriodEnd = new DateTime(2026, 6, 1)
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PayrollErrorCode.ValidationError);
    }

    [Fact]
    public async Task CreateRun_OverlapWithActiveRun_Fails()
    {
        var tenantId = NewTenant();
        var (svc, _, _, _, _) = Build(tenantId);

        // أول دورة
        await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);

        // تتداخل مع الأولى
        var r = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 15), PeriodEnd = new DateTime(2026, 7, 15)
        }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PayrollErrorCode.BusinessRuleViolation);
    }

    [Fact]
    public async Task CreateRun_NoOverlap_Allowed()
    {
        var tenantId = NewTenant();
        var (svc, _, _, _, _) = Build(tenantId);

        await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);

        var r = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 7, 1), PeriodEnd = new DateTime(2026, 7, 31)
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
    }

    // ---------- ProcessRunAsync ----------

    [Fact]
    public async Task ProcessRun_GeneratesPayslip_ForEachActiveEmployee()
    {
        var tenantId = NewTenant();
        var (svc, runs, employees, _, _) = Build(tenantId);

        employees.Add(NewEmployee(tenantId, baseSalary: 1_000m, hireDate: new DateTime(2020, 1, 1)));
        employees.Add(NewEmployee(tenantId, baseSalary: 2_000m, hireDate: new DateTime(2018, 6, 1)));

        var run = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);

        var r = await svc.ProcessRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(PayrollRunStatus.Processing);
        r.Value!.ItemsCount.Should().Be(2);

        var items = runs.Items;
        items.Should().HaveCount(2);
        items.Sum(i => i.GrossSalary).Should().Be(3_000m);
    }

    [Fact]
    public async Task ProcessRun_SkipsTerminatedBeforePeriod()
    {
        var tenantId = NewTenant();
        var (svc, runs, employees, _, _) = Build(tenantId);

        employees.Add(NewEmployee(tenantId, baseSalary: 1_000m, hireDate: new DateTime(2020, 1, 1),
            terminationDate: new DateTime(2026, 5, 31)));  // انتهى قبل فترة يونيو

        var run = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);

        var r = await svc.ProcessRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.ItemsCount.Should().Be(0, "الموظف انتهى قبل بداية الفترة");
        runs.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessRun_SkipsHiredAfterPeriod()
    {
        var tenantId = NewTenant();
        var (svc, runs, employees, _, _) = Build(tenantId);

        employees.Add(NewEmployee(tenantId, baseSalary: 1_000m,
            hireDate: new DateTime(2026, 7, 1)));  // تعيين بعد نهاية الفترة

        var run = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);

        var r = await svc.ProcessRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.ItemsCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessRun_InactiveEmployees_AreSkipped()
    {
        var tenantId = NewTenant();
        var (svc, runs, employees, _, _) = Build(tenantId);

        employees.Add(NewEmployee(tenantId, baseSalary: 1_000m, hireDate: new DateTime(2020, 1, 1), isActive: false));

        var run = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);

        var r = await svc.ProcessRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.ItemsCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessRun_EmptyTenant_StillMovesToProcessing()
    {
        var tenantId = NewTenant();
        var (svc, runs, employees, _, _) = Build(tenantId);
        // لا موظفين

        var run = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);

        var r = await svc.ProcessRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(PayrollRunStatus.Processing);
        r.Value!.ItemsCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessRun_NonExistent_Fails()
    {
        var (svc, _, _, _, _) = Build(NewTenant());
        var r = await svc.ProcessRunAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PayrollErrorCode.NotFound);
    }

    [Fact]
    public async Task ProcessRun_WrongTenant_Fails()
    {
        var tenantId = NewTenant();
        var (svc, _, _, _, _) = Build(tenantId);
        var run = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);

        var r = await svc.ProcessRunAsync(Guid.NewGuid(), Guid.NewGuid(), run.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PayrollErrorCode.NotFound);
    }

    [Fact]
    public async Task ProcessRun_CalculatesTaxAndSocialInsurance()
    {
        var tenantId = NewTenant();
        var (svc, runs, employees, _, _) = Build(tenantId);

        // راتب 1,500 شهرياً → tax=100, social insurance=56.25, net = 1500 - 100 - 56.25 = 1,343.75
        employees.Add(NewEmployee(tenantId, baseSalary: 1_500m, hireDate: new DateTime(2020, 1, 1)));

        var run = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);

        await svc.ProcessRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);

        var item = runs.Items.Single();
        item.BaseSalary.Should().Be(1_500m);
        item.GrossSalary.Should().Be(1_500m);
        item.TaxAmount.Should().Be(100m);
        item.SocialInsuranceEmployee.Should().Be(56.25m);
        item.NetSalary.Should().Be(1_343.75m);
    }

    [Fact]
    public async Task ProcessRun_PopulatesPayslipComponents()
    {
        var tenantId = NewTenant();
        var (svc, runs, employees, _, _) = Build(tenantId);

        employees.Add(NewEmployee(tenantId, baseSalary: 1_000m, hireDate: new DateTime(2020, 1, 1)));

        var run = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);

        await svc.ProcessRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);
        var item = runs.Items.Single();
        var comps = runs.ComponentsByItem[item.Id];

        comps.Should().HaveCount(3, "Basic earning + Tax deduction + Social insurance deduction");
        comps.Should().Contain(c => c.ComponentType == SalaryComponentType.Earning && c.Name == "الراتب الأساسي" && c.Amount == 1_000m);
        comps.Should().Contain(c => c.ComponentType == SalaryComponentType.Deduction && c.Name.Contains("ضريبة"));
        comps.Should().Contain(c => c.ComponentType == SalaryComponentType.Deduction && c.Name.Contains("التأمينات"));
    }

    [Fact]
    public async Task ProcessRun_UpdatesRunTotals()
    {
        var tenantId = NewTenant();
        var (svc, runs, employees, _, _) = Build(tenantId);

        employees.Add(NewEmployee(tenantId, baseSalary: 1_000m, hireDate: new DateTime(2020, 1, 1)));
        employees.Add(NewEmployee(tenantId, baseSalary: 2_000m, hireDate: new DateTime(2018, 6, 1)));

        var run = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);
        await svc.ProcessRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);

        var updated = runs.Runs.Values.Single();
        updated.TotalGross.Should().Be(3_000m);
        updated.TotalNet.Should().BeGreaterThan(0);
        updated.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessRun_Twice_FailsAsInvalidStatusTransition()
    {
        var tenantId = NewTenant();
        var (svc, _, employees, _, _) = Build(tenantId);
        employees.Add(NewEmployee(tenantId, baseSalary: 1_000m, hireDate: new DateTime(2020, 1, 1)));

        var run = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);
        await svc.ProcessRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);

        var r2 = await svc.ProcessRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);
        r2.Succeeded.Should().BeFalse();
        r2.ErrorCode.Should().Be(PayrollErrorCode.InvalidStatusTransition);
    }

    // ---------- PostRunAsync ----------

    [Fact]
    public async Task PostRun_FromDraft_FailsAsInvalidTransition()
    {
        var tenantId = NewTenant();
        var (svc, _, employees, _, _) = Build(tenantId);
        employees.Add(NewEmployee(tenantId, baseSalary: 1_000m, hireDate: new DateTime(2020, 1, 1)));

        var run = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);

        var r = await svc.PostRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PayrollErrorCode.InvalidStatusTransition);
    }

    [Fact]
    public async Task PostRun_AfterProcessing_CreatesJournalEntry_AndMovesToPosted()
    {
        var tenantId = NewTenant();
        var (svc, runs, employees, accounts, journal) = Build(tenantId);
        employees.Add(NewEmployee(tenantId, baseSalary: 1_000m, hireDate: new DateTime(2020, 1, 1)));

        var run = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);
        await svc.ProcessRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);

        var r = await svc.PostRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(PayrollRunStatus.Posted);
        r.Value!.PostedAt.Should().NotBeNull();

        journal.CreateDraftCalls.Should().Be(1, "CreateDraftAsync called once");
        journal.PostCalls.Should().Be(1, "PostAsync called once");

        var posted = runs.Runs.Values.Single();
        posted.Status.Should().Be(PayrollRunStatus.Posted);
        posted.Notes.Should().Contain("JE:");
    }

    [Fact]
    public async Task PostRun_EmptyItems_FailsAsBusinessRule()
    {
        var tenantId = NewTenant();
        var (svc, _, employees, _, _) = Build(tenantId);
        // لا موظفين
        var run = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);
        await svc.ProcessRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);

        var r = await svc.PostRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PayrollErrorCode.BusinessRuleViolation);
    }

    [Fact]
    public async Task PostRun_MissingSalaryAccount_FailsAsBusinessRule()
    {
        var tenantId = NewTenant();
        var (svc, _, employees, accounts, _) = Build(tenantId);
        accounts.Clear();  // حذف CoA الافتراضي
        employees.Add(NewEmployee(tenantId, baseSalary: 1_000m, hireDate: new DateTime(2020, 1, 1)));

        var run = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);
        await svc.ProcessRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);

        var r = await svc.PostRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PayrollErrorCode.BusinessRuleViolation);
    }

    [Fact]
    public async Task PostRun_JournalEntryFailure_PropagatesAsInternal()
    {
        var tenantId = NewTenant();
        var (svc, _, employees, _, journal) = Build(tenantId);
        journal.FailNext = "فشل اختباري";
        employees.Add(NewEmployee(tenantId, baseSalary: 1_000m, hireDate: new DateTime(2020, 1, 1)));

        var run = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);
        await svc.ProcessRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);

        var r = await svc.PostRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PayrollErrorCode.Internal);
    }

    // ---------- GetItemsAsync / GetPayslipAsync ----------

    [Fact]
    public async Task GetItems_ReturnsAllPayslipsWithComponents()
    {
        var tenantId = NewTenant();
        var (svc, _, employees, _, _) = Build(tenantId);
        employees.Add(NewEmployee(tenantId, baseSalary: 1_000m, hireDate: new DateTime(2020, 1, 1)));
        employees.Add(NewEmployee(tenantId, baseSalary: 2_000m, hireDate: new DateTime(2018, 6, 1)));

        var run = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);
        await svc.ProcessRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);

        var items = await svc.GetItemsAsync(tenantId, run.Value!.Id, CancellationToken.None);
        items.Succeeded.Should().BeTrue();
        items.Value!.Should().HaveCount(2);
        items.Value!.Should().OnlyContain(p => p.Components.Count >= 3);
    }

    [Fact]
    public async Task GetPayslip_ForSpecificEmployee_ReturnsItem()
    {
        var tenantId = NewTenant();
        var (svc, _, employees, _, _) = Build(tenantId);
        var emp = NewEmployee(tenantId, baseSalary: 1_500m, hireDate: new DateTime(2020, 1, 1));
        employees.Add(emp);

        var run = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);
        await svc.ProcessRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);

        var slip = await svc.GetPayslipAsync(tenantId, run.Value!.Id, emp.Id, CancellationToken.None);
        slip.Succeeded.Should().BeTrue();
        slip.Value!.EmployeeName.Should().Be(emp.FullName);
        slip.Value!.GrossSalary.Should().Be(1_500m);
    }

    [Fact]
    public async Task GetPayslip_NotInRun_Fails()
    {
        var tenantId = NewTenant();
        var (svc, _, employees, _, _) = Build(tenantId);
        employees.Add(NewEmployee(tenantId, baseSalary: 1_000m, hireDate: new DateTime(2020, 1, 1)));

        var run = await svc.CreateRunAsync(tenantId, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);
        await svc.ProcessRunAsync(tenantId, Guid.NewGuid(), run.Value!.Id, CancellationToken.None);

        var r = await svc.GetPayslipAsync(tenantId, run.Value!.Id, Guid.NewGuid(), CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PayrollErrorCode.NotFound);
    }

    // ---------- ListRunsAsync / GetRunAsync ----------

    [Fact]
    public async Task ListRuns_ReturnsRunsForTenant_Only()
    {
        var tenant1 = NewTenant();
        var tenant2 = NewTenant();
        var (svc1, _, _, _, _) = Build(tenant1);
        var (svc2, _, _, _, _) = Build(tenant2);

        await svc1.CreateRunAsync(tenant1, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);
        await svc2.CreateRunAsync(tenant2, Guid.NewGuid(), new CreatePayrollRunRequest
        {
            PeriodStart = new DateTime(2026, 6, 1), PeriodEnd = new DateTime(2026, 6, 30)
        }, CancellationToken.None);

        var r1 = await svc1.ListRunsAsync(tenant1, null, 0, 50, CancellationToken.None);
        r1.Value!.Should().HaveCount(1);

        var r2 = await svc2.ListRunsAsync(tenant2, null, 0, 50, CancellationToken.None);
        r2.Value!.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetRun_NonExistent_Fails()
    {
        var (svc, _, _, _, _) = Build(NewTenant());
        var r = await svc.GetRunAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PayrollErrorCode.NotFound);
    }

    // ---------- helpers ----------

    private static Employee NewEmployee(Guid tenantId, decimal baseSalary, DateTime hireDate,
        DateTime? terminationDate = null, bool isActive = true)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeNumber = $"EMP-{Guid.NewGuid().ToString()[..6]}",
            FullName = $"موظف {Guid.NewGuid().ToString()[..4]}",
            Email = $"emp{Guid.NewGuid().ToString()[..4]}@example.com",
            HireDate = hireDate,
            TerminationDate = terminationDate,
            BaseSalary = baseSalary,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
}

// ============== Fake Repositories ==============

internal class FakePayrollRepository : IPayrollRepository, ISalaryStructureRepository
{
    public Dictionary<Guid, PayrollRun> Runs { get; } = new();
    public List<PayrollItem> Items { get; } = new();
    public Dictionary<Guid, List<PayslipComponent>> ComponentsByItem { get; } = new();
    public Dictionary<Guid, SalaryStructure> Structures { get; } = new();
    public Dictionary<Guid, List<SalaryStructureLine>> StructureLines { get; } = new();

    // ---------- IPayrollRepository ----------
    public Task<PayrollRun?> GetRunByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Runs.TryGetValue(id, out var r) ? r : null);

    public Task<PayrollRun?> GetRunByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        Task.FromResult(Runs.TryGetValue(id, out var r) && r.TenantId == tenantId ? r : null);

    public Task<IReadOnlyList<PayrollRun>> ListRunsAsync(Guid tenantId, PayrollRunStatus? status, int skip, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PayrollRun>>(Runs.Values
            .Where(r => r.TenantId == tenantId && (status == null || r.Status == status))
            .OrderByDescending(r => r.PeriodStart)
            .ToList());

    public Task InsertRunAsync(PayrollRun run, CancellationToken ct) { Runs[run.Id] = run; return Task.CompletedTask; }
    public Task UpdateRunAsync(PayrollRun run, CancellationToken ct) { Runs[run.Id] = run; return Task.CompletedTask; }

    public Task<PayrollItem?> GetItemByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Id == id));

    public Task<IReadOnlyList<PayrollItem>> GetItemsByRunAsync(Guid payrollRunId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PayrollItem>>(Items.Where(i => i.PayrollRunId == payrollRunId).ToList());

    public Task AddItemAsync(PayrollItem item, IEnumerable<PayslipComponent> components, CancellationToken ct)
    {
        Items.Add(item);
        ComponentsByItem[item.Id] = components.ToList();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PayslipComponent>> GetComponentsByItemAsync(Guid payrollItemId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PayslipComponent>>(ComponentsByItem.TryGetValue(payrollItemId, out var c) ? c : new List<PayslipComponent>());

    public Task<SalaryStructure?> GetStructureByCodeAsync(Guid tenantId, string code, CancellationToken ct) =>
        Task.FromResult(Structures.Values.FirstOrDefault(s => s.TenantId == tenantId && s.Code == code));

    // ---------- ISalaryStructureRepository ----------
    public Task<SalaryStructure?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Structures.TryGetValue(id, out var s) ? s : null);

    public Task<SalaryStructure?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct) =>
        Task.FromResult(Structures.Values.FirstOrDefault(s => s.TenantId == tenantId && s.Code == code));

    public Task<IReadOnlyList<SalaryStructure>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SalaryStructure>>(Structures.Values.Where(s => s.TenantId == tenantId).ToList());

    public Task<IReadOnlyList<SalaryStructureLine>> GetLinesAsync(Guid salaryStructureId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SalaryStructureLine>>(StructureLines.TryGetValue(salaryStructureId, out var l) ? l : new List<SalaryStructureLine>());

    public Task InsertAsync(SalaryStructure structure, IEnumerable<SalaryStructureLine> lines, CancellationToken ct)
    {
        Structures[structure.Id] = structure;
        StructureLines[structure.Id] = lines.ToList();
        return Task.CompletedTask;
    }

    public Task UpdateAsync(SalaryStructure structure, CancellationToken ct)
    {
        Structures[structure.Id] = structure;
        return Task.CompletedTask;
    }

    public Task InsertLineAsync(SalaryStructureLine line, CancellationToken ct)
    {
        if (!StructureLines.TryGetValue(line.SalaryStructureId, out var list))
            StructureLines[line.SalaryStructureId] = list = new List<SalaryStructureLine>();
        list.Add(line);
        return Task.CompletedTask;
    }

    public Task DeleteLineAsync(Guid lineId, CancellationToken ct)
    {
        foreach (var kv in StructureLines)
            kv.Value.RemoveAll(l => l.Id == lineId);
        return Task.CompletedTask;
    }
}

internal class FakeEmployeeRepository : IEmployeeRepository
{
    private readonly Dictionary<Guid, Employee> _items = new();
    public void Add(Employee e) => _items[e.Id] = e;

    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var e) ? e : null);

    public Task<Employee?> GetByNumberAsync(Guid tenantId, string number, CancellationToken ct) =>
        Task.FromResult(_items.Values.FirstOrDefault(e => e.TenantId == tenantId && e.EmployeeNumber == number));

    public Task<Employee?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct) =>
        Task.FromResult(_items.Values.FirstOrDefault(e => e.TenantId == tenantId && e.Email == email));

    public Task<IReadOnlyList<Employee>> ListAsync(Guid tenantId, Guid? departmentId, bool includeInactive, int skip, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Employee>>(_items.Values
            .Where(e => e.TenantId == tenantId && (includeInactive || e.IsActive)
                && (departmentId == null || e.DepartmentId == departmentId))
            .ToList());

    public Task InsertAsync(Employee emp, CancellationToken ct) { _items[emp.Id] = emp; return Task.CompletedTask; }
    public Task UpdateAsync(Employee emp, CancellationToken ct) { _items[emp.Id] = emp; return Task.CompletedTask; }
}

internal class FakeAccountRepository : IAccountRepository
{
    private readonly List<Account> _accounts = new();
    public void Add(Account a) => _accounts.Add(a);
    public void Clear() => _accounts.Clear();

    public Task<Account?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_accounts.FirstOrDefault(a => a.Id == id));
    public Task<Account?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct) =>
        Task.FromResult(_accounts.FirstOrDefault(a => a.TenantId == tenantId && a.Code == code));
    public Task<IReadOnlyList<Account>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Account>>(_accounts.Where(a => a.TenantId == tenantId).ToList());
    public Task<IReadOnlyList<Account>> ListChildrenAsync(Guid parentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Account>>(_accounts.Where(a => a.ParentAccountId == parentId).ToList());
    public Task InsertAsync(Account account, CancellationToken ct) { _accounts.Add(account); return Task.CompletedTask; }
    public Task UpdateAsync(Account account, CancellationToken ct) { var idx = _accounts.FindIndex(a => a.Id == account.Id); if (idx >= 0) _accounts[idx] = account; return Task.CompletedTask; }
    public Task<int> CountPostingsAsync(Guid accountId, CancellationToken ct) => Task.FromResult(0);
    public Task EnsureDefaultCoAAsync(Guid tenantId, Guid companyId, CancellationToken ct) => Task.CompletedTask;
    public Task CloneCoAFromCompanyAsync(Guid targetCompanyId, Guid sourceCompanyId, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<Account>> ListByCompanyAsync(Guid tenantId, Guid? companyId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Account>>(_accounts.Where(a => a.TenantId == tenantId).ToList());
}

internal class FakeJournalService : IJournalEntryService
{
    public int CreateDraftCalls { get; private set; }
    public int PostCalls { get; private set; }
    public string? FailNext { get; set; }

    public Task<FinanceResult<JournalEntryResponse>> CreateDraftAsync(Guid tenantId, Guid userId, PostJournalEntryRequest request, CancellationToken ct)
    {
        CreateDraftCalls++;
        if (FailNext != null)
            return Task.FromResult(FinanceResult<JournalEntryResponse>.Fail(FailNext, FinanceErrorCode.Internal));
        var resp = new JournalEntryResponse
        {
            Id = Guid.NewGuid(),
            EntryNumber = $"JE-{DateTime.UtcNow.Year}-{CreateDraftCalls:D4}",
            EntryDate = request.EntryDate,
            Description = request.Description,
            Reference = request.Reference,
            Status = JournalEntryStatus.Draft,
            TotalDebit = request.Lines.Sum(l => l.Debit),
            TotalCredit = request.Lines.Sum(l => l.Credit)
        };
        return Task.FromResult(FinanceResult<JournalEntryResponse>.Ok(resp));
    }

    public Task<FinanceResult<JournalEntryResponse>> PostAsync(Guid tenantId, Guid userId, Guid entryId, CancellationToken ct)
    {
        PostCalls++;
        if (FailNext != null)
            return Task.FromResult(FinanceResult<JournalEntryResponse>.Fail(FailNext, FinanceErrorCode.Internal));
        var resp = new JournalEntryResponse
        {
            Id = entryId,
            EntryNumber = $"JE-POST-{PostCalls}",
            EntryDate = DateTime.UtcNow,
            Status = JournalEntryStatus.Posted,
            PostedAt = DateTime.UtcNow
        };
        return Task.FromResult(FinanceResult<JournalEntryResponse>.Ok(resp));
    }

    public Task<FinanceResult<JournalEntryResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        Task.FromResult(FinanceResult<JournalEntryResponse>.Fail("not used", FinanceErrorCode.NotFound));

    public Task<FinanceResult<IReadOnlyList<JournalEntryResponse>>> ListAsync(Guid tenantId, DateTime? from, DateTime? to, JournalEntryStatus? status, int skip, int take, CancellationToken ct) =>
        Task.FromResult(FinanceResult<IReadOnlyList<JournalEntryResponse>>.Ok(new List<JournalEntryResponse>()));
}
