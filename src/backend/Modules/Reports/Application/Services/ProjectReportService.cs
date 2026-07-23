using Dapper;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Projects.Entities;
using ERPSystem.Modules.Projects.Infrastructure;
using ERPSystem.Shared.Infrastructure;
using ERPSystem.Modules.Reports.Application;

namespace ERPSystem.Modules.Reports.Application.Services;

public interface IProjectReportService
{
    Task<ProjectPnL> GetProjectPnLAsync(Guid tenantId, Guid projectId, DateTime from, DateTime to, CancellationToken ct);
    Task<ProjectBudgetVsActual> GetBudgetVsActualAsync(Guid tenantId, Guid projectId, CancellationToken ct);
    Task<List<ProjectSummary>> GetProjectsSummaryAsync(Guid tenantId, Guid? companyId, CancellationToken ct);
}

public sealed class ProjectReportService : IProjectReportService
{
    private readonly IDbConnectionFactory _db;
    private readonly IProjectRepository _projects;
    private readonly IProjectBudgetRepository _budgets;

    public ProjectReportService(IDbConnectionFactory db, IProjectRepository projects, IProjectBudgetRepository budgets)
    {
        _db = db; _projects = projects; _budgets = budgets;
    }

    /// <summary>
    /// P&L per Project — يحسب Revenue من الإيرادات (4100, 5110-5120 sundry) و Direct Costs من journal_lines
    /// المُرحّلة على cost_center المرتبط بالـ project.
    /// </summary>
    public async Task<ProjectPnL> GetProjectPnLAsync(Guid tenantId, Guid projectId, DateTime from, DateTime to, CancellationToken ct)
    {
        var project = await _projects.GetByIdAsync(projectId, ct);
        if (project == null || project.TenantId != tenantId)
            return new ProjectPnL { ProjectId = projectId, ProjectCode = "—", ProjectName = "—" };

        using var conn = await _db.CreateOltpConnectionAsync(ct);
        // Revenue (Cr on Revenue accounts 4100-4120) and Costs (Dr on Expense 5110-5400) from journal_lines
        // joined to journal_entries (status=Posted, date range, tenant)
        // joined to accounts (code in 4xxx=revenue, 5xxx=expense)
        // joined to project via cost_center_id on journal_line == project.cost_center_id
        var sql = @"
            SELECT a.code AS AccountCode, a.type AS AccountType,
                   COALESCE(SUM(jl.debit), 0) AS TotalDebit,
                   COALESCE(SUM(jl.credit), 0) AS TotalCredit
            FROM journal_lines jl
            INNER JOIN journal_entries je ON je.id = jl.journal_entry_id
            INNER JOIN accounts a ON a.id = jl.account_id
            WHERE jl.cost_center_id = @CostCenterId
              AND je.status = 2
              AND je.tenant_id = @TenantId
              AND je.entry_date >= @From
              AND je.entry_date <= @To
            GROUP BY a.code, a.type";

        var rows = (await conn.QueryAsync<(string AccountCode, int AccountType, decimal TotalDebit, decimal TotalCredit)>(
            new CommandDefinition(sql, new { CostCenterId = project.CostCenterId, TenantId = tenantId, From = from, To = to }, cancellationToken: ct))).ToList();

        decimal revenue = 0, material = 0, labor = 0, subcontract = 0, overhead = 0;
        foreach (var (code, type, debit, credit) in rows)
        {
            if (type == (int)AccountType.Revenue) revenue += credit - debit;
            else if (type == (int)AccountType.Expense)
            {
                var net = debit - credit;
                if (code.StartsWith("411")) material += net;
                else if (code.StartsWith("412")) labor += net;
                else if (code.StartsWith("413")) subcontract += net;
                else overhead += net;
            }
        }

        return new ProjectPnL
        {
            ProjectId = projectId, ProjectCode = project.Code, ProjectName = project.Name,
            From = from, To = to,
            Revenue = revenue, MaterialCost = material, LaborCost = labor, SubcontractorCost = subcontract,
            AllocatedOverhead = overhead
        };
    }

    public async Task<ProjectBudgetVsActual> GetBudgetVsActualAsync(Guid tenantId, Guid projectId, CancellationToken ct)
    {
        var budget = await _budgets.GetByProjectAsync(projectId, ct);
        if (budget == null || budget.TenantId != tenantId)
            return new ProjectBudgetVsActual { ProjectId = projectId, ProjectCode = "—" };

        // refresh spent (uses Phase 2.1's RecalculateSpentAsync)
        var spent = await _budgets.RecalculateSpentAsync(projectId, budget.CostCenterId, ct);
        var updated = await _budgets.GetByProjectAsync(projectId, ct);

        var project = await _projects.GetByIdAsync(projectId, ct);
        return new ProjectBudgetVsActual
        {
            ProjectId = projectId,
            ProjectCode = project?.Code ?? "—",
            BudgetAmount = updated!.BudgetAmount,
            SpentAmount = updated.SpentAmount,
            CommittedAmount = updated.CommittedAmount,
            LastRecalculatedAt = updated.LastRecalculatedAt
        };
    }

    public async Task<List<ProjectSummary>> GetProjectsSummaryAsync(Guid tenantId, Guid? companyId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = @"
            SELECT p.id, p.code, p.name, p.status, p.budget,
                   COALESCE(pb.spent_amount, 0) AS spent,
                   COALESCE(pb.last_recalculated_at, NULL) AS last_recalc
            FROM projects p
            LEFT JOIN project_budgets pb ON pb.project_id = p.id
            WHERE p.tenant_id = @TenantId AND p.is_active = true"
            + (companyId.HasValue ? " AND p.company_id = @CompanyId" : "")
            + " ORDER BY p.start_date DESC";
        var rows = await conn.QueryAsync<ProjectSummaryRow>(new CommandDefinition(sql,
            new { TenantId = tenantId, CompanyId = companyId }, cancellationToken: ct));
        return rows.Select(r => new ProjectSummary
        {
            Id = r.id, Code = r.code, Name = r.name, Status = (ProjectStatus)r.status,
            Budget = r.budget, Spent = r.spent,
            LastActivity = r.last_recalc,
            MarginPercent = r.budget > 0 ? ((r.budget - r.spent) / r.budget) * 100 : 0
        }).ToList();
    }

    private sealed class ProjectSummaryRow
    {
        public Guid id { get; set; }
        public string code { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public int status { get; set; }
        public decimal budget { get; set; }
        public decimal spent { get; set; }
        public DateTime? last_recalc { get; set; }
    }
}
