using Dapper;
using ERPSystem.Modules.Projects.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Projects.Infrastructure;

public sealed class ProjectBudgetRepository : IProjectBudgetRepository
{
    private readonly IDbConnectionFactory _db;
    public ProjectBudgetRepository(IDbConnectionFactory db) => _db = db;
    private const string Sel = @"id, tenant_id AS TenantId, project_id AS ProjectId, cost_center_id AS CostCenterId,
        account_id AS AccountId, budget_amount AS BudgetAmount, spent_amount AS SpentAmount,
        committed_amount AS CommittedAmount, last_recalculated_at AS LastRecalculatedAt";

    public async Task<ProjectBudget?> GetByProjectAsync(Guid projectId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<ProjectBudget>(new CommandDefinition(
            $"SELECT {Sel} FROM project_budgets WHERE project_id = @ProjectId LIMIT 1",
            new { ProjectId = projectId }, cancellationToken: ct));
    }

    public async Task<ProjectBudget?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<ProjectBudget>(new CommandDefinition(
            $"SELECT {Sel} FROM project_budgets WHERE id = @Id LIMIT 1", new { Id = id }, cancellationToken: ct));
    }

    public async Task InsertAsync(ProjectBudget budget, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO project_budgets (id, tenant_id, project_id, cost_center_id, account_id,
                                         budget_amount, spent_amount, committed_amount, last_recalculated_at)
            VALUES (@Id, @TenantId, @ProjectId, @CostCenterId, @AccountId,
                    @BudgetAmount, @SpentAmount, @CommittedAmount, @LastRecalculatedAt)", budget, cancellationToken: ct));
    }

    public async Task UpdateAsync(ProjectBudget budget, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE project_budgets SET budget_amount = @BudgetAmount, spent_amount = @SpentAmount,
                                       committed_amount = @CommittedAmount, account_id = @AccountId,
                                       last_recalculated_at = @LastRecalculatedAt
            WHERE id = @Id", budget, cancellationToken: ct));
    }

    /// <summary>
    /// يعيد حساب SpentAmount من journal_lines المُرحّلة لـ cost_center هذا المشروع.
    /// معادلة: Σ(debit) - Σ(credit) على كل journal_lines مرتبطة بـ cost_center هذا المشروع.
    /// </summary>
    public async Task<decimal> RecalculateSpentAsync(Guid projectId, Guid costCenterId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            SELECT COALESCE(SUM(jl.debit) - SUM(jl.credit), 0)
            FROM journal_lines jl
            INNER JOIN journal_entries je ON je.id = jl.journal_entry_id
            WHERE jl.cost_center_id = @CostCenterId
              AND je.status = 2  -- Posted
              AND je.tenant_id = @TenantId";
        // Note: projectId not strictly needed; the cost_center mapping is what matters
        var project = await GetByProjectAsync(projectId, ct);
        if (project == null) return 0;
        var spent = await conn.ExecuteScalarAsync<decimal?>(new CommandDefinition(sql,
            new { CostCenterId = costCenterId, TenantId = project.TenantId }, cancellationToken: ct)) ?? 0;

        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE project_budgets SET spent_amount = @Spent, last_recalculated_at = @Now WHERE id = @Id",
            new { Spent = spent, Now = DateTime.UtcNow, Id = project.Id }, cancellationToken: ct));
        return spent;
    }
}
