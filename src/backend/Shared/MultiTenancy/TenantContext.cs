namespace ERPSystem.Shared.MultiTenancy;

/// <summary>
/// تنفيذ ITenantContext باستخدام AsyncLocal ليكون scoped على request
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private static readonly AsyncLocal<TenantHolder> _holder = new();

    public Guid? TenantId => _holder.Value?.TenantId;
    public Guid? UserId => _holder.Value?.UserId;
    public bool IsResolved => _holder.Value is { TenantId: not null, UserId: not null };

    public void Set(Guid tenantId, Guid userId)
    {
        _holder.Value = new TenantHolder(tenantId, userId);
    }

    public void Clear()
    {
        _holder.Value = null!;
    }

    private sealed class TenantHolder
    {
        public Guid? TenantId { get; }
        public Guid? UserId { get; }

        public TenantHolder(Guid tenantId, Guid userId)
        {
            TenantId = tenantId;
            UserId = userId;
        }
    }
}
