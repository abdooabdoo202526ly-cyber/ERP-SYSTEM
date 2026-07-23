namespace ERPSystem.Shared.Audit;

/// <summary>
/// Audit log action types.
/// </summary>
public static class AuditAction
{
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";
    public const string Restore = "RESTORE";
}

/// <summary>
/// Logs CREATE/UPDATE/DELETE events on Finance + Projects entities (Sprint-4.5 / DEC-056).
/// Implementations must be safe to call from any service layer (no exceptions thrown).
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// سجل حدث audit log. لا يرمي exceptions — failures تُسجّل في الـ logger بدلاً من ذلك.
    /// </summary>
    /// <param name="tenantId">معرّف الـ tenant (إلزامي)</param>
    /// <param name="entityType">نوع الـ entity (مثل "journal_entry"، "project")</param>
    /// <param name="entityId">معرّف الـ entity (Guid)</param>
    /// <param name="action">CREATE/UPDATE/DELETE/RESTORE</param>
    /// <param name="userId">المستخدم الذي قام بالتغيير (nullable لو system)</param>
    /// <param name="changes">JSON للـ before/after state (nullable لـ CREATE على entity جديد بدون state سابق)</param>
    /// <param name="ipAddress">IP المستخدم (nullable)</param>
    Task LogAsync(
        Guid tenantId,
        string entityType,
        Guid entityId,
        string action,
        Guid? userId = null,
        object? changes = null,
        string? ipAddress = null);

    /// <summary>
    /// Variant يقبل TenantId من الـ TenantContext مباشرة (الـ use case الشائع).
    /// </summary>
    Task LogAsync(
        string entityType,
        Guid entityId,
        string action,
        object? changes = null);
}