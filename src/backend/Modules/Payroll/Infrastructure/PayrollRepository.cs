using Dapper;
using ERPSystem.Modules.Payroll.Domain.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Payroll.Infrastructure;

/// <summary>
/// تنفيذ Dapper لمستودع Payroll (SalaryStructure + PayrollRun + PayrollItem + PayslipComponent).
///
/// Conventions:
/// - كل الـ queries تستخدم snake_case AS Pascal لتطابق Dapper column mapping.
/// - استخدام IDbConnectionFactory.CreateOltpConnectionAsync.
/// - كل الـ INSERT/UPDATE تأخذ الـ TenantId من الـ entity (لا من الـ context — للدفاع متعدد الطبقات).
/// - Multi-tenancy: كل الـ reads المفهرسة بـ tenant_id.
/// - ON DELETE RESTRICT على payroll_items → employees و runs (SOX: لا نحذف تاريخ).
/// </summary>
public sealed class SalaryStructureRepository : ISalaryStructureRepository
{
    private readonly IDbConnectionFactory _db;
    public SalaryStructureRepository(IDbConnectionFactory db) => _db = db;

    private const string StructureSel = @"id, tenant_id AS TenantId, name, code, currency,
        is_active AS IsActive, created_at AS CreatedAt, created_by AS CreatedBy,
        updated_at AS UpdatedAt, updated_by AS UpdatedBy";

    public async Task<SalaryStructure?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<SalaryStructure>(new CommandDefinition(
            $"SELECT {StructureSel} FROM salary_structures WHERE id = @Id LIMIT 1",
            new { Id = id }, cancellationToken: ct));
    }

    public async Task<SalaryStructure?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<SalaryStructure>(new CommandDefinition(
            $"SELECT {StructureSel} FROM salary_structures WHERE tenant_id = @TenantId AND LOWER(code) = LOWER(@Code) LIMIT 1",
            new { TenantId = tenantId, Code = code }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<SalaryStructure>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {StructureSel} FROM salary_structures WHERE tenant_id = @TenantId";
        if (!includeInactive) sql += " AND is_active = true";
        sql += " ORDER BY code";
        var rows = await conn.QueryAsync<SalaryStructure>(new CommandDefinition(
            sql, new { TenantId = tenantId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<SalaryStructureLine>> GetLinesAsync(Guid salaryStructureId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = @"id, tenant_id AS TenantId, salary_structure_id AS SalaryStructureId,
            type, name, formula, amount, sort_order AS SortOrder
            FROM salary_structure_lines
            WHERE salary_structure_id = @SalaryStructureId
            ORDER BY sort_order, name";
        var rows = await conn.QueryAsync<SalaryStructureLine>(new CommandDefinition(
            sql, new { SalaryStructureId = salaryStructureId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertAsync(SalaryStructure s, IEnumerable<SalaryStructureLine> lines, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO salary_structures (id, tenant_id, name, code, currency, is_active,
                                           created_at, created_by, updated_at, updated_by)
            VALUES (@Id, @TenantId, @Name, @Code, @Currency, @IsActive,
                    @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy)", new
        {
            s.Id, s.TenantId, s.Name, s.Code, s.Currency, s.IsActive,
            s.CreatedAt, s.CreatedBy, s.UpdatedAt, s.UpdatedBy
        }, cancellationToken: ct));

        var linesArr = lines.ToList();
        if (linesArr.Count == 0) return;

        const string lineSql = @"
            INSERT INTO salary_structure_lines (id, tenant_id, salary_structure_id, type, name, formula, amount, sort_order)
            VALUES (@Id, @TenantId, @SalaryStructureId, @Type, @Name, @Formula, @Amount, @SortOrder)";
        foreach (var ln in linesArr)
        {
            await conn.ExecuteAsync(new CommandDefinition(lineSql, new
            {
                ln.Id, ln.TenantId, ln.SalaryStructureId,
                Type = ln.Type.ToString(),
                ln.Name, ln.Formula, ln.Amount, ln.SortOrder
            }, cancellationToken: ct));
        }
    }

    public async Task UpdateAsync(SalaryStructure s, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE salary_structures
            SET name = @Name, code = @Code, currency = @Currency, is_active = @IsActive,
                updated_at = @UpdatedAt, updated_by = @UpdatedBy
            WHERE id = @Id AND tenant_id = @TenantId", new
        {
            s.Id, s.TenantId, s.Name, s.Code, s.Currency, s.IsActive,
            s.UpdatedAt, s.UpdatedBy
        }, cancellationToken: ct));
    }

    public async Task InsertLineAsync(SalaryStructureLine ln, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string lineSql = @"
            INSERT INTO salary_structure_lines (id, tenant_id, salary_structure_id, type, name, formula, amount, sort_order)
            VALUES (@Id, @TenantId, @SalaryStructureId, @Type, @Name, @Formula, @Amount, @SortOrder)";
        await conn.ExecuteAsync(new CommandDefinition(lineSql, new
        {
            ln.Id, ln.TenantId, ln.SalaryStructureId,
            Type = ln.Type.ToString(),
            ln.Name, ln.Formula, ln.Amount, ln.SortOrder
        }, cancellationToken: ct));
    }

    public async Task DeleteLineAsync(Guid lineId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM salary_structure_lines WHERE id = @Id",
            new { Id = lineId }, cancellationToken: ct));
    }
}

/// <summary>
/// تنفيذ Dapper لمستودع PayrollRun + PayrollItem + PayslipComponent.
/// </summary>
public sealed class PayrollRepository : IPayrollRepository
{
    private readonly IDbConnectionFactory _db;
    public PayrollRepository(IDbConnectionFactory db) => _db = db;

    private const string RunSel = @"id, tenant_id AS TenantId, period_start AS PeriodStart, period_end AS PeriodEnd,
        status, total_gross AS TotalGross, total_net AS TotalNet,
        processed_at AS ProcessedAt, posted_at AS PostedAt, notes,
        created_at AS CreatedAt, created_by AS CreatedBy, updated_at AS UpdatedAt, updated_by AS UpdatedBy";

    private const string ItemSel = @"id, tenant_id AS TenantId, payroll_run_id AS PayrollRunId,
        employee_id AS EmployeeId, base_salary AS BaseSalary, gross_salary AS GrossSalary,
        tax_amount AS TaxAmount, social_insurance_employee AS SocialInsuranceEmployee,
        net_salary AS NetSalary, status, payment_days AS PaymentDays, notes,
        created_at AS CreatedAt, created_by AS CreatedBy, updated_at AS UpdatedAt";

    private const string ComponentSel = @"id, tenant_id AS TenantId, payroll_item_id AS PayrollItemId,
        component_type AS ComponentType, name, amount, sort_order AS SortOrder";

    // =================== PayrollRun ===================

    public async Task<PayrollRun?> GetRunByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<PayrollRun>(new CommandDefinition(
            $"SELECT {RunSel} FROM payroll_runs WHERE id = @Id LIMIT 1",
            new { Id = id }, cancellationToken: ct));
    }

    public async Task<PayrollRun?> GetRunByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<PayrollRun>(new CommandDefinition(
            $"SELECT {RunSel} FROM payroll_runs WHERE tenant_id = @TenantId AND id = @Id LIMIT 1",
            new { TenantId = tenantId, Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<PayrollRun>> ListRunsAsync(Guid tenantId, PayrollRunStatus? status, int skip, int take, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {RunSel} FROM payroll_runs WHERE tenant_id = @TenantId";
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (status.HasValue)
        {
            sql += " AND status = @Status";
            p.Add("Status", status.Value.ToString());
        }
        sql += " ORDER BY period_start DESC OFFSET @Skip LIMIT @Take";
        p.Add("Skip", skip); p.Add("Take", take);
        var rows = await conn.QueryAsync<PayrollRun>(new CommandDefinition(sql, p, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertRunAsync(PayrollRun r, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO payroll_runs (id, tenant_id, period_start, period_end, status,
                                      total_gross, total_net, processed_at, posted_at, notes,
                                      created_at, created_by, updated_at, updated_by)
            VALUES (@Id, @TenantId, @PeriodStart, @PeriodEnd, @Status,
                    @TotalGross, @TotalNet, @ProcessedAt, @PostedAt, @Notes,
                    @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy)", new
        {
            r.Id, r.TenantId, r.PeriodStart, r.PeriodEnd,
            Status = r.Status.ToString(),
            r.TotalGross, r.TotalNet, r.ProcessedAt, r.PostedAt, r.Notes,
            r.CreatedAt, r.CreatedBy, r.UpdatedAt, r.UpdatedBy
        }, cancellationToken: ct));
    }

    public async Task UpdateRunAsync(PayrollRun r, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE payroll_runs SET status = @Status, total_gross = @TotalGross, total_net = @TotalNet,
                                    processed_at = @ProcessedAt, posted_at = @PostedAt,
                                    notes = @Notes, updated_at = @UpdatedAt, updated_by = @UpdatedBy
            WHERE id = @Id", new
        {
            r.Id, Status = r.Status.ToString(),
            r.TotalGross, r.TotalNet, r.ProcessedAt, r.PostedAt, r.Notes,
            r.UpdatedAt, r.UpdatedBy
        }, cancellationToken: ct));
    }

    // =================== PayrollItem ===================

    public async Task<PayrollItem?> GetItemByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<PayrollItem>(new CommandDefinition(
            $"SELECT {ItemSel} FROM payroll_items WHERE id = @Id LIMIT 1",
            new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<PayrollItem>> GetItemsByRunAsync(Guid payrollRunId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {ItemSel} FROM payroll_items WHERE payroll_run_id = @PayrollRunId ORDER BY created_at";
        var rows = await conn.QueryAsync<PayrollItem>(new CommandDefinition(
            sql, new { PayrollRunId = payrollRunId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task AddItemAsync(PayrollItem item, IEnumerable<PayslipComponent> components, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO payroll_items (id, tenant_id, payroll_run_id, employee_id,
                                       base_salary, gross_salary, tax_amount, social_insurance_employee,
                                       net_salary, status, payment_days, notes,
                                       created_at, created_by, updated_at)
            VALUES (@Id, @TenantId, @PayrollRunId, @EmployeeId,
                    @BaseSalary, @GrossSalary, @TaxAmount, @SocialInsuranceEmployee,
                    @NetSalary, @Status, @PaymentDays, @Notes,
                    @CreatedAt, @CreatedBy, @UpdatedAt)", new
        {
            item.Id, item.TenantId, item.PayrollRunId, item.EmployeeId,
            item.BaseSalary, item.GrossSalary, item.TaxAmount, item.SocialInsuranceEmployee,
            item.NetSalary, Status = item.Status.ToString(),
            item.PaymentDays, item.Notes,
            item.CreatedAt, item.CreatedBy, item.UpdatedAt
        }, cancellationToken: ct));

        var compArr = components.ToList();
        if (compArr.Count == 0) return;

        const string compSql = @"
            INSERT INTO payslip_components (id, tenant_id, payroll_item_id, component_type, name, amount, sort_order)
            VALUES (@Id, @TenantId, @PayrollItemId, @ComponentType, @Name, @Amount, @SortOrder)";
        foreach (var c in compArr)
        {
            await conn.ExecuteAsync(new CommandDefinition(compSql, new
            {
                c.Id, c.TenantId, c.PayrollItemId,
                ComponentType = c.ComponentType.ToString(),
                c.Name, c.Amount, c.SortOrder
            }, cancellationToken: ct));
        }
    }

    // =================== PayslipComponent ===================

    public async Task<IReadOnlyList<PayslipComponent>> GetComponentsByItemAsync(Guid payrollItemId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"id, tenant_id AS TenantId, payroll_item_id AS PayrollItemId,
            component_type AS ComponentType, name, amount, sort_order AS SortOrder
            FROM payslip_components
            WHERE payroll_item_id = @PayrollItemId
            ORDER BY sort_order, name";
        var rows = await conn.QueryAsync<PayslipComponent>(new CommandDefinition(
            sql, new { PayrollItemId = payrollItemId }, cancellationToken: ct));
        return rows.AsList();
    }

    // =================== SalaryStructure passthrough ===================

    private const string StructureSel = @"id, tenant_id AS TenantId, name, code, currency,
        is_active AS IsActive, created_at AS CreatedAt, created_by AS CreatedBy,
        updated_at AS UpdatedAt, updated_by AS UpdatedBy";

    public async Task<SalaryStructure?> GetStructureByCodeAsync(Guid tenantId, string code, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<SalaryStructure>(new CommandDefinition(
            $"SELECT {StructureSel} FROM salary_structures WHERE tenant_id = @TenantId AND LOWER(code) = LOWER(@Code) LIMIT 1",
            new { TenantId = tenantId, Code = code }, cancellationToken: ct));
    }
}