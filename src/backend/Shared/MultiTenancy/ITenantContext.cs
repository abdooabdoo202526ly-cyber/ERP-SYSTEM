namespace ERPSystem.Shared.MultiTenancy;

/// <summary>
/// سياق المستأجر (Tenant) الحالي داخل الـ request
/// يُملأ من الـ TenantMiddleware بعد التحقق من JWT
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? UserId { get; }
    bool IsResolved { get; }

    void Set(Guid tenantId, Guid userId);
    void Clear();
}
