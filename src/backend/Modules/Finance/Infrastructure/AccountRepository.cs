using Dapper;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Shared.Infrastructure;
using ERPSystem.Shared.SeedData;

namespace ERPSystem.Modules.Finance.Infrastructure;

public sealed class AccountRepository : IAccountRepository
{
    private readonly IDbConnectionFactory _db;
    public AccountRepository(IDbConnectionFactory db) => _db = db;

    private const string SelectColumns = @"
        id, tenant_id AS TenantId, company_id AS CompanyId, code, name, description, type,
        normal_balance AS NormalBalance, parent_account_id AS ParentAccountId,
        is_postable AS IsPostable, is_active AS IsActive, is_intercompany AS IsIntercompany,
        created_at AS CreatedAt, updated_at AS UpdatedAt";

    public async Task<Account?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await QueryFirstAsync(conn, "WHERE id = @Id", new { Id = id }, ct);
    }

    public async Task<Account?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await QueryFirstAsync(conn, "WHERE tenant_id = @TenantId AND LOWER(code) = LOWER(@Code)",
            new { TenantId = tenantId, Code = code }, ct);
    }

    public async Task<IReadOnlyList<Account>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {SelectColumns} FROM accounts WHERE tenant_id = @TenantId"
            + (includeInactive ? "" : " AND is_active = true") + " ORDER BY code";
        var rows = await conn.QueryAsync<Account>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<Account>> ListChildrenAsync(Guid parentId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var rows = await conn.QueryAsync<Account>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM accounts WHERE parent_account_id = @ParentId ORDER BY code",
            new { ParentId = parentId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<Account>> ListByCompanyAsync(Guid tenantId, Guid? companyId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {SelectColumns} FROM accounts WHERE tenant_id = @TenantId";
        if (companyId.HasValue) sql += " AND company_id = @CompanyId";
        sql += " ORDER BY code";
        var rows = await conn.QueryAsync<Account>(new CommandDefinition(sql, new { TenantId = tenantId, CompanyId = companyId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertAsync(Account account, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO accounts (id, tenant_id, company_id, code, name, description, type, normal_balance,
                                  parent_account_id, is_postable, is_active, is_intercompany, created_at, updated_at)
            VALUES (@Id, @TenantId, @CompanyId, @Code, @Name, @Description, @Type, @NormalBalance,
                    @ParentAccountId, @IsPostable, @IsActive, @IsIntercompany, @CreatedAt, @UpdatedAt)",
            account, cancellationToken: ct));
    }

    public async Task UpdateAsync(Account account, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE accounts SET name = @Name, description = @Description, type = @Type,
                                normal_balance = @NormalBalance, parent_account_id = @ParentAccountId,
                                is_postable = @IsPostable, is_active = @IsActive,
                                is_intercompany = @IsIntercompany, updated_at = @UpdatedAt
            WHERE id = @Id", account, cancellationToken: ct));
    }

    public async Task<int> CountPostingsAsync(Guid accountId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM journal_lines WHERE account_id = @AccountId",
            new { AccountId = accountId }, cancellationToken: ct));
    }

    public async Task EnsureDefaultCoAAsync(Guid tenantId, Guid companyId, CancellationToken ct)
    {
        if (await GetByCodeAsync(tenantId, "0000", ct) != null) return;

        // Topological sort: نضيف على passes متتالية حتى ما يبقى accounts بـ parent غير محلول
        var allEntries = DefaultCoASeed.HoldingAccounts.ToList();
        var idByCode = new Dictionary<string, Guid>();
        var added = 0;
        while (added < allEntries.Count)
        {
            var addedThisPass = 0;
            foreach (var (code, name, type, parentCode, postable, intercompany) in allEntries)
            {
                if (idByCode.ContainsKey(code)) continue; // already added
                Guid? parentId = null;
                if (parentCode != null)
                {
                    if (!idByCode.TryGetValue(parentCode, out var p)) continue; // parent not yet added
                    parentId = p;
                }
                var acc = NewAccount(tenantId, companyId, code, name, type, parentId, postable, intercompany);
                await InsertAsync(acc, ct);
                idByCode[code] = acc.Id;
                addedThisPass++;
            }
            if (addedThisPass == 0) break; // لن يحدث لأن الـ seed صحيح
            added += addedThisPass;
        }
    }

    public async Task CloneCoAFromCompanyAsync(Guid targetCompanyId, Guid sourceCompanyId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sourceAccounts = (await conn.QueryAsync<Account>(new CommandDefinition(@$"
            SELECT {SelectColumns} FROM accounts WHERE company_id = @SourceId ORDER BY code",
            new { SourceId = sourceCompanyId }, cancellationToken: ct))).AsList();
        if (sourceAccounts.Count == 0) return;
        var tenantId = sourceAccounts.First().TenantId;
        var idMapping = new Dictionary<Guid, Guid>();
        foreach (var src in sourceAccounts) idMapping[src.Id] = Guid.NewGuid();
        // Pass 1: roots
        foreach (var src in sourceAccounts.Where(a => a.ParentAccountId == null))
            await InsertAsync(new Account
            {
                Id = idMapping[src.Id], TenantId = tenantId, CompanyId = targetCompanyId,
                Code = src.Code, Name = src.Name, Description = src.Description, Type = src.Type,
                NormalBalance = src.NormalBalance, IsPostable = src.IsPostable, IsActive = src.IsActive,
                IsIntercompany = src.IsIntercompany, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            }, ct);
        // Pass 2: children
        foreach (var src in sourceAccounts.Where(a => a.ParentAccountId != null))
        {
            Guid? newParentId = src.ParentAccountId.HasValue && idMapping.TryGetValue(src.ParentAccountId.Value, out var mapped) ? mapped : null;
            await InsertAsync(new Account
            {
                Id = idMapping[src.Id], TenantId = tenantId, CompanyId = targetCompanyId,
                Code = src.Code, Name = src.Name, Description = src.Description, Type = src.Type,
                NormalBalance = src.NormalBalance, ParentAccountId = newParentId,
                IsPostable = src.IsPostable, IsActive = src.IsActive, IsIntercompany = src.IsIntercompany,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            }, ct);
        }
    }

    private static Account NewAccount(Guid tenantId, Guid companyId, string code, string name, AccountType type, Guid? parentId, bool postable, bool intercompany)
    {
        var now = DateTime.UtcNow;
        return new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = companyId, Code = code, Name = name,
            Type = type,
            NormalBalance = type == AccountType.Asset || type == AccountType.Expense ? NormalBalance.Debit : NormalBalance.Credit,
            ParentAccountId = parentId, IsPostable = postable, IsActive = true, IsIntercompany = intercompany,
            CreatedAt = now, UpdatedAt = now
        };
    }

    private static async Task<Account?> QueryFirstAsync(System.Data.IDbConnection conn, string where, object p, CancellationToken ct)
    {
        return await conn.QueryFirstOrDefaultAsync<Account>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM accounts " + where + " LIMIT 1", p, cancellationToken: ct));
    }
}
