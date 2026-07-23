using System;
using System.Collections.Generic;

namespace ERPSystem.Modules.Payroll.Domain.Entities;

/// <summary>
/// حالة دورة الرواتب (PayrollRun aggregate root).
///
/// State machine (لا رجوع بعد Posted):
/// Draft ──► Processing ──► Posted
///   │           │              ▲
///   ▼           ▼              │
/// Cancelled  Cancelled         │
///                          (final)
///
/// Transitions:
/// - Create() → Draft
/// - Process() → Processing (يحسب PayrollItem لكل موظف)
/// - Post() → Posted (يُجمّد، يحرّك totals، يُنشئ JournalEntry في Finance)
/// - Cancel() → Cancelled (مسموح فقط من Draft أو Processing)
/// </summary>
public enum PayrollRunStatus
{
    Draft = 1,
    Processing = 2,
    Posted = 3,
    Cancelled = 4
}

/// <summary>
/// دورة رواتب — فترة (period) لموظف/عدة موظفين.
/// Aggregate Root: PayrollRun + PayrollItems + PayslipComponents.
///
/// Business Rules:
/// - period_start < period_end (validation في Application).
/// - PeriodOverlap: لا يوجد run آخر مُتداخل في حالة Draft/Processing/Posted للـ tenant.
/// - بعد Posted: كل القيم (totals، items، components) ثابتة (immutable).
/// - posted_at يُسجّل مرة واحدة فقط (لا تعديل بعده).
/// </summary>
public class PayrollRun
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;

    /// <summary>إجمالي الـ Gross عبر كل الـ items (يُحدّث عند Post).</summary>
    public decimal TotalGross { get; set; }

    /// <summary>إجمالي الـ Net (ما يحصل عليه الموظف فعلياً) عبر كل الـ items.</summary>
    public decimal TotalNet { get; set; }

    /// <summary>وقت بدء المعالجة (Process).</summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>وقت الترحيل النهائي (Post).</summary>
    public DateTime? PostedAt { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>عناصر الـ run (غير محفوظة مباشرة — تُحمَّل منفصلة).</summary>
    public List<PayrollItem> Items { get; set; } = new();

    /// <summary>
    /// ينتقل Run من Draft إلى Processing عند بدء معالجة الـ payslips.
    /// مسموح فقط من حالة Draft.
    /// </summary>
    public void MarkProcessing(DateTime when)
    {
        if (Status != PayrollRunStatus.Draft)
            throw new InvalidOperationException(
                $"Cannot process PayrollRun in status {Status}. Only Draft runs can be processed.");
        Status = PayrollRunStatus.Processing;
        ProcessedAt = when;
        UpdatedAt = when;
    }

    /// <summary>
    /// ينقل Run من Processing إلى Posted (نقطة اللاعودة).
    /// مسموح فقط من حالة Processing.
    /// </summary>
    public void MarkPosted(DateTime when)
    {
        if (Status != PayrollRunStatus.Processing)
            throw new InvalidOperationException(
                $"Cannot post PayrollRun in status {Status}. Only Processing runs can be posted.");
        Status = PayrollRunStatus.Posted;
        PostedAt = when;
        UpdatedAt = when;
    }

    /// <summary>
    /// يلغي الـ run. مسموح من Draft أو Processing فقط.
    /// لا يمكن إلغاء Run مُرحَّل (Posted) — حماية SOX-grade.
    /// </summary>
    public void Cancel(DateTime when)
    {
        if (Status == PayrollRunStatus.Posted)
            throw new InvalidOperationException("Cannot cancel a posted PayrollRun (SOX: no deletes/reversals on posted payroll).");
        if (Status == PayrollRunStatus.Cancelled)
            return;
        Status = PayrollRunStatus.Cancelled;
        UpdatedAt = when;
    }
}