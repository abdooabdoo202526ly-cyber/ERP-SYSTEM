using Dapper;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Finance.Infrastructure;

public sealed class JournalEntryRepository : IJournalEntryRepository
{
    private readonly IDbConnectionFactory _db;

    public JournalEntryRepository(IDbConnectionFactory db) => _db = db;

    public async Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            SELECT id, tenant_id AS TenantId, entry_number AS EntryNumber, entry_date AS EntryDate,
                   description, reference, status, created_by_user_id AS CreatedByUserId,
                   posted_at AS PostedAt, created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM journal_entries WHERE id = @Id LIMIT 1";
        return await conn.QueryFirstOrDefaultAsync<JournalEntry>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<JournalEntry?> GetWithLinesAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string headerSql = @"
            SELECT id, tenant_id AS TenantId, entry_number AS EntryNumber, entry_date AS EntryDate,
                   description, reference, status, created_by_user_id AS CreatedByUserId,
                   posted_at AS PostedAt, created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM journal_entries WHERE id = @Id LIMIT 1";
        const string linesSql = @"
            SELECT id, journal_entry_id AS JournalEntryId, account_id AS AccountId,
                   debit, credit, description, line_number AS LineNumber
            FROM journal_lines WHERE journal_entry_id = @Id ORDER BY line_number";

        var entry = await conn.QueryFirstOrDefaultAsync<JournalEntry>(
            new CommandDefinition(headerSql, new { Id = id }, cancellationToken: ct));
        if (entry == null) return null;

        var lines = await conn.QueryAsync<JournalLine>(
            new CommandDefinition(linesSql, new { Id = id }, cancellationToken: ct));
        entry.Lines = lines.AsList();
        return entry;
    }

    public async Task<bool> EntryNumberExistsAsync(Guid tenantId, string entryNumber, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = "SELECT 1 FROM journal_entries WHERE tenant_id = @TenantId AND entry_number = @EntryNumber LIMIT 1";
        var hit = await conn.QueryFirstOrDefaultAsync<int?>(new CommandDefinition(sql, new { TenantId = tenantId, EntryNumber = entryNumber }, cancellationToken: ct));
        return hit.HasValue;
    }

    public async Task<string> GetNextEntryNumberAsync(Guid tenantId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            SELECT entry_number FROM journal_entries
            WHERE tenant_id = @TenantId AND entry_number LIKE @Prefix
            ORDER BY entry_number DESC LIMIT 1";
        var year = DateTime.UtcNow.Year;
        var prefix = $"JE-{year}-";
        var last = await conn.QueryFirstOrDefaultAsync<string>(
            new CommandDefinition(sql, new { TenantId = tenantId, Prefix = prefix + "%" }, cancellationToken: ct));

        if (last == null) return $"{prefix}0001";

        // JE-2026-0042 -> 42
        var lastSeq = last.Substring(prefix.Length);
        return int.TryParse(lastSeq, out var n) ? $"{prefix}{(n + 1):D4}" : $"{prefix}0001";
    }

    public async Task InsertAsync(JournalEntry entry, CancellationToken ct)
    {
        using var conn = (Npgsql.NpgsqlConnection)await _db.CreateOltpConnectionAsync(ct);
        using var tx = await conn.BeginTransactionAsync(ct);

        const string headerSql = @"
            INSERT INTO journal_entries (id, tenant_id, entry_number, entry_date, description, reference,
                                         status, created_by_user_id, posted_at, created_at, updated_at)
            VALUES (@Id, @TenantId, @EntryNumber, @EntryDate, @Description, @Reference,
                    @Status, @CreatedByUserId, @PostedAt, @CreatedAt, @UpdatedAt)";
        await conn.ExecuteAsync(new CommandDefinition(headerSql, entry, transaction: tx, cancellationToken: ct));

        const string lineSql = @"
            INSERT INTO journal_lines (id, journal_entry_id, account_id, debit, credit, description, line_number)
            VALUES (@Id, @JournalEntryId, @AccountId, @Debit, @Credit, @Description, @LineNumber)";
        foreach (var line in entry.Lines)
        {
            await conn.ExecuteAsync(new CommandDefinition(lineSql, line, transaction: tx, cancellationToken: ct));
        }

        await tx.CommitAsync(ct);
    }

    public async Task UpdateAsync(JournalEntry entry, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        const string sql = @"
            UPDATE journal_entries SET entry_date = @EntryDate, description = @Description,
                                       reference = @Reference, status = @Status, posted_at = @PostedAt,
                                       updated_at = @UpdatedAt
            WHERE id = @Id";
        await conn.ExecuteAsync(new CommandDefinition(sql, entry, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<JournalEntry>> ListAsync(Guid tenantId, DateTime? from, DateTime? to, JournalEntryStatus? status, int skip, int take, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = @"
            SELECT id, tenant_id AS TenantId, entry_number AS EntryNumber, entry_date AS EntryDate,
                   description, reference, status, created_by_user_id AS CreatedByUserId,
                   posted_at AS PostedAt, created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM journal_entries
            WHERE tenant_id = @TenantId";
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (from.HasValue) { sql += " AND entry_date >= @From"; p.Add("From", from.Value); }
        if (to.HasValue) { sql += " AND entry_date <= @To"; p.Add("To", to.Value); }
        if (status.HasValue) { sql += " AND status = @Status"; p.Add("Status", (int)status.Value); }
        sql += " ORDER BY entry_date DESC, entry_number DESC OFFSET @Skip LIMIT @Take";
        p.Add("Skip", skip);
        p.Add("Take", take);

        var rows = await conn.QueryAsync<JournalEntry>(new CommandDefinition(sql, p, cancellationToken: ct));
        return rows.AsList();
    }
}
