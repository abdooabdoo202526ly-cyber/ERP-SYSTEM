using System;

namespace ERPSystem.Modules.Projects.Entities;

/// <summary>
/// ميزانية مشروع — مرتبطة بـ CostCenter (1:1) و Account اختياري (4111-4114).
/// SpentAmount يُحسب من journal_lines المُرحّلة (JOIN على cost_center_id).
/// </summary>
public class ProjectBudget
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid CostCenterId { get; set; }
    public Guid? AccountId { get; set; }            // 4111-4114 مصروفات
    public decimal BudgetAmount { get; set; }
    public decimal SpentAmount { get; set; }        // محسوب
    public decimal CommittedAmount { get; set; }    // طلبيات + عقود
    public DateTime? LastRecalculatedAt { get; set; }

    public decimal AvailableAmount => BudgetAmount - SpentAmount - CommittedAmount;
    public decimal UtilizationPercent => BudgetAmount > 0 ? (SpentAmount / BudgetAmount) * 100 : 0;
}
