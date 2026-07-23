using ERPSystem.Modules.Finance.Entities;

namespace ERPSystem.Modules.Finance.Application.Services;

public interface IPostingRulesService
{
    /// <summary>إنشاء قاعدة جديدة</summary>
    Task<FinanceResult<PostingRule>> CreateAsync(Guid tenantId, CreatePostingRuleRequest request, CancellationToken ct);

    /// <summary>قائمة القواعد للمستأجر</summary>
    Task<FinanceResult<IReadOnlyList<PostingRule>>> ListAsync(Guid tenantId, CancellationToken ct);

    /// <summary>v1.0.30: تفاصيل قاعدة واحدة</summary>
    Task<FinanceResult<PostingRule>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// <summary>v1.0.30: تحديث قاعدة</summary>
    Task<FinanceResult<PostingRule>> UpdateAsync(Guid tenantId, Guid id, CreatePostingRuleRequest request, CancellationToken ct);

    /// <summary>تطبيق كل القواعد النشطة لحدث معين على Tenant معطى</summary>
    /// <returns>عدد القيود المُنشأة</returns>
    Task<int> ApplyRulesAsync(Guid tenantId, Guid userId, TriggeringEvent eventType, EventPayload payload, CancellationToken ct);

    /// <summary>Seed القواعد الافتراضية عند إنشاء tenant جديد</summary>
    Task EnsureDefaultRulesAsync(Guid tenantId, CancellationToken ct);
}

/// <summary>البيانات المحمولة في الحدث (event payload) — تُمرَّر لـ template</summary>
public sealed class EventPayload
{
    /// <summary>المبلغ الأساسي (مثال: قيمة المخزون المستلم)</summary>
    public decimal Amount { get; set; }

    /// <summary>عملة (افتراضياً SAR)</summary>
    public string Currency { get; set; } = "SAR";

    /// <summary>وصف القيد</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>مرجع خارجي (رقم فاتورة استلام، إلخ)</summary>
    public string? Reference { get; set; }

    /// <summary>تاريخ الحركة (افتراضياً الآن)</summary>
    public DateTime EntryDate { get; set; } = DateTime.UtcNow;
}
