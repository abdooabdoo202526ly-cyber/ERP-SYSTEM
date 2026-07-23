using System.Data;
using System.Text.Json;
using Dapper;
using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Finance.Infrastructure;
using ERPSystem.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Shared.SeedData;

/// <summary>
/// CLEAN admin seeder (added 2026-07-23) — replaces the broken
/// ScenarioSeederHostedService + RealisticSeedHostedService which depend
/// on stale JSON data files (DEC-086/087/088) and a buggy audit_log
/// migration (DEC-082). This seeder is minimal, idempotent, and creates
/// only the bare minimum needed to log in:
///   1. A tenant (AlFajr Trading & Contracting)
///   2. An admin user (admin@alfajr.local / Demo1234)
///   3. An Admin role bound to the user
///
/// Hash for "Demo1234" was pre-computed with BCrypt cost 12 — this matches
/// the AuthService.RegisterAsync BCrypt cost (12), so the same plaintext
/// password is verified at login time.
///
/// Disable by setting Database:SeedAdminUser = false in appsettings.json.
/// </summary>
public sealed class AdminUserSeederHostedService : BackgroundService
{
    // BCrypt hash for "Demo1234" with cost 12 (matches AuthService cost).
    // Generated once and hardcoded so we don't depend on BCrypt being loaded
    // at seeder time. Both $2a$ and $2b$ variants are accepted by BCrypt.Net.
    public const string AdminPasswordHash = "$2b$12$D3lhJwRg2e8o.8eQURnBiOC7iYu5kRVID25EZ0qtpO2D6MV4vdPma";

    public const string AdminEmail = "admin@alfajr.local";
    public const string AdminPassword = "Demo1234";   // for display only
    public const string TenantName = "AlFajr Trading & Contracting";
    public const string TenantSubdomain = "alfajr";
    public const string RoleName = "Admin";
    public const string FullName = "AlFajr Administrator";

    private readonly IServiceProvider _root;
    private readonly IConfiguration _config;
    private readonly ILogger<AdminUserSeederHostedService> _logger;

    public AdminUserSeederHostedService(
        IServiceProvider root,
        IConfiguration config,
        ILogger<AdminUserSeederHostedService> logger)
    {
        _root = root;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait 8s to let migrations + other services start first.
        try { await Task.Delay(8000, stoppingToken); }
        catch (OperationCanceledException) { return; }

        var enabled = _config.GetValue<bool?>("Database:SeedAdminUser") ?? true;
        if (!enabled)
        {
            _logger.LogInformation("AdminUserSeeder: disabled (Database:SeedAdminUser=false)");
            return;
        }

        try
        {
            using var scope = _root.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var conn = await dbFactory.CreateOltpConnectionAsync(stoppingToken);

            // Step 1: ensure tenant exists
            var tenantId = await conn.QueryFirstOrDefaultAsync<Guid?>(@"
                SELECT id FROM tenants WHERE subdomain = @subdomain LIMIT 1",
                new { subdomain = TenantSubdomain });

            if (tenantId == null)
            {
                tenantId = Guid.NewGuid();
                await conn.ExecuteAsync(@"
                    INSERT INTO tenants (id, name, subdomain, is_active, created_at)
                    VALUES (@id, @name, @subdomain, true, now())",
                    new { id = tenantId, name = TenantName, subdomain = TenantSubdomain });
                _logger.LogInformation("AdminUserSeeder: created tenant {TenantId} ({Subdomain})", tenantId, TenantSubdomain);
            }
            else
            {
                _logger.LogInformation("AdminUserSeeder: tenant exists {TenantId} ({Subdomain})", tenantId, TenantSubdomain);
            }

            // Step 2: ensure role exists
            var roleId = await conn.QueryFirstOrDefaultAsync<Guid?>(@"
                SELECT id FROM roles WHERE tenant_id = @tid AND name = @name LIMIT 1",
                new { tid = tenantId, name = RoleName });

            if (roleId == null)
            {
                roleId = Guid.NewGuid();
                await conn.ExecuteAsync(@"
                    INSERT INTO roles (id, tenant_id, name, description, created_at)
                    VALUES (@id, @tid, @name, @desc, now())",
                    new { id = roleId, tid = tenantId, name = RoleName, desc = "System administrator" });
                _logger.LogInformation("AdminUserSeeder: created role Admin ({RoleId})", roleId);
            }

            // Step 3: ensure user exists
            var existingUserId = await conn.QueryFirstOrDefaultAsync<Guid?>(@"
                SELECT id FROM users WHERE tenant_id = @tid AND email = @email LIMIT 1",
                new { tid = tenantId, email = AdminEmail });

            Guid userId;
            if (existingUserId == null)
            {
                userId = Guid.NewGuid();
                await conn.ExecuteAsync(@"
                    INSERT INTO users (id, tenant_id, email, password_hash, full_name,
                                       is_active, two_factor_enabled, created_at, updated_at)
                    VALUES (@id, @tid, @email, @hash, @name,
                            true, false, now(), now())",
                    new
                    {
                        id = userId,
                        tid = tenantId,
                        email = AdminEmail,
                        hash = AdminPasswordHash,
                        name = FullName
                    });
                _logger.LogInformation("AdminUserSeeder: created user {Email} ({UserId})", AdminEmail, userId);
            }
            else
            {
                userId = existingUserId.Value;
                _logger.LogInformation("AdminUserSeeder: user exists {Email} ({UserId})", AdminEmail, userId);
            }

            // Step 4: ensure user has Admin role
            var hasRole = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*)::int FROM user_roles WHERE user_id = @uid AND role_id = @rid",
                new { uid = userId, rid = roleId });

            if (hasRole == 0)
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO user_roles (user_id, role_id, assigned_at)
                    VALUES (@uid, @rid, now())",
                    new { uid = userId, rid = roleId });
                _logger.LogInformation("AdminUserSeeder: assigned Admin role to user");
            }

            // Step 5: ensure default Chart of Accounts exists (v1.0.20)
            // The previous behavior was lazy-seeding on first GET, but
            // many pages immediately call GET /api/finance/accounts and
            // hit the cache with an empty result. Seeding here makes the
            // app immediately useful after first boot.
            try
            {
                var accountRepo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
                var coaCount = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*)::int FROM accounts WHERE tenant_id = @tid",
                    new { tid = tenantId.Value });
                if (coaCount == 0)
                {
                    await SeedDefaultCoAAsync(tenantId.Value, accountRepo, conn, stoppingToken);
                    _logger.LogInformation("AdminUserSeeder: seeded default Chart of Accounts for tenant {TenantId}", tenantId);
                }
                else
                {
                    _logger.LogInformation("AdminUserSeeder: Chart of Accounts already exists ({Count} accounts)", coaCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AdminUserSeeder: CoA seeding skipped (non-fatal)");
            }

            // Step 6: ensure default Cost Centers exist (v1.0.31)
            try
            {
                var ccService = scope.ServiceProvider.GetRequiredService<ERPSystem.Modules.Companies.Application.Services.ICostCenterService>();
                var companyRepo = scope.ServiceProvider.GetRequiredService<ERPSystem.Modules.Companies.Infrastructure.ICompanyRepository>();
                var companyId = await companyRepo.GetHoldingCompanyIdAsync(tenantId.Value, stoppingToken)
                    ?? Guid.NewGuid();
                var ccCount = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*)::int FROM cost_centers WHERE tenant_id = @tid",
                    new { tid = tenantId.Value });
                if (ccCount == 0)
                {
                    await DefaultCostCentersSeed.EnsureDefaultsForTenantAsync(
                        tenantId.Value, companyId, ccService, _logger, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AdminUserSeeder: CostCenter seeding skipped (non-fatal)");
            }

            // Final summary log
            _logger.LogInformation("==========================================");
            _logger.LogInformation("AdminUserSeeder: DONE");
            _logger.LogInformation("  Tenant: {Name} (subdomain={Sub})", TenantName, TenantSubdomain);
            _logger.LogInformation("  Login:  {Email} / {Password}", AdminEmail, AdminPassword);
            _logger.LogInformation("  TenantId: {TenantId}", tenantId);
            _logger.LogInformation("==========================================");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdminUserSeeder: FAILED — login will not work until fixed");
            // Re-throw so the host sees the failure in logs (but don't crash the app)
        }
    }

    /// <summary>
    /// v1.0.20: Seed default Chart of Accounts for a tenant. Uses the
    /// shared DefaultCoASeed data but writes directly with companyId=null
    /// (since this seeder doesn't create a default company). Topological
    /// pass so children are inserted after parents.
    /// </summary>
    private async Task SeedDefaultCoAAsync(
        Guid tenantId,
        IAccountRepository accountRepo,
        System.Data.IDbConnection conn,
        CancellationToken ct)
    {
        var allEntries = DefaultCoASeed.HoldingAccounts.ToList();
        var idByCode = new Dictionary<string, Guid>();
        var added = 0;
        while (added < allEntries.Count)
        {
            var addedThisPass = 0;
            foreach (var (code, name, type, parentCode, postable, intercompany) in allEntries)
            {
                if (idByCode.ContainsKey(code)) continue;
                Guid? parentId = null;
                if (parentCode != null)
                {
                    if (!idByCode.TryGetValue(parentCode, out var p)) continue;
                    parentId = p;
                }
                var now = DateTime.UtcNow;
                var acc = new Account
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CompanyId = null, // seeder doesn't bind to a specific company
                    Code = code,
                    Name = name,
                    Type = type,
                    NormalBalance = type == AccountType.Asset || type == AccountType.Expense
                        ? NormalBalance.Debit
                        : NormalBalance.Credit,
                    ParentAccountId = parentId,
                    IsPostable = postable,
                    IsActive = true,
                    IsIntercompany = intercompany,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await conn.ExecuteAsync(@"
                    INSERT INTO accounts (id, tenant_id, company_id, code, name, description, type,
                                          normal_balance, parent_account_id, is_postable, is_active,
                                          is_intercompany, created_at, updated_at)
                    VALUES (@Id, @TenantId, @CompanyId, @Code, @Name, NULL, @Type,
                            @NormalBalance, @ParentAccountId, @IsPostable, @IsActive,
                            @IsIntercompany, @CreatedAt, @UpdatedAt)",
                    acc);
                idByCode[code] = acc.Id;
                addedThisPass++;
            }
            if (addedThisPass == 0) break; // safety: circular parent ref
            added += addedThisPass;
        }
    }
}
