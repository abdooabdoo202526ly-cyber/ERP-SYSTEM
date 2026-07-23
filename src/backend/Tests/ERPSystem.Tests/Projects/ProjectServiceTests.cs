using ERPSystem.Modules.Companies.Application.Services;
using ERPSystem.Modules.Companies.Entities;
using ERPSystem.Modules.Projects.Application;
using ERPSystem.Modules.Projects.Application.Services;
using ERPSystem.Modules.Projects.Entities;
using ERPSystem.Modules.Projects.Infrastructure;
using ERPSystem.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TaskStatus = ERPSystem.Modules.Projects.Entities.TaskStatus;

namespace ERPSystem.Tests.Projects;

public class ProjectServiceTests
{
    private static (ProjectService svc, FakeProjectRepository projects, FakeCostCenterService costCenters)
        Build()
    {
        var projects = new FakeProjectRepository();
        var budgets = new FakeProjectBudgetRepository();
        var costCenters = new FakeCostCenterService();
        var companies = new FakeCompanyService();
        var svc = new ProjectService(projects, budgets, costCenters, companies, NullLogger<ProjectService>.Instance);
        return (svc, projects, costCenters);
    }

    [Fact]
    public async Task Create_AutoCreatesCostCenter_AndBudget()
    {
        var (svc, projects, costCenters) = Build();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var result = await svc.CreateAsync(tenantId, userId, new CreateProjectRequest
        {
            CompanyId = companyId,
            Code = "PRJ-001",
            Name = "مشروع تجريبي",
            Budget = 100_000m,
            StartDate = DateTime.UtcNow,
        }, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        costCenters.Created.Count.Should().Be(1, "CostCenter يُنشأ تلقائياً");
        costCenters.Created[0].Type.Should().Be(CostCenterType.Project);
        costCenters.Created[0].BudgetAmount.Should().Be(100_000m);

        var project = result.Value!;
        project.CostCenterId.Should().Be(costCenters.Created[0].Id);
        project.Status.Should().Be(ProjectStatus.Planning);
        project.IsActive.Should().BeTrue();
        project.CompanyId.Should().Be(companyId);

        projects.BudgetsByProject[project.Id].Should().NotBeNull("ProjectBudget يُنشأ تلقائياً");
        projects.BudgetsByProject[project.Id].BudgetAmount.Should().Be(100_000m);
    }

    [Fact]
    public async Task Create_DuplicateCode_Fails()
    {
        var (svc, _, _) = Build();
        var tenantId = Guid.NewGuid();
        await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateProjectRequest
        {
            CompanyId = Guid.NewGuid(), Code = "PRJ-001", Name = "A", Budget = 100, StartDate = DateTime.UtcNow
        }, CancellationToken.None);
        var r2 = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateProjectRequest
        {
            CompanyId = Guid.NewGuid(), Code = "PRJ-001", Name = "B", Budget = 200, StartDate = DateTime.UtcNow
        }, CancellationToken.None);
        r2.Succeeded.Should().BeFalse();
        r2.ErrorCode.Should().Be(ProjectErrorCode.AlreadyExists);
    }

    [Fact]
    public async Task ChangeStatus_PlanningToActive_Succeeds()
    {
        var (svc, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var p = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateProjectRequest
        {
            CompanyId = Guid.NewGuid(), Code = "P1", Name = "X", Budget = 1000, StartDate = DateTime.UtcNow
        }, CancellationToken.None);
        var r = await svc.ChangeStatusAsync(tenantId, Guid.NewGuid(), p.Value!.Id, ProjectStatus.Active, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public async Task ChangeStatus_ActiveToPlanning_Fails()
    {
        var (svc, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var p = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateProjectRequest
        {
            CompanyId = Guid.NewGuid(), Code = "P2", Name = "X", Budget = 1000, StartDate = DateTime.UtcNow
        }, CancellationToken.None);
        await svc.ChangeStatusAsync(tenantId, Guid.NewGuid(), p.Value!.Id, ProjectStatus.Active, CancellationToken.None);
        var r = await svc.ChangeStatusAsync(tenantId, Guid.NewGuid(), p.Value!.Id, ProjectStatus.Planning, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ProjectErrorCode.InvalidStatusTransition);
    }

    [Fact]
    public async Task ChangeStatus_ActiveToCompleted_ThenToActive_Fails()
    {
        var (svc, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var p = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateProjectRequest
        {
            CompanyId = Guid.NewGuid(), Code = "P2c", Name = "X", Budget = 1000, StartDate = DateTime.UtcNow
        }, CancellationToken.None);
        await svc.ChangeStatusAsync(tenantId, Guid.NewGuid(), p.Value!.Id, ProjectStatus.Active, CancellationToken.None);
        await svc.ChangeStatusAsync(tenantId, Guid.NewGuid(), p.Value!.Id, ProjectStatus.Completed, CancellationToken.None);
        var r = await svc.ChangeStatusAsync(tenantId, Guid.NewGuid(), p.Value!.Id, ProjectStatus.Active, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ProjectErrorCode.InvalidStatusTransition);
    }

    [Fact]
    public async Task Deactivate_SetsIsActiveFalse()
    {
        var (svc, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var p = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateProjectRequest
        {
            CompanyId = Guid.NewGuid(), Code = "P3", Name = "X", Budget = 100, StartDate = DateTime.UtcNow
        }, CancellationToken.None);
        var r = await svc.DeactivateAsync(tenantId, Guid.NewGuid(), p.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        var fetched = await svc.GetByIdAsync(tenantId, p.Value!.Id, CancellationToken.None);
        fetched.Value!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_WrongTenant_Fails()
    {
        var (svc, _, _) = Build();
        var p = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateProjectRequest
        {
            CompanyId = Guid.NewGuid(), Code = "P4", Name = "X", Budget = 100, StartDate = DateTime.UtcNow
        }, CancellationToken.None);
        var r = await svc.GetByIdAsync(Guid.NewGuid(), p.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(ProjectErrorCode.NotFound);
    }

    [Fact]
    public async Task List_FiltersByCompany()
    {
        var (svc, _, _) = Build();
        var tenantId = Guid.NewGuid();
        var comp1 = Guid.NewGuid();
        var comp2 = Guid.NewGuid();
        await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateProjectRequest { CompanyId = comp1, Code = "C1A", Name = "A", Budget = 1, StartDate = DateTime.UtcNow }, CancellationToken.None);
        await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateProjectRequest { CompanyId = comp1, Code = "C1B", Name = "B", Budget = 1, StartDate = DateTime.UtcNow }, CancellationToken.None);
        await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateProjectRequest { CompanyId = comp2, Code = "C2A", Name = "C", Budget = 1, StartDate = DateTime.UtcNow }, CancellationToken.None);
        var r = await svc.ListAsync(tenantId, comp1, null, true, 0, 50, CancellationToken.None);
        r.Value!.Count.Should().Be(2);
    }
}

public class TaskServiceTests
{
    private static async Task<ProjectResponse> CreateProjectAsync(FakeProjectRepository repo, string code = "P-DEFAULT")
    {
        var ccService = new FakeCostCenterService();
        var projectSvc = new ProjectService(repo, new FakeProjectBudgetRepository(), ccService, new FakeCompanyService(), NullLogger<ProjectService>.Instance);
        var r = await projectSvc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateProjectRequest
        {
            CompanyId = Guid.NewGuid(), Code = code, Name = "X", Budget = 100, StartDate = DateTime.UtcNow
        }, CancellationToken.None);
        return r.Value!;
    }

    [Fact]
    public async Task Create_DefaultsToNotStarted_ZeroProgress()
    {
        var tasks = new FakeTaskRepository();
        var projects = new FakeProjectRepository();
        var p = await CreateProjectAsync(projects);
        var svc = new TaskService(tasks);
        var r = await svc.CreateAsync(p.TenantId, new CreateTaskRequest
        {
            ProjectId = p.Id, Name = "مهمة 1", EstimatedHours = 8
        }, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(TaskStatus.NotStarted);
        r.Value.ProgressPercent.Should().Be(0);
    }

    [Fact]
    public async Task Update_ChangesProgressAndStatus()
    {
        var tasks = new FakeTaskRepository();
        var projects = new FakeProjectRepository();
        var p = await CreateProjectAsync(projects);
        var svc = new TaskService(tasks);
        var t = await svc.CreateAsync(p.TenantId, new CreateTaskRequest { ProjectId = p.Id, Name = "T", EstimatedHours = 4 }, CancellationToken.None);
        var r = await svc.UpdateAsync(p.TenantId, t.Value!.Id, new UpdateTaskRequest
        {
            Name = "T", Status = TaskStatus.InProgress, EstimatedHours = 4, ActualHours = 2, ProgressPercent = 50
        }, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(TaskStatus.InProgress);
        r.Value!.ProgressPercent.Should().Be(50);
        r.Value!.ActualHours.Should().Be(2);
    }

    [Fact]
    public async Task ListByProject_OnlyReturnsProjectTasks()
    {
        var tasks = new FakeTaskRepository();
        var projects = new FakeProjectRepository();
        var p1 = await CreateProjectAsync(projects, "P-A");
        var p2 = await CreateProjectAsync(projects, "P-B");
        var svc = new TaskService(tasks);
        await svc.CreateAsync(p1.TenantId, new CreateTaskRequest { ProjectId = p1.Id, Name = "T1", EstimatedHours = 1 }, CancellationToken.None);
        await svc.CreateAsync(p1.TenantId, new CreateTaskRequest { ProjectId = p1.Id, Name = "T2", EstimatedHours = 2 }, CancellationToken.None);
        await svc.CreateAsync(p2.TenantId, new CreateTaskRequest { ProjectId = p2.Id, Name = "T3", EstimatedHours = 3 }, CancellationToken.None);
        var r = await svc.ListByProjectAsync(p1.TenantId, p1.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Count.Should().Be(2);
    }

    [Fact]
    public async Task Delete_RemovesTask()
    {
        var tasks = new FakeTaskRepository();
        var projects = new FakeProjectRepository();
        var p = await CreateProjectAsync(projects);
        var svc = new TaskService(tasks);
        var t = await svc.CreateAsync(p.TenantId, new CreateTaskRequest { ProjectId = p.Id, Name = "T", EstimatedHours = 1 }, CancellationToken.None);
        var r = await svc.DeleteAsync(p.TenantId, t.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        var fetched = await svc.GetByIdAsync(p.TenantId, t.Value!.Id, CancellationToken.None);
        fetched.Succeeded.Should().BeFalse();
    }
}

public class ResourceServiceTests
{
    [Fact]
    public async Task Create_DefaultsToActive()
    {
        var svc = new ResourceService(new FakeResourceRepository());
        var r = await svc.CreateAsync(Guid.NewGuid(), new CreateResourceRequest
        {
            Code = "RES-001", Name = "عامل 1", Type = ResourceType.Labor, HourlyRate = 50
        }, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_DuplicateCode_Fails()
    {
        var repo = new FakeResourceRepository();
        var svc = new ResourceService(repo);
        var t = Guid.NewGuid();
        await svc.CreateAsync(t, new CreateResourceRequest { Code = "R1", Name = "A", Type = ResourceType.Labor, HourlyRate = 1 }, CancellationToken.None);
        var r = await svc.CreateAsync(t, new CreateResourceRequest { Code = "R1", Name = "B", Type = ResourceType.Labor, HourlyRate = 2 }, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Deactivate_SetsIsActiveFalse()
    {
        var repo = new FakeResourceRepository();
        var svc = new ResourceService(repo);
        var t = Guid.NewGuid();
        var r = await svc.CreateAsync(t, new CreateResourceRequest { Code = "R-Active", Name = "X", Type = ResourceType.Labor, HourlyRate = 1 }, CancellationToken.None);
        var u = await svc.UpdateAsync(t, r.Value!.Id, new UpdateResourceRequest
        {
            Name = "X", Type = ResourceType.Labor, HourlyRate = 1, IsActive = false
        }, CancellationToken.None);
        u.Succeeded.Should().BeTrue();
        u.Value!.IsActive.Should().BeFalse();
    }
}

public class BudgetServiceTests
{
    private static async Task<ProjectResponse> CreateProjectAsync()
    {
        var repo = new FakeProjectRepository();
        var ccService = new FakeCostCenterService();
        var projectSvc = new ProjectService(repo, new FakeProjectBudgetRepository(), ccService, new FakeCompanyService(), NullLogger<ProjectService>.Instance);
        var r = await projectSvc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateProjectRequest
        {
            CompanyId = Guid.NewGuid(), Code = "PB", Name = "B", Budget = 1000, StartDate = DateTime.UtcNow
        }, CancellationToken.None);
        return r.Value!;
    }

    [Fact]
    public async Task GetByProject_RecalculatesAfterManualUpdate()
    {
        // Setup: share the budget repo between ProjectService and BudgetService
        var projects = new FakeProjectRepository();
        var budgets = new FakeProjectBudgetRepository();
        var ccService = new FakeCostCenterService();
        var projectSvc = new ProjectService(projects, budgets, ccService, new FakeCompanyService(), NullLogger<ProjectService>.Instance);
        var r = await projectSvc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateProjectRequest
        {
            CompanyId = Guid.NewGuid(), Code = "PB-RC", Name = "B", Budget = 1000, StartDate = DateTime.UtcNow
        }, CancellationToken.None);
        var p = r.Value!;
        // Manually set spent (simulating posted journal lines)
        var budget = await budgets.GetByProjectAsync(p.Id, CancellationToken.None);
        budget!.SpentAmount = 250;
        await budgets.UpdateAsync(budget, CancellationToken.None);

        var svc = new BudgetService(budgets);
        var fetched = await svc.GetByProjectAsync(p.TenantId, p.Id, CancellationToken.None);
        fetched.Succeeded.Should().BeTrue();
        fetched.Value!.SpentAmount.Should().Be(250);
        fetched.Value.UtilizationPercent.Should().Be(25);
    }

    [Fact]
    public void ProjectBudget_AvailableAmount_CalculatesCorrectly()
    {
        var b = new ProjectBudget { BudgetAmount = 1000, SpentAmount = 300, CommittedAmount = 100 };
        b.AvailableAmount.Should().Be(600);
        b.UtilizationPercent.Should().Be(30);
    }

    [Fact]
    public void ProjectBudget_ZeroBudget_HandlesGracefully()
    {
        var b = new ProjectBudget { BudgetAmount = 0, SpentAmount = 0, CommittedAmount = 0 };
        b.AvailableAmount.Should().Be(0);
        b.UtilizationPercent.Should().Be(0);
    }
}

public class ResourceAssignmentComputedTests
{
    [Fact]
    public void EstimatedHoursAndCost_ComputedCorrectly()
    {
        var a = new ResourceAssignment
        {
            From = DateTime.UtcNow,
            To = DateTime.UtcNow.AddHours(10),
            HourlyRate = 50,
        };
        // TimeSpan.TotalHours returns double-precision; we cast to decimal with potential rounding
        // The exact value is 10.000000000x; we accept any value close to 10
        a.EstimatedHours.Should().BeGreaterThan(9.9m).And.BeLessThan(10.1m);
        a.EstimatedCost.Should().BeGreaterThan(490m).And.BeLessThan(510m);
    }

    [Fact]
    public void EstimatedHours_HandlesInvertedRange()
    {
        var a = new ResourceAssignment { From = DateTime.UtcNow, To = DateTime.UtcNow.AddHours(-5), HourlyRate = 10 };
        a.EstimatedHours.Should().Be(0, "ما قبل البداية لا يحسب");
    }
}

// ============== Fakes ==============

internal class FakeProjectRepository : IProjectRepository
{
    private readonly Dictionary<Guid, Project> _items = new();
    public Dictionary<Guid, ProjectBudget> BudgetsByProject { get; } = new();

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var p) ? p : null);

    public Task<Project?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct) =>
        Task.FromResult(_items.Values.FirstOrDefault(p => p.TenantId == tenantId && p.Code == code));

    public Task<IReadOnlyList<Project>> ListAsync(Guid tenantId, Guid? companyId, ProjectStatus? status, bool includeInactive, int skip, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Project>>(_items.Values
            .Where(p => p.TenantId == tenantId && (includeInactive || p.IsActive)
                && (companyId == null || p.CompanyId == companyId)
                && (status == null || p.Status == status))
            .ToList());

    public Task InsertAsync(Project project, CancellationToken ct)
    {
        _items[project.Id] = project;
        BudgetsByProject[project.Id] = new ProjectBudget
        {
            Id = Guid.NewGuid(), TenantId = project.TenantId, ProjectId = project.Id,
            CostCenterId = project.CostCenterId, BudgetAmount = project.Budget,
            SpentAmount = 0, CommittedAmount = 0, LastRecalculatedAt = DateTime.UtcNow
        };
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Project project, CancellationToken ct)
    {
        _items[project.Id] = project;
        if (BudgetsByProject.TryGetValue(project.Id, out var b)) b.BudgetAmount = project.Budget;
        return Task.CompletedTask;
    }
}

internal class FakeTaskRepository : ITaskRepository
{
    private readonly Dictionary<Guid, ProjectTask> _items = new();
    public Task<ProjectTask?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var t) ? t : null);
    public Task<IReadOnlyList<ProjectTask>> ListByProjectAsync(Guid projectId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProjectTask>>(_items.Values.Where(t => t.ProjectId == projectId).ToList());
    public Task InsertAsync(ProjectTask task, CancellationToken ct) { _items[task.Id] = task; return Task.CompletedTask; }
    public Task UpdateAsync(ProjectTask task, CancellationToken ct) { _items[task.Id] = task; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct) { _items.Remove(id); return Task.CompletedTask; }
}

internal class FakeResourceRepository : IResourceRepository
{
    private readonly Dictionary<Guid, Resource> _items = new();
    public Task<Resource?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var r) ? r : null);
    public Task<Resource?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct) =>
        Task.FromResult(_items.Values.FirstOrDefault(r => r.TenantId == tenantId && r.Code == code));
    public Task<IReadOnlyList<Resource>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Resource>>(_items.Values.Where(r => r.TenantId == tenantId).ToList());
    public Task InsertAsync(Resource r, CancellationToken ct) { _items[r.Id] = r; return Task.CompletedTask; }
    public Task UpdateAsync(Resource r, CancellationToken ct) { _items[r.Id] = r; return Task.CompletedTask; }
}

internal class FakeProjectBudgetRepository : IProjectBudgetRepository
{
    private readonly Dictionary<Guid, ProjectBudget> _items = new();
    public Task<ProjectBudget?> GetByProjectAsync(Guid projectId, CancellationToken ct) =>
        Task.FromResult(_items.Values.FirstOrDefault(b => b.ProjectId == projectId));
    public Task<ProjectBudget?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var b) ? b : null);
    public Task InsertAsync(ProjectBudget b, CancellationToken ct) { _items[b.Id] = b; return Task.CompletedTask; }
    public Task UpdateAsync(ProjectBudget b, CancellationToken ct) { _items[b.Id] = b; return Task.CompletedTask; }
    public Task<decimal> RecalculateSpentAsync(Guid projectId, Guid costCenterId, CancellationToken ct) => Task.FromResult(0m);
}

internal class FakeResourceAssignmentRepository : IResourceAssignmentRepository
{
    private readonly Dictionary<Guid, ResourceAssignment> _items = new();
    public Task<ResourceAssignment?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var a) ? a : null);
    public Task<IReadOnlyList<ResourceAssignment>> ListByProjectAsync(Guid projectId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ResourceAssignment>>(_items.Values.Where(a => a.ProjectId == projectId).ToList());
    public Task InsertAsync(ResourceAssignment a, CancellationToken ct) { _items[a.Id] = a; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct) { _items.Remove(id); return Task.CompletedTask; }
}

internal class FakeCostCenterService : ICostCenterService
{
    public List<CostCenter> Created { get; } = new();
    public Task<CostCenterResult<CostCenter>> UpdateAsync(Guid tenantId, Guid id, CreateCostCenterRequest req, CancellationToken ct)
    {
        var cc = Created.First(c => c.Id == id);
        cc.Name = req.Name;
        cc.Type = req.Type;
        if (req.BudgetAmount.HasValue) cc.BudgetAmount = req.BudgetAmount;
        cc.UpdatedAt = DateTime.UtcNow;
        return Task.FromResult(CostCenterResult<CostCenter>.Ok(cc));
    }
    public Task<CostCenterResult<CostCenter>> CreateAsync(Guid tenantId, CreateCostCenterRequest req, CancellationToken ct)
    {
        var cc = new CostCenter
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = req.CompanyId, Code = req.Code, Name = req.Name,
            Type = req.Type, BudgetAmount = req.BudgetAmount, StartDate = req.StartDate, EndDate = req.EndDate,
            Sku = req.Sku, Location = req.Location, ActivityCategory = req.ActivityCategory,
            IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        Created.Add(cc);
        return Task.FromResult(CostCenterResult<CostCenter>.Ok(cc));
    }
    public Task<CostCenterResult<CostCenter>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        Task.FromResult(CostCenterResult<CostCenter>.Ok(Created.First(c => c.Id == id)));
    public Task<CostCenterResult<IReadOnlyList<CostCenter>>> ListAsync(Guid tenantId, Guid? companyId, CostCenterType? type, bool includeInactive, CancellationToken ct) =>
        Task.FromResult(CostCenterResult<IReadOnlyList<CostCenter>>.Ok(Created));
    public Task<CostCenterResult<IReadOnlyList<CostCenter>>> GetChildrenAsync(Guid parentId, CancellationToken ct) =>
        Task.FromResult(CostCenterResult<IReadOnlyList<CostCenter>>.Ok(new List<CostCenter>()));
    public Task<CostCenterResult<CostCenterBudgetStatus>> GetBudgetStatusAsync(Guid tenantId, Guid costCenterId, DateTime? asOf, CancellationToken ct) =>
        Task.FromResult(CostCenterResult<CostCenterBudgetStatus>.Ok(new CostCenterBudgetStatus()));
    public Task<CostCenterResult<bool>> DeactivateAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        Task.FromResult(CostCenterResult<bool>.Ok(true));
}

