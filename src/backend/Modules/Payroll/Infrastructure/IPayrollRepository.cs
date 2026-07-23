using System;
using System.Collections.Generic;
using ERPSystem.Modules.Payroll.Domain.Entities;

namespace ERPSystem.Modules.Payroll.Infrastructure;

/// <summary>
/// عقد المستودع لهيكل الرواتب (SalaryStructure + Lines).
/// كل الـ queries مُفلترة بـ tenant_id لتطبيق الـ multi-tenancy.
/// </summary>
public interface ISalaryStructureRepository
{
    Task<SalaryStructure?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<SalaryStructure?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct);
    Task<IReadOnlyList<SalaryStructure>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct);
    Task<IReadOnlyList<SalaryStructureLine>> GetLinesAsync(Guid salaryStructureId, CancellationToken ct);
    Task InsertAsync(SalaryStructure structure, IEnumerable<SalaryStructureLine> lines, CancellationToken ct);
    Task UpdateAsync(SalaryStructure structure, CancellationToken ct);
    /// <summary>إدراج سطر واحد (يُستخدم في التحديث full-replace بعد حذف القديم).</summary>
    Task InsertLineAsync(SalaryStructureLine line, CancellationToken ct);
    /// <summary>حذف سطر واحد (full-replace على الـ lines في التحديث).</summary>
    Task DeleteLineAsync(Guid lineId, CancellationToken ct);
}

/// <summary>
/// عقد المستودع لدورة الرواتب (PayrollRun aggregate).
/// </summary>
public interface IPayrollRepository
{
    // ============ PayrollRun ============
    Task<PayrollRun?> GetRunByIdAsync(Guid id, CancellationToken ct);
    Task<PayrollRun?> GetRunByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<PayrollRun>> ListRunsAsync(Guid tenantId, PayrollRunStatus? status, int skip, int take, CancellationToken ct);
    Task InsertRunAsync(PayrollRun run, CancellationToken ct);
    Task UpdateRunAsync(PayrollRun run, CancellationToken ct);

    // ============ PayrollItem ============
    Task<PayrollItem?> GetItemByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PayrollItem>> GetItemsByRunAsync(Guid payrollRunId, CancellationToken ct);
    Task AddItemAsync(PayrollItem item, IEnumerable<PayslipComponent> components, CancellationToken ct);

    // ============ PayslipComponent ============
    Task<IReadOnlyList<PayslipComponent>> GetComponentsByItemAsync(Guid payrollItemId, CancellationToken ct);

    // ============ SalaryStructure (passthrough for PayrollService) ============
    Task<SalaryStructure?> GetStructureByCodeAsync(Guid tenantId, string code, CancellationToken ct);
}