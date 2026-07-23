using Dapper;
using ERPSystem.Modules.Identity.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Identity.Infrastructure;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IDbConnectionFactory _db;

    public RefreshTokenRepository(IDbConnectionFactory db) => _db = db;

    public async Task InsertAsync(RefreshToken token, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            INSERT INTO refresh_tokens (id, user_id, token_hash, expires_at, created_at, created_by_ip)
            VALUES (@Id, @UserId, @TokenHash, @ExpiresAt, @CreatedAt, @CreatedByIp)";
        await conn.ExecuteAsync(new CommandDefinition(sql, token, cancellationToken: ct));
    }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            SELECT id, user_id AS UserId, token_hash AS TokenHash, expires_at AS ExpiresAt,
                   created_at AS CreatedAt, revoked_at AS RevokedAt,
                   replaced_by_token_hash AS ReplacedByTokenHash, revoked_reason AS RevokedReason,
                   created_by_ip AS CreatedByIp, revoked_by_ip AS RevokedByIp
            FROM refresh_tokens WHERE token_hash = @TokenHash LIMIT 1";
        return await conn.QueryFirstOrDefaultAsync<RefreshToken>(new CommandDefinition(sql, new { TokenHash = tokenHash }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            SELECT id, user_id AS UserId, token_hash AS TokenHash, expires_at AS ExpiresAt,
                   created_at AS CreatedAt, revoked_at AS RevokedAt,
                   replaced_by_token_hash AS ReplacedByTokenHash, revoked_reason AS RevokedReason,
                   created_by_ip AS CreatedByIp, revoked_by_ip AS RevokedByIp
            FROM refresh_tokens
            WHERE user_id = @UserId AND revoked_at IS NULL AND expires_at > @Now";
        var rows = await conn.QueryAsync<RefreshToken>(new CommandDefinition(sql, new { UserId = userId, Now = DateTime.UtcNow }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task RevokeAsync(RefreshToken token, string reason, string? replacedByHash, string? ip, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            UPDATE refresh_tokens
            SET revoked_at = @Now, revoked_reason = @Reason,
                replaced_by_token_hash = @ReplacedBy, revoked_by_ip = @Ip
            WHERE id = @Id";
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = token.Id,
            Now = DateTime.UtcNow,
            Reason = reason,
            ReplacedBy = replacedByHash,
            Ip = ip
        }, cancellationToken: ct));
    }

    public async Task RevokeAllForUserAsync(Guid userId, string reason, string? ip, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            UPDATE refresh_tokens
            SET revoked_at = @Now, revoked_reason = @Reason, revoked_by_ip = @Ip
            WHERE user_id = @UserId AND revoked_at IS NULL";
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            UserId = userId,
            Now = DateTime.UtcNow,
            Reason = reason,
            Ip = ip
        }, cancellationToken: ct));
    }
}
