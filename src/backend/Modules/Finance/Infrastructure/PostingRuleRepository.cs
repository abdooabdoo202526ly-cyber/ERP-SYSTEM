using Dapper;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Finance.Infrastructure;

public sealed class PostingRuleRepository : IPostingRuleRepository
{
    private readonly IDbConnectionFactory _db;

    public PostingRuleRepository(IDbConnectionFactory db) => _db = db;

    public async Task<PostingRule?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            SELECT id, tenant_id AS TenantId, name, description, event_type AS EventType,
                   is_active AS IsActive, template_json AS TemplateJson,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM posting_rules WHERE id = @Id LIMIT 1";
        return await conn.QueryFirstOrDefaultAsync<PostingRule>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<PostingRule>> ListActiveByEventAsync(Guid tenantId, TriggeringEvent eventType, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            SELECT id, tenant_id AS TenantId, name, description, event_type AS EventType,
                   is_active AS IsActive, template_json AS TemplateJson,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM posting_rules
            WHERE tenant_id = @TenantId AND event_type = @EventType AND is_active = true
            ORDER BY created_at";
        var rows = await conn.QueryAsync<PostingRule>(new CommandDefinition(sql,
            new { TenantId = tenantId, EventType = (int)eventType }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<PostingRule>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            SELECT id, tenant_id AS TenantId, name, description, event_type AS EventType,
                   is_active AS IsActive, template_json AS TemplateJson,
                   created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM posting_rules WHERE tenant_id = @TenantId ORDER BY created_at DESC";
        var rows = await conn.QueryAsync<PostingRule>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertAsync(PostingRule rule, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            INSERT INTO posting_rules (id, tenant_id, name, description, event_type, is_active, template_json, created_at, updated_at)
            VALUES (@Id, @TenantId, @Name, @Description, @EventType, @IsActive, @TemplateJson, @CreatedAt, @UpdatedAt)";
        await conn.ExecuteAsync(new CommandDefinition(sql, rule, cancellationToken: ct));
    }

    public async Task UpdateAsync(PostingRule rule, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            UPDATE posting_rules SET name = @Name, description = @Description,
                                      is_active = @IsActive, template_json = @TemplateJson,
                                      updated_at = @UpdatedAt
            WHERE id = @Id";
        await conn.ExecuteAsync(new CommandDefinition(sql, rule, cancellationToken: ct));
    }
}
