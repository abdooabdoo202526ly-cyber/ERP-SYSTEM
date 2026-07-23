using System.Text.Json;
using Dapper;
using ERPSystem.Shared.Infrastructure;
using ERPSystem.Shared.MultiTenancy;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Shared.Audit;

/// <summary>
/// Default implementation of <see cref="IAuditLogger"/> using Dapper.
/// Stores audit entries in the `audit_log` table (DEC-056).
/// </summary>
public class AuditLogger : IAuditLogger
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(
        IDbConnectionFactory dbFactory,
        ITenantContext tenantContext,
        ILogger<AuditLogger> logger)
    {
        _dbFactory = dbFactory;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task LogAsync(
        Guid tenantId,
        string entityType,
        Guid entityId,
        string action,
        Guid? userId = null,
        object? changes = null,
        string? ipAddress = null)
    {
        if (tenantId == Guid.Empty)
        {
            _logger.LogWarning("AuditLog skipped: tenantId is empty (entity: {EntityType}/{EntityId})", entityType, entityId);
            return;
        }

        if (string.IsNullOrWhiteSpace(entityType))
        {
            _logger.LogWarning("AuditLog skipped: entityType is empty");
            return;
        }

        try
        {
            const string sql = @"
                INSERT INTO audit_log (
                    tenant_id, entity_type, entity_id, action, user_id, changes, ip_address
                ) VALUES (
                    @TenantId, @EntityType, @EntityId, @Action, @UserId, @Changes::jsonb, @IpAddress
                )";

            var changesJson = changes != null
                ? JsonSerializer.Serialize(changes)
                : null;

            using var conn = await _dbFactory.CreateOltpConnectionAsync();
            await conn.ExecuteAsync(sql, new
            {
                TenantId = tenantId,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                UserId = userId,
                Changes = changesJson,
                IpAddress = ipAddress
            });
        }
        catch (Exception ex)
        {
            // Audit failures must NOT break the original operation.
            _logger.LogError(ex,
                "Failed to write audit log for {EntityType}/{EntityId} action={Action}",
                entityType, entityId, action);
        }
    }

    public async Task LogAsync(
        string entityType,
        Guid entityId,
        string action,
        object? changes = null)
    {
        var tenantId = _tenantContext.TenantId;
        var userId = _tenantContext.UserId;

        if (tenantId == null || tenantId == Guid.Empty)
        {
            _logger.LogWarning("AuditLog (context) skipped: tenant context empty");
            return;
        }

        await LogAsync(tenantId.Value, entityType, entityId, action, userId, changes);
    }
}