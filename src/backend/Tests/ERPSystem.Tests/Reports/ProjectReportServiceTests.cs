using ERPSystem.Modules.Projects.Entities;
using ERPSystem.Modules.Projects.Infrastructure;
using ERPSystem.Modules.Reports.Application;
using ERPSystem.Modules.Reports.Application.Services;
using ERPSystem.Tests.Common;
using FluentAssertions;

namespace ERPSystem.Tests.Reports;

/// <summary>
/// اختبارات خدمة تقارير Project.
///
/// DTO Unit Tests (تشتغل دائماً) + Service Tests marked Skip
/// (تتطلب Postgres حقيقي + Fake repos).
/// </summary>
public class ProjectReportServiceTests
{
    private sealed class FakeProjectRepository : IProjectRepository
    {
        public Dictionary<Guid, Project> Items { get; } = new();
        public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Items.TryGetValue(id, out var p) ? p : null);
        public Task<Project?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct) =>
            Task.FromResult(Items.Values.FirstOrDefault(p => p.TenantId == tenantId && p.Code == code));
        public Task<IReadOnlyList<Project>> ListAsync(Guid tenantId, Guid? companyId, ProjectStatus? status, bool includeInactive, int skip, int take, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Project>>(Items.Values.Where(p => p.TenantId == tenantId).ToList());
        public Task InsertAsync(Project project, CancellationToken ct) { Items[project.Id] = project; return Task.CompletedTask; }
        public Task UpdateAsync(Project project, CancellationToken ct) { Items[project.Id] = project; return Task.CompletedTask; }
    }

    private sealed class FakeProjectBudgetRepository : IProjectBudgetRepository
    {
        public Dictionary<Guid, ProjectBudget> Items { get; } = new();
        public Task<ProjectBudget?> GetByProjectAsync(Guid projectId, CancellationToken ct) =>
            Task.FromResult(Items.Values.FirstOrDefault(b => b.ProjectId == projectId));
        public Task<ProjectBudget?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Items.TryGetValue(id, out var b) ? b : null);
        public Task InsertAsync(ProjectBudget budget, CancellationToken ct) { Items[budget.Id] = budget; return Task.CompletedTask; }
        public Task UpdateAsync(ProjectBudget budget, CancellationToken ct) { Items[budget.Id] = budget; return Task.CompletedTask; }
        public Task<decimal> RecalculateSpentAsync(Guid projectId, Guid costCenterId, CancellationToken ct)
        {
            var b = Items.Values.FirstOrDefault(x => x.ProjectId == projectId);
            return Task.FromResult(b?.SpentAmount ?? 0);
        }
    }

    // ============== DTO Unit Tests ==============

    [Fact]
    public void ProjectPnL_Dto_MarginPercent_CalculatesCorrectly()
    {
        var pnl = new ProjectPnL
        {
            Revenue = 1000, MaterialCost = 300, LaborCost = 200,
            SubcontractorCost = 100, AllocatedOverhead = 50
        };
        pnl.DirectCosts.Should().Be(600);
        pnl.NetProfit.Should().Be(350, "1000 - 600 - 50");
        pnl.MarginPercent.Should().Be(35m);
    }

    [Fact]
    public void ProjectPnL_Dto_ZeroRevenue_ZeroMargin()
    {
        var pnl = new ProjectPnL { AllocatedOverhead = 100 };
        pnl.MarginPercent.Should().Be(0, "Revenue=0 → MarginPercent=0 (لا قسمة على صفر)");
    }

    [Fact]
    public void ProjectPnL_Dto_NegativeNetProfit_WhenCostsExceedRevenue()
    {
        var pnl = new ProjectPnL { Revenue = 500, MaterialCost = 800 };
        pnl.NetProfit.Should().Be(-300);
        pnl.MarginPercent.Should().Be(-60m);
    }

    [Fact]
    public void ProjectBudgetVsActual_Dto_VariancePercent_CalculatesCorrectly()
    {
        var bva = new ProjectBudgetVsActual
        {
            BudgetAmount = 200_000, SpentAmount = 50_000, CommittedAmount = 20_000
        };
        bva.Variance.Should().Be(150_000);
        bva.VariancePercent.Should().Be(75m);
        bva.AvailableAmount.Should().Be(130_000, "200k - 50k - 20k");
        bva.UtilizationPercent.Should().Be(25m);
    }

    [Fact]
    public void ProjectBudgetVsActual_Dto_OverBudget_NegativeVariance()
    {
        var bva = new ProjectBudgetVsActual
        {
            BudgetAmount = 100_000, SpentAmount = 120_000, CommittedAmount = 0
        };
        bva.Variance.Should().Be(-20_000);
        bva.AvailableAmount.Should().Be(-20_000);
    }

    [Fact]
    public void ProjectSummary_Dto_DefaultsToEmpty()
    {
        var summary = new ProjectSummary();
        summary.Code.Should().Be(string.Empty);
        summary.Spent.Should().Be(0);
        summary.MarginPercent.Should().Be(0);
    }

    // ============== Service Integration Tests (Skip - تحتاج Postgres) ==============

    [Fact(Skip = "Integration: requires real Postgres for SQL JOINs across journal_lines and projects.")]
    public async Task GetProjectPnL_RevenueAndMaterial_CalculatesCorrectly()
    {
        var tenant = Guid.NewGuid();
        var db = new FakeDbConnectionFactory();
        var projects = new FakeProjectRepository();
        var budgets = new FakeProjectBudgetRepository();
        var svc = new ProjectReportService(db, projects, budgets);

        var projectId = Guid.NewGuid();
        projects.Items[projectId] = new Project
        {
            Id = projectId, TenantId = tenant, CostCenterId = Guid.NewGuid(),
            Code = "PRJ-001", Name = "مشروع اختبار"
        };

        var pnl = await svc.GetProjectPnLAsync(tenant, projectId,
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), CancellationToken.None);
        pnl.ProjectId.Should().Be(projectId);
    }

    [Fact(Skip = "Integration: requires real Postgres.")]
    public async Task GetBudgetVsActual_WithBudget_ReturnsSpentAndAvailable()
    {
        var tenant = Guid.NewGuid();
        var db = new FakeDbConnectionFactory();
        var projects = new FakeProjectRepository();
        var budgets = new FakeProjectBudgetRepository();
        var svc = new ProjectReportService(db, projects, budgets);

        var projectId = Guid.NewGuid();
        projects.Items[projectId] = new Project
        {
            Id = projectId, TenantId = tenant, CostCenterId = Guid.NewGuid(),
            Code = "PRJ-A", Name = "مشروع A"
        };
        budgets.Items[Guid.NewGuid()] = new ProjectBudget
        {
            Id = Guid.NewGuid(), ProjectId = projectId, TenantId = tenant,
            BudgetAmount = 100_000m, SpentAmount = 30_000m, CommittedAmount = 10_000m
        };

        var bva = await svc.GetBudgetVsActualAsync(tenant, projectId, CancellationToken.None);
        bva.BudgetAmount.Should().Be(100_000);
    }
}
