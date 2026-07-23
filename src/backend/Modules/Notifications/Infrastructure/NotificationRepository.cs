using Dapper;
using ERPSystem.Modules.Notifications.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Notifications.Infrastructure;

public interface INotificationRepository
{
    Task InsertAsync(Notification n, CancellationToken ct);
    Task<IReadOnlyList<Notification>> ListAsync(Guid tenantId, Guid userId, bool unreadOnly, int skip, int take, CancellationToken ct);
    Task<int> CountUnreadAsync(Guid tenantId, Guid userId, CancellationToken ct);
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct);
    Task MarkReadAsync(Guid id, DateTime at, CancellationToken ct);
}

public sealed class NotificationRepository : INotificationRepository
{
    private readonly IDbConnectionFactory _db;
    public NotificationRepository(IDbConnectionFactory db) => _db = db;
    private const string Sel = @"id, tenant_id AS TenantId, user_id AS UserId, type, title, message,
        reference_type AS ReferenceType, reference_id AS ReferenceId,
        is_read AS IsRead, created_at AS CreatedAt, read_at AS ReadAt";

    public async Task InsertAsync(Notification n, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO notifications (id, tenant_id, user_id, type, title, message, reference_type, reference_id, is_read, created_at, read_at)
            VALUES (@Id, @TenantId, @UserId, @Type, @Title, @Message, @ReferenceType, @ReferenceId, @IsRead, @CreatedAt, @ReadAt)", n, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Notification>> ListAsync(Guid tenantId, Guid userId, bool unreadOnly, int skip, int take, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {Sel} FROM notifications WHERE tenant_id = @TenantId AND user_id = @UserId"
            + (unreadOnly ? " AND is_read = false" : "")
            + " ORDER BY created_at DESC OFFSET @Skip LIMIT @Take";
        var rows = await conn.QueryAsync<Notification>(new CommandDefinition(sql,
            new { TenantId = tenantId, UserId = userId, Skip = skip, Take = take }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<int> CountUnreadAsync(Guid tenantId, Guid userId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM notifications WHERE tenant_id = @TenantId AND user_id = @UserId AND is_read = false",
            new { TenantId = tenantId, UserId = userId }, cancellationToken: ct));
    }

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Notification>(new CommandDefinition(
            $"SELECT {Sel} FROM notifications WHERE id = @Id LIMIT 1", new { Id = id }, cancellationToken: ct));
    }

    public async Task MarkReadAsync(Guid id, DateTime at, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE notifications SET is_read = true, read_at = @At WHERE id = @Id",
            new { Id = id, At = at }, cancellationToken: ct));
    }
}
