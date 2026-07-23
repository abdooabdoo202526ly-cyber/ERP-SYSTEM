using System.Data;
using Dapper;
using ERPSystem.Shared.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ERPSystem.Shared.SeedData;

/// <summary>
/// Realistic 2-year seed for a Libyan holding company (Sprint-4.5 / DEC-064 / DEC-067 / DEC-069).
///
/// DEC-067: Converted from IHostedService → BackgroundService so it does NOT block startup.
/// DEC-069: Robust logging + per-step scope + emergency fail-safe + connectivity check.
/// </summary>
public sealed class RealisticSeedHostedService : BackgroundService
{
    private readonly IServiceProvider _rootServiceProvider;
    private readonly ILogger<RealisticSeedHostedService> _logger;
    private readonly IConfiguration _config;
    private readonly JsonSeedLoader _seedLoader = new();  // DEC-087/088: JSON-driven seed

    private static readonly DateTime ScenarioStart = new(2024, 7, 1);
    private static readonly DateTime ScenarioEnd = new(2026, 7, 1);
    private const int CompaniesCount = 5;
    private const int VendorsCount = 15;
    private const int CustomersCount = 20;
    private const int ProjectsCount = 8;
    private const int GoodsReceiptsCount = 100;
    private const int BillsCount = 100;
    private const int SalesInvoicesCount = 50;
    private const int JournalEntriesCount = 200;
    private const int TotalMonths = 24;  // DEC-088: restored constant (was lost in refactor)
    private const int InitialDelayMs = 5000;
    private const int YieldEveryRecords = 50;
    private const int YieldSleepMs = 50;

    public RealisticSeedHostedService(
        IServiceProvider rootServiceProvider,
        ILogger<RealisticSeedHostedService> logger,
        IConfiguration config)
    {
        _rootServiceProvider = rootServiceProvider;
        _logger = logger;
        _config = config;

        // DEC-069: Log at construction time so we can see if service was instantiated
        logger.LogInformation("[DEC-069] RealisticSeedHostedService CONSTRUCTED");
        logger.LogInformation("[DEC-069] Config flag Database:SeedRealisticScenario = {Flag}",
            config.GetValue<bool?>("Database:SeedRealisticScenario") ?? false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // DEC-069: Log immediately to prove ExecuteAsync was called
        _logger.LogInformation("[DEC-069] RealisticSeedHostedService.ExecuteAsync ENTERED");

        var seedEnabled = _config.GetValue<bool?>("Database:SeedRealisticScenario") ?? false;
        if (!seedEnabled)
        {
            _logger.LogInformation("RealisticSeed: disabled (Database:SeedRealisticScenario = false)");
            return;
        }

        _logger.LogInformation("RealisticSeed: background mode — letting app start first ({Delay}ms)", InitialDelayMs);
        try
        {
            await Task.Delay(InitialDelayMs, stoppingToken);
        }
        catch (OperationCanceledException) { return; }

        _logger.LogInformation("RealisticSeed: starting 2-year scenario...");
        _logger.LogInformation("  Period: {Start:yyyy-MM} → {End:yyyy-MM}", ScenarioStart, ScenarioEnd);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // DEC-069: Connectivity sanity check BEFORE main work
            _logger.LogInformation("[DEC-069] Connectivity check...");
            var canConnect = await ConnectivityCheckAsync(stoppingToken);
            if (!canConnect)
            {
                _logger.LogError("[DEC-069] Connectivity check FAILED — aborting seed");
                return;
            }
            _logger.LogInformation("[DEC-069] Connectivity OK");

            // DEC-087/088: Load JSON seed data (for the 5 entities that have JSON files)
            var seedDataDir = Path.Combine(AppContext.BaseDirectory, "data-types", "seeds");
            if (!Directory.Exists(seedDataDir))
            {
                seedDataDir = Path.Combine(Directory.GetCurrentDirectory(), "data-types", "seeds");
            }
            _seedLoader.LoadFromDirectory(seedDataDir);
            _logger.LogInformation("[DEC-088] Loaded {N} seed files from {Path}",
                _seedLoader.Files.Count, seedDataDir);

            // DEC-069: Per-step scope (more reliable than one big scope)
            // DEC-070: GetOrCreateTenantAsync returns Guid, not List<Guid> — wrap in list for StepWithScopeAsync
            var tenantIdWrapper = await StepWithScopeAsync(
                "GetOrCreateTenant",
                async (factory, ct) =>
                {
                    var id = await GetOrCreateTenantAsync(factory, ct);
                    return id == Guid.Empty ? new List<Guid>() : new List<Guid> { id };
                },
                stoppingToken);
            var tenantId = tenantIdWrapper.FirstOrDefault();

            if (tenantId == Guid.Empty)
            {
                _logger.LogError("[DEC-069] Failed to get/create tenant — aborting seed");
                return;
            }
            _logger.LogInformation("[DEC-069] TenantId: {TenantId}", tenantId);

            await StepWithScopeAsync("Companies", (factory, ct) =>
                SeedCompaniesAsync(factory, tenantId, ct), stoppingToken);

            var vendorIds = await StepWithScopeAsync("Vendors", (factory, ct) =>
                SeedVendorsAsync(factory, tenantId, ct), stoppingToken);

            var customerIds = await StepWithScopeAsync("Customers", (factory, ct) =>
                SeedCustomersAsync(factory, tenantId, ct), stoppingToken);

            await StepWithScopeAsync("Projects", (factory, ct) =>
                SeedProjectsAsync(factory, tenantId, ct), stoppingToken);

            var itemIds = await StepWithScopeAsync("Items", (factory, ct) =>
                SeedItemsAsync(factory, tenantId, ct), stoppingToken);

            await StepWithScopeAsync("GoodsReceipts", (factory, ct) =>
                SeedGoodsReceiptsAsync(factory, tenantId, vendorIds, itemIds, ct), stoppingToken);

            await StepWithScopeAsync("Bills", (factory, ct) =>
                SeedBillsAsync(factory, tenantId, vendorIds, itemIds, ct), stoppingToken);

            await StepWithScopeAsync("SalesInvoices", (factory, ct) =>
                SeedSalesInvoicesAsync(factory, tenantId, customerIds, itemIds, ct), stoppingToken);

            await StepWithScopeAsync("JournalEntries", (factory, ct) =>
                SeedJournalEntriesAsync(factory, tenantId, ct), stoppingToken);

            sw.Stop();
            _logger.LogInformation("========================================");
            _logger.LogInformation("RealisticSeed: DONE in {Sec:F1}s", sw.Elapsed.TotalSeconds);
            _logger.LogInformation("========================================");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("RealisticSeed: cancelled (app shutting down)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DEC-069] RealisticSeed: top-level failure");
        }
    }

    private async Task<bool> ConnectivityCheckAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _rootServiceProvider.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var conn = await factory.CreateOltpConnectionAsync(ct);
            var result = await conn.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT 1", cancellationToken: ct));
            _logger.LogInformation("[DEC-069] SELECT 1 → {Result}", result);
            return result != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DEC-069] Connectivity check exception");
            return false;
        }
    }

    private async Task<List<Guid>> StepWithScopeAsync(
        string stepName,
        Func<IDbConnectionFactory, CancellationToken, Task<List<Guid>>> stepFn,
        CancellationToken ct)
    {
        _logger.LogInformation("[DEC-069] → {Step}", stepName);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await Task.Yield();
            using var scope = _rootServiceProvider.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            var result = await stepFn(factory, ct);
            sw.Stop();
            _logger.LogInformation("[DEC-069] ✓ {Step} done in {Sec:F1}s ({Count} records)",
                stepName, sw.Elapsed.TotalSeconds, result.Count);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[DEC-069] ✗ {Step} failed after {Sec:F1}s — continuing",
                stepName, sw.Elapsed.TotalSeconds);
            return new List<Guid>();
        }
    }

    // ... rest stays the same (GetOrCreateTenantAsync, SeedCompaniesAsync, etc.)
    // For brevity, these methods remain as in the original file
    private async Task<List<Guid>> GetExistingIdsAsync(
        IDbConnectionFactory factory, Guid tenantId, string table, CancellationToken ct)
    {
        using var conn = await factory.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT id FROM {table} WHERE tenant_id = @T";
        var ids = await conn.QueryAsync<Guid>(new CommandDefinition(sql, new { T = tenantId }, cancellationToken: ct));
        return ids.ToList();
    }

    // ==================== Chart of Accounts (DEC-090 Part 2) ====================

    private async Task<List<Guid>> SeedGlsAsync(
        IDbConnectionFactory factory, Guid tenantId, CancellationToken ct)
    {
        // DEC-090: Read GL accounts from JSON (data-types/seeds/seed_gls.json)
        var seedData = _seedLoader.GetFile("Account");
        if (seedData == null || seedData.Records == null || seedData.Records.Count == 0)
        {
            _logger.LogWarning("[DEC-090] seed_gls.json not found — skipping GLs seed");
            return await GetExistingIdsAsync(factory, tenantId, "accounts", ct);
        }

        using var conn = await factory.CreateOltpConnectionAsync(ct);
        var existing = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM accounts WHERE tenant_id = @T", new { T = tenantId }, cancellationToken: ct));
        if (existing >= seedData.Records.Count) return await GetExistingIdsAsync(factory, tenantId, "accounts", ct);

        // Look up parent accounts by code for self-referencing FK
        var accountIds = new Dictionary<string, Guid>();
        var glIds = new List<Guid>();
        foreach (var rec in seedData.Records)
        {
            var id = Guid.NewGuid();
            var code = GetStr(rec, "code");
            string? parentCode = null;
            // Parent accounts: 4-digit codes have 2-digit prefix (11, 21, 31, 41, 51, 61, 71)
            if (code.Length == 4)
            {
                var prefix = code.Substring(0, 2);
                var possibleParent = prefix + "00";
                if (code != possibleParent) parentCode = possibleParent;
            }

            Guid? parentId = null;
            if (parentCode != null)
            {
                if (accountIds.TryGetValue(parentCode, out var pid)) parentId = pid;
            }

            const string sql = @"
                INSERT INTO accounts (id, tenant_id, code, name, type, normal_balance, is_postable, is_active, created_at, updated_at, parent_account_id)
                VALUES (@Id, @T, @Code, @Name, @Type, @NormalBalance, @IsPostable, true, @Now, @Now, @ParentId)";
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = id,
                T = tenantId,
                Code = code,
                Name = GetStr(rec, "name"),
                Type = GetInt(rec, "type", 1),
                NormalBalance = GetInt(rec, "normal_balance", 1),
                IsPostable = GetBool(rec, "is_postable", true),
                ParentId = parentId,
                Now = DateTime.UtcNow
            }, cancellationToken: ct));
            accountIds[code] = id;
            glIds.Add(id);

            if (glIds.Count % YieldEveryRecords == 0) await YieldAsync(ct);
        }
        return glIds;
    }

    // ==================== Employees (DEC-090 Part 2) ====================

    private async Task<List<Guid>> SeedEmployeesAsync(
        IDbConnectionFactory factory, Guid tenantId, Guid adminUserId, CancellationToken ct)
    {
        // DEC-090: Read employees from JSON (data-types/seeds/seed_employees.json)
        var seedData = _seedLoader.GetFile("Employee");
        if (seedData == null || seedData.Records == null || seedData.Records.Count == 0)
        {
            _logger.LogWarning("[DEC-090] seed_employees.json not found — skipping employees seed");
            return new List<Guid>();
        }

        using var conn = await factory.CreateOltpConnectionAsync(ct);
        var existing = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM employees WHERE tenant_id = @T", new { T = tenantId }, cancellationToken: ct));
        if (existing >= seedData.Records.Count) return new List<Guid>();

        // Look up first department for FK reference
        var firstDept = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM departments WHERE tenant_id = @T ORDER BY created_at ASC LIMIT 1",
            new { T = tenantId }, cancellationToken: ct));

        var empIds = new List<Guid>();
        foreach (var rec in seedData.Records)
        {
            var id = Guid.NewGuid();
            const string sql = @"
                INSERT INTO employees (id, tenant_id, employee_number, full_name, email, phone, national_id, department_id, job_title, hire_date, base_salary, is_active, created_at, created_by, updated_at, updated_by)
                VALUES (@Id, @T, @EmpNum, @FullName, @Email, @Phone, @NationalId, @DeptId, @JobTitle, @HireDate, @BaseSalary, true, @Now, @User, @Now, @User)";
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = id,
                T = tenantId,
                EmpNum = GetStr(rec, "employee_number"),
                FullName = GetStr(rec, "full_name"),
                Email = GetStrOrNull(rec, "email"),
                Phone = GetStrOrNull(rec, "phone"),
                NationalId = GetStrOrNull(rec, "national_id"),
                DeptId = firstDept,
                JobTitle = GetStrOrNull(rec, "job_title"),
                HireDate = DateTime.TryParse(GetStrOrNull(rec, "hire_date") ?? "", out var d) ? d : ScenarioStart,
                BaseSalary = GetDecimal(rec, "base_salary", 5000m),
                Now = DateTime.UtcNow,
                User = adminUserId
            }, cancellationToken: ct));
            empIds.Add(id);

            if (empIds.Count % YieldEveryRecords == 0) await YieldAsync(ct);
        }
        return empIds;
    }

    // ==================== Cost Centers (DEC-090 Part 2) ====================

    private async Task<List<Guid>> SeedCostCentersAsync(
        IDbConnectionFactory factory, Guid tenantId, Guid adminUserId, CancellationToken ct)
    {
        // DEC-090: Read cost centers from JSON (data-types/seeds/seed_cost_centers.json)
        var seedData = _seedLoader.GetFile("CostCenter");
        if (seedData == null || seedData.Records == null || seedData.Records.Count == 0)
        {
            _logger.LogWarning("[DEC-090] seed_cost_centers.json not found — skipping cost centers seed");
            return new List<Guid>();
        }

        using var conn = await factory.CreateOltpConnectionAsync(ct);
        var existing = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM cost_centers WHERE tenant_id = @T", new { T = tenantId }, cancellationToken: ct));
        if (existing >= seedData.Records.Count) return new List<Guid>();

        // Look up first company for FK reference
        var firstCompany = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM companies WHERE tenant_id = @T ORDER BY created_at ASC LIMIT 1",
            new { T = tenantId }, cancellationToken: ct));

        var ccIds = new List<Guid>();
        foreach (var rec in seedData.Records)
        {
            var id = Guid.NewGuid();
            const string sql = @"
                INSERT INTO cost_centers (id, tenant_id, company_id, code, name, type, budget_amount, is_active, created_at, created_by, updated_at, updated_by)
                VALUES (@Id, @T, @CompanyId, @Code, @Name, @Type, @Budget, true, @Now, @User, @Now, @User)";
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = id,
                T = tenantId,
                CompanyId = firstCompany,
                Code = GetStr(rec, "code"),
                Name = GetStr(rec, "name"),
                Type = GetInt(rec, "type", 0),
                Budget = GetDecimal(rec, "budget_amount", 0m),
                Now = DateTime.UtcNow,
                User = adminUserId
            }, cancellationToken: ct));
            ccIds.Add(id);

            if (ccIds.Count % YieldEveryRecords == 0) await YieldAsync(ct);
        }
        return ccIds;
    }

    // ==================== Purchase Orders (DEC-090 Part 2) ====================

    private async Task<List<Guid>> SeedPOsAsync(
        IDbConnectionFactory factory, Guid tenantId, Guid adminUserId, List<Guid> vendorIds, List<Guid> itemIds, CancellationToken ct)
    {
        // DEC-090: Read POs from JSON (data-types/seeds/seed_pos.json)
        var seedData = _seedLoader.GetFile("PurchaseOrder");
        if (seedData == null || seedData.Records == null || seedData.Records.Count == 0)
        {
            _logger.LogWarning("[DEC-090] seed_pos.json not found — skipping POs seed");
            return new List<Guid>();
        }

        if (vendorIds.Count == 0 || itemIds.Count == 0) return new List<Guid>();

        using var conn = await factory.CreateOltpConnectionAsync(ct);
        var existing = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM purchase_orders WHERE tenant_id = @T", new { T = tenantId }, cancellationToken: ct));
        if (existing >= seedData.Records.Count) return await GetExistingIdsAsync(factory, tenantId, "purchase_orders", ct);

        var firstVendor = vendorIds[0];

        var poIds = new List<Guid>();
        foreach (var rec in seedData.Records)
        {
            var id = Guid.NewGuid();
            // DEC-085: 'currency_code' (NOT 'currency')
            const string sql = @"
                INSERT INTO purchase_orders (id, tenant_id, po_number, vendor_id, status, order_date, expected_date, currency_code, created_at, created_by, updated_at, updated_by)
                VALUES (@Id, @T, @PoNum, @Vendor, 2, @OrderDate, @ExpectedDate, 'LYD', @Now, @User, @Now, @User)";
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = id,
                T = tenantId,
                PoNum = GetStr(rec, "po_number"),
                Vendor = firstVendor,
                OrderDate = DateTime.TryParse(GetStrOrNull(rec, "order_date") ?? "", out var d) ? d : ScenarioStart,
                ExpectedDate = DateTime.TryParse(GetStrOrNull(rec, "expected_date") ?? "", out var ed) ? ed : ScenarioStart.AddMonths(1),
                Now = DateTime.UtcNow,
                User = adminUserId
            }, cancellationToken: ct));
            poIds.Add(id);

            if (poIds.Count % YieldEveryRecords == 0) await YieldAsync(ct);
        }
        return poIds;
    }

    // ==================== Tenant ====================

    private async Task<Guid> GetOrCreateTenantAsync(IDbConnectionFactory factory, CancellationToken ct)
    {
        using var conn = await factory.CreateOltpConnectionAsync(ct);
        const string findSql = "SELECT id FROM tenants WHERE subdomain = @sub LIMIT 1";
        var existing = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            findSql, new { sub = "alfajr" }, cancellationToken: ct));
        if (existing.HasValue)
        {
            _logger.LogInformation("[RealisticSeed] Using existing tenant: {TenantId}", existing.Value);
            return existing.Value;
        }
        var newId = Guid.NewGuid();
        const string insertSql = @"
            INSERT INTO tenants (id, name, subdomain, is_active, created_at)
            VALUES (@Id, 'AlFajr Holding', 'alfajr', true, @Now)";
        await conn.ExecuteAsync(new CommandDefinition(insertSql, new
        {
            Id = newId,
            Now = DateTime.UtcNow
        }, cancellationToken: ct));
        return newId;
    }

    // ==================== Companies (5) ====================

    private async Task<List<Guid>> SeedCompaniesAsync(
        IDbConnectionFactory factory, Guid tenantId, CancellationToken ct)
    {
        var companies = new (string code, string name)[]
        {
            ("ALF", "AlFajr Trading & Contracting"),
            ("ALB", "AlBurj Building Materials"),
            ("ALN", "AlNoor Office Supplies"),
            ("ALK", "AlKawn Food Services"),
            ("ALKH", "AlNakhla Tourism & Cleaning")
        };

        using var conn = await factory.CreateOltpConnectionAsync(ct);
        var existing = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM companies WHERE tenant_id = @T", new { T = tenantId }, cancellationToken: ct));
        if (existing >= CompaniesCount) return await GetExistingIdsAsync(factory, tenantId, "companies", ct);

        var companyIds = new List<Guid>();
        for (int i = 0; i < companies.Length; i++)
        {
            var id = Guid.NewGuid();
            const string sql = @"
                INSERT INTO companies (id, tenant_id, code, name, currency, is_active, created_at, updated_at, created_by, updated_by)
                VALUES (@Id, @T, @Code, @Name, 'LYD', true, @Now, @Now, @User, @User)";
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = id,
                T = tenantId,
                Code = companies[i].code,
                Name = companies[i].name,
                Now = DateTime.UtcNow,
                User = Guid.Empty
            }, cancellationToken: ct));
            companyIds.Add(id);

            if ((i + 1) % YieldEveryRecords == 0) await YieldAsync(ct);
        }
        return companyIds;
    }

    // ==================== Vendors (25) ====================

    private async Task<List<Guid>> SeedVendorsAsync(
        IDbConnectionFactory factory, Guid tenantId, CancellationToken ct)
    {
        // DEC-088: Read vendor data from JSON (data-types/seeds/seed_vendors.json)
        var seedData = _seedLoader.GetFile("Vendor");
        if (seedData == null || seedData.Records == null || seedData.Records.Count == 0)
        {
            _logger.LogWarning("[DEC-088] seed_vendors.json not found — skipping vendors seed");
            return await GetExistingIdsAsync(factory, tenantId, "vendors", ct);
        }

        using var conn = await factory.CreateOltpConnectionAsync(ct);
        var existing = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM vendors WHERE tenant_id = @T", new { T = tenantId }, cancellationToken: ct));
        if (existing >= seedData.Records.Count) return await GetExistingIdsAsync(factory, tenantId, "vendors", ct);

        var vendorIds = new List<Guid>();
        foreach (var rec in seedData.Records)
        {
            var id = Guid.NewGuid();
            // DEC-072: 'contact_name' and 'balance' columns were dropped; 'email'/'phone'/'address'/'tax_number' added
            const string sql = @"
                INSERT INTO vendors (id, tenant_id, code, name, email, phone, address, tax_number, currency, payment_terms, is_active, created_at, updated_at, created_by, updated_by)
                VALUES (@Id, @T, @Code, @Name, @Email, @Phone, @Address, @Tax, @Currency, @PaymentTerms, true, @Now, @Now, @User, @User)";
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = id,
                T = tenantId,
                Code = GetStr(rec, "code"),
                Name = GetStr(rec, "name"),
                Email = GetStrOrNull(rec, "email") ?? "",
                Phone = GetStrOrNull(rec, "phone") ?? "",
                Address = GetStrOrNull(rec, "address") ?? "",
                Tax = GetStrOrNull(rec, "tax_number"),
                Currency = GetStr(rec, "currency", "LYD"),
                PaymentTerms = GetStr(rec, "payment_terms", "Net30"),
                Now = DateTime.UtcNow,
                User = Guid.Empty
            }, cancellationToken: ct));
            vendorIds.Add(id);

            if (vendorIds.Count % YieldEveryRecords == 0) await YieldAsync(ct);
        }
        return vendorIds;
    }

    // ==================== Customers (20) ====================

    private async Task<List<Guid>> SeedCustomersAsync(
        IDbConnectionFactory factory, Guid tenantId, CancellationToken ct)
    {
        // DEC-088: Read customer data from JSON (data-types/seeds/seed_customers.json)
        var seedData = _seedLoader.GetFile("Customer");
        if (seedData == null || seedData.Records == null || seedData.Records.Count == 0)
        {
            _logger.LogWarning("[DEC-088] seed_customers.json not found — skipping customers seed");
            return await GetExistingIdsAsync(factory, tenantId, "customers", ct);
        }

        using var conn = await factory.CreateOltpConnectionAsync(ct);
        var existing = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM customers WHERE tenant_id = @T", new { T = tenantId }, cancellationToken: ct));
        if (existing >= seedData.Records.Count) return await GetExistingIdsAsync(factory, tenantId, "customers", ct);

        var customerIds = new List<Guid>();
        foreach (var rec in seedData.Records)
        {
            var id = Guid.NewGuid();
            // DEC-072: 'type' and 'balance' columns were dropped; 'tax_id' and 'payment_terms_days' added
            const string sql = @"
                INSERT INTO customers (id, tenant_id, code, name, tax_id, email, phone, address, credit_limit, payment_terms_days, is_active, created_at, updated_at, created_by, updated_by)
                VALUES (@Id, @T, @Code, @Name, @TaxId, @Email, @Phone, @Address, @CreditLimit, @PaymentTermsDays, true, @Now, @Now, @User, @User)";
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = id,
                T = tenantId,
                Code = GetStr(rec, "code"),
                Name = GetStr(rec, "name"),
                TaxId = GetStrOrNull(rec, "tax_id"),
                Email = GetStrOrNull(rec, "email") ?? "",
                Phone = GetStrOrNull(rec, "phone") ?? "",
                Address = GetStrOrNull(rec, "address") ?? "",
                CreditLimit = GetDecimal(rec, "credit_limit", 0m),
                PaymentTermsDays = GetInt(rec, "payment_terms_days", 30),
                Now = DateTime.UtcNow,
                User = Guid.Empty
            }, cancellationToken: ct));
            customerIds.Add(id);

            if (customerIds.Count % YieldEveryRecords == 0) await YieldAsync(ct);
        }
        return customerIds;
    }

    // ==================== Projects (8) ====================

    private async Task<List<Guid>> SeedProjectsAsync(
        IDbConnectionFactory factory, Guid tenantId, CancellationToken ct)
    {
        // DEC-088: Read project data from JSON (data-types/seeds/seed_projects.json)
        var seedData = _seedLoader.GetFile("Project");
        if (seedData == null || seedData.Records == null || seedData.Records.Count == 0)
        {
            _logger.LogWarning("[DEC-088] seed_projects.json not found — skipping projects seed");
            return await GetExistingIdsAsync(factory, tenantId, "projects", ct);
        }

        using var conn = await factory.CreateOltpConnectionAsync(ct);
        var existing = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM projects WHERE tenant_id = @T", new { T = tenantId }, cancellationToken: ct));
        if (existing >= seedData.Records.Count) return await GetExistingIdsAsync(factory, tenantId, "projects", ct);

        // Look up the first company + cost_center for FK references
        var companyId = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM companies WHERE tenant_id = @T ORDER BY created_at ASC LIMIT 1",
            new { T = tenantId }, cancellationToken: ct));
        if (companyId == null)
        {
            _logger.LogWarning("[DEC-088] No company found for project FKs — skipping");
            return new List<Guid>();
        }
        var costCenterId = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM cost_centers WHERE tenant_id = @T ORDER BY created_at ASC LIMIT 1",
            new { T = tenantId }, cancellationToken: ct));

        var projectIds = new List<Guid>();
        foreach (var rec in seedData.Records)
        {
            var id = Guid.NewGuid();
            const string sql = @"
                INSERT INTO projects (id, tenant_id, company_id, cost_center_id, code, name, description, status, budget, start_date, end_date, created_at, updated_at, created_by, updated_by)
                VALUES (@Id, @T, @CompanyId, @CCId, @Code, @Name, @Desc, @Status, @Budget, @Start, @End, @Now, @Now, @User, @User)";
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = id,
                T = tenantId,
                CompanyId = companyId.Value,
                CCId = costCenterId ?? companyId,  // fallback: use company_id if no cost center
                Code = GetStr(rec, "code"),
                Name = GetStr(rec, "name"),
                Desc = GetStrOrNull(rec, "description") ?? GetStr(rec, "name"),
                Status = GetInt(rec, "status", 0),
                Budget = GetDecimal(rec, "budget", 0m),
                Start = DateTime.Parse(GetStr(rec, "start_date", ScenarioStart.ToString("o"))),
                End = DateTime.TryParse(GetStrOrNull(rec, "end_date") ?? "", out var e) ? e : (DateTime?)null,
                Now = DateTime.UtcNow,
                User = Guid.Empty
            }, cancellationToken: ct));
            projectIds.Add(id);

            if (projectIds.Count % YieldEveryRecords == 0) await YieldAsync(ct);
        }
        return projectIds;
    }

    // ==================== Items ====================

    private async Task<List<Guid>> SeedItemsAsync(
        IDbConnectionFactory factory, Guid tenantId, CancellationToken ct)
    {
        // DEC-088: Read item data from JSON (data-types/seeds/seed_items.json)
        var seedData = _seedLoader.GetFile("Item");
        if (seedData == null || seedData.Records == null || seedData.Records.Count == 0)
        {
            _logger.LogWarning("[DEC-088] seed_items.json not found — skipping items seed");
            return await GetExistingIdsAsync(factory, tenantId, "items", ct);
        }

        using var conn = await factory.CreateOltpConnectionAsync(ct);
        var existing = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM items WHERE tenant_id = @T", new { T = tenantId }, cancellationToken: ct));
        if (existing >= seedData.Records.Count) return await GetExistingIdsAsync(factory, tenantId, "items", ct);

        // Look up FK references (company + uom)
        var companyId = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM companies WHERE tenant_id = @T ORDER BY created_at ASC LIMIT 1",
            new { T = tenantId }, cancellationToken: ct));
        if (companyId == null)
        {
            _logger.LogWarning("[DEC-088] No company found for item FKs — skipping");
            return new List<Guid>();
        }
        var uomId = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM units_of_measure WHERE tenant_id = @T ORDER BY created_at ASC LIMIT 1",
            new { T = tenantId }, cancellationToken: ct));

        var itemIds = new List<Guid>();
        foreach (var rec in seedData.Records)
        {
            var id = Guid.NewGuid();
            // DEC-085 fix: item_type and costing_method are integers (not 'Stock'/'Average' strings)
            const string sql = @"
                INSERT INTO items (id, tenant_id, company_id, sku, name, item_type, costing_method, unit_of_measure_id, average_cost, standard_cost, reorder_level, reorder_quantity, is_active, created_at, updated_at, created_by, updated_by)
                VALUES (@Id, @T, @CompanyId, @Sku, @Name, @ItemType, @CostingMethod, @UoM, @AvgCost, @StdCost, @ReorderLevel, @ReorderQty, true, @Now, @Now, @User, @User)";
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = id,
                T = tenantId,
                CompanyId = companyId.Value,
                Sku = GetStr(rec, "sku"),
                Name = GetStr(rec, "name"),
                ItemType = GetInt(rec, "item_type", 1),
                CostingMethod = GetInt(rec, "costing_method", 3),
                UoM = uomId ?? Guid.Empty,
                AvgCost = GetDecimal(rec, "average_cost", 0m),
                StdCost = GetDecimal(rec, "standard_cost", 0m),
                ReorderLevel = GetDecimal(rec, "reorder_level", 0m),
                ReorderQty = GetDecimal(rec, "reorder_quantity", 0m),
                Now = DateTime.UtcNow,
                User = Guid.Empty
            }, cancellationToken: ct));
            itemIds.Add(id);

            if (itemIds.Count % YieldEveryRecords == 0) await YieldAsync(ct);
        }
        return itemIds;
    }

    // ==================== GRs (100) ====================

    private async Task<List<Guid>> SeedGoodsReceiptsAsync(
        IDbConnectionFactory factory, Guid tenantId, List<Guid> vendorIds, List<Guid> itemIds, CancellationToken ct)
    {
        // DEC-090: Read GR data from JSON (data-types/seeds/seed_grns.json)
        var seedData = _seedLoader.GetFile("GoodsReceipt");
        if (seedData == null || seedData.Records == null || seedData.Records.Count == 0)
        {
            _logger.LogWarning("[DEC-090] seed_grns.json not found — skipping goods_receipts seed");
            return await GetExistingIdsAsync(factory, tenantId, "goods_receipts", ct);
        }

        if (vendorIds.Count == 0 || itemIds.Count == 0) return new List<Guid>();

        using var conn = await factory.CreateOltpConnectionAsync(ct);
        var existing = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM goods_receipts WHERE tenant_id = @T", new { T = tenantId }, cancellationToken: ct));
        if (existing >= seedData.Records.Count) return await GetExistingIdsAsync(factory, tenantId, "goods_receipts", ct);

        // DEC-090: Look up the first PO + warehouse for FK references
        var firstPo = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM purchase_orders WHERE tenant_id = @T ORDER BY created_at ASC LIMIT 1",
            new { T = tenantId }, cancellationToken: ct));
        if (firstPo == null)
        {
            _logger.LogWarning("[DEC-090] No PO found for GR FKs — skipping goods_receipts seed");
            return new List<Guid>();
        }
        var firstWarehouse = await conn.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM warehouses WHERE tenant_id = @T ORDER BY created_at ASC LIMIT 1",
            new { T = tenantId }, cancellationToken: ct));

        var grIds = new List<Guid>();
        foreach (var rec in seedData.Records)
        {
            var id = Guid.NewGuid();
            const string sql = @"
                INSERT INTO goods_receipts (id, tenant_id, gr_number, purchase_order_id, vendor_id, warehouse_id, status, receipt_date, total_amount, currency, created_at, updated_at, created_by, updated_by)
                VALUES (@Id, @T, @GrNumber, @PO, @Vendor, @Warehouse, 2, @Date, @Amount, 'LYD', @Now, @Now, @User, @User)";
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = id,
                T = tenantId,
                GrNumber = GetStr(rec, "gr_number"),
                PO = firstPo,
                Vendor = vendorIds.Count > 0 ? vendorIds[0] : Guid.Empty,
                Warehouse = firstWarehouse ?? Guid.Empty,
                Date = DateTime.TryParse(GetStrOrNull(rec, "received_date") ?? "", out var d) ? d : ScenarioStart,
                Amount = 1500m + (id.GetHashCode() % 5000),
                Now = DateTime.UtcNow,
                User = Guid.Empty
            }, cancellationToken: ct));
            grIds.Add(id);

            if (grIds.Count % YieldEveryRecords == 0) await YieldAsync(ct);
        }
        return grIds;
    }

    // ==================== Bills (100, with line items) ====================

    private async Task<List<Guid>> SeedBillsAsync(
        IDbConnectionFactory factory, Guid tenantId, List<Guid> vendorIds, List<Guid> itemIds, CancellationToken ct)
    {
        // DEC-090: Read bill headers from JSON. Line items remain in C# (future DEC-091 will move them).
        var seedData = _seedLoader.GetFile("VendorBill");
        if (seedData == null || seedData.Records == null || seedData.Records.Count == 0)
        {
            _logger.LogWarning("[DEC-090] seed_bills.json not found — skipping bills seed");
            return await GetExistingIdsAsync(factory, tenantId, "vendor_bills", ct);
        }

        if (vendorIds.Count == 0 || itemIds.Count == 0) return new List<Guid>();

        using var conn = await factory.CreateOltpConnectionAsync(ct);
        var existing = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM vendor_bills WHERE tenant_id = @T", new { T = tenantId }, cancellationToken: ct));
        if (existing >= seedData.Records.Count) return await GetExistingIdsAsync(factory, tenantId, "vendor_bills", ct);

        // DEC-090: Look up the first vendor for FK reference
        var firstVendor = vendorIds[0];

        var billIds = new List<Guid>();
        foreach (var rec in seedData.Records)
        {
            var id = Guid.NewGuid();
            const string sql = @"
                INSERT INTO vendor_bills (id, tenant_id, bill_number, vendor_id, status, bill_date, due_date, total_amount, currency, created_at, updated_at, created_by, updated_by)
                VALUES (@Id, @T, @BillNumber, @Vendor, 2, @BillDate, @DueDate, @Amount, 'LYD', @Now, @Now, @User, @User)";
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = id,
                T = tenantId,
                BillNumber = GetStr(rec, "bill_number"),
                Vendor = firstVendor,
                BillDate = DateTime.Parse(GetStrOrNull(rec, "bill_date") ?? DateTime.UtcNow.AddMonths(-6).ToString("o")),
                DueDate = DateTime.Parse(GetStrOrNull(rec, "due_date") ?? DateTime.UtcNow.AddMonths(-3).ToString("o")),
                Amount = GetDecimal(rec, "total_amount", 5000m),
                Now = DateTime.UtcNow,
                User = Guid.Empty
            }, cancellationToken: ct));
            billIds.Add(id);

            if (billIds.Count % YieldEveryRecords == 0) await YieldAsync(ct);
        }
        return billIds;
    }

    // ==================== Sales Invoices (50) ====================

    private async Task<List<Guid>> SeedSalesInvoicesAsync(
        IDbConnectionFactory factory, Guid tenantId, List<Guid> customerIds, List<Guid> itemIds, CancellationToken ct)
    {
        // DEC-090: Read sales invoice headers from JSON. Line items remain in C# (DEC-085 fix: 'currency_code')
        var seedData = _seedLoader.GetFile("SalesInvoice");
        if (seedData == null || seedData.Records == null || seedData.Records.Count == 0)
        {
            _logger.LogWarning("[DEC-090] seed_sales_invoices.json not found — skipping sales invoices seed");
            return await GetExistingIdsAsync(factory, tenantId, "sales_invoices", ct);
        }

        if (customerIds.Count == 0 || itemIds.Count == 0) return new List<Guid>();

        using var conn = await factory.CreateOltpConnectionAsync(ct);
        var existing = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM sales_invoices WHERE tenant_id = @T", new { T = tenantId }, cancellationToken: ct));
        if (existing >= seedData.Records.Count) return await GetExistingIdsAsync(factory, tenantId, "sales_invoices", ct);

        // DEC-090: Look up the first customer for FK reference
        var firstCustomer = customerIds[0];

        var invIds = new List<Guid>();
        foreach (var rec in seedData.Records)
        {
            var id = Guid.NewGuid();
            // DEC-085 fix: column is named 'currency_code', not 'currency'
            const string sql = @"
                INSERT INTO sales_invoices (id, tenant_id, invoice_number, customer_id, status, invoice_date, due_date, total_amount, currency_code, created_at, updated_at, created_by, updated_by)
                VALUES (@Id, @T, @InvoiceNumber, @Customer, 2, @IssueDate, @DueDate, @Amount, @Currency, @Now, @Now, @User, @User)";
            await conn.ExecuteAsync(new CommandDefinition(sql, new
            {
                Id = id,
                T = tenantId,
                InvoiceNumber = GetStr(rec, "invoice_number"),
                Customer = firstCustomer,
                IssueDate = DateTime.Parse(GetStrOrNull(rec, "issue_date") ?? DateTime.UtcNow.AddMonths(-6).ToString("o")),
                DueDate = DateTime.Parse(GetStrOrNull(rec, "due_date") ?? DateTime.UtcNow.AddMonths(-3).ToString("o")),
                Amount = GetDecimal(rec, "total_amount", 5000m),
                Currency = GetStr(rec, "currency_code", "LYD"),
                Now = DateTime.UtcNow,
                User = Guid.Empty
            }, cancellationToken: ct));
            invIds.Add(id);

            if (invIds.Count % YieldEveryRecords == 0) await YieldAsync(ct);
        }
        return invIds;
    }

    // ==================== Journal Entries (200+, balanced) ====================

    private async Task<List<Guid>> SeedJournalEntriesAsync(
        IDbConnectionFactory factory, Guid tenantId, CancellationToken ct)
    {
        using var conn = await factory.CreateOltpConnectionAsync(ct);
        var existing = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM journal_entries WHERE tenant_id = @T", new { T = tenantId }, cancellationToken: ct));
        if (existing >= JournalEntriesCount) return new List<Guid>();

        var rng = new Random(2024);
        var jeIds = new List<Guid>();
        for (int i = 1; i <= JournalEntriesCount; i++)
        {
            var id = Guid.NewGuid();
            var monthOffset = rng.Next(0, TotalMonths);
            var day = rng.Next(1, 28);
            var date = ScenarioStart.AddMonths(monthOffset).AddDays(day);
            if (date > DateTime.UtcNow) date = DateTime.UtcNow.AddDays(-rng.Next(1, 30));

            var amount = 1000m + (rng.Next(0, 9000) * 1m);
            var reference = $"JV-{i:D5}";

            const string insertJE = @"
                INSERT INTO journal_entries (id, tenant_id, entry_number, reference, entry_date, status, total_debit, total_credit, currency, created_at, updated_at, created_by, updated_by)
                VALUES (@Id, @T, @EntryNumber, @Reference, @Date, 2, @Amount, @Amount, 'LYD', @Now, @Now, @User, @User)";
            await conn.ExecuteAsync(new CommandDefinition(insertJE, new
            {
                Id = id,
                T = tenantId,
                EntryNumber = reference,
                Reference = $"Auto-generated JV {i}",
                Date = date,
                Amount = amount,
                Now = DateTime.UtcNow,
                User = Guid.Empty
            }, cancellationToken: ct));

            const string insertLine = @"
                INSERT INTO journal_entry_lines (id, tenant_id, journal_entry_id, account_id, type, amount, description, created_at, updated_at, created_by, updated_by)
                VALUES (@Id, @T, @JE, @Account, @Type, @Amount, @Desc, @Now, @Now, @User, @User)";
            // Debit
            await conn.ExecuteAsync(new CommandDefinition(insertLine, new
            {
                Id = Guid.NewGuid(),
                T = tenantId,
                JE = id,
                Account = Guid.NewGuid(),
                Type = "Debit",
                Amount = amount,
                Desc = $"Debit for {reference}",
                Now = DateTime.UtcNow,
                User = Guid.Empty
            }, cancellationToken: ct));
            // Credit
            await conn.ExecuteAsync(new CommandDefinition(insertLine, new
            {
                Id = Guid.NewGuid(),
                T = tenantId,
                JE = id,
                Account = Guid.NewGuid(),
                Type = "Credit",
                Amount = amount,
                Desc = $"Credit for {reference}",
                Now = DateTime.UtcNow,
                User = Guid.Empty
            }, cancellationToken: ct));
            jeIds.Add(id);

            if (i % YieldEveryRecords == 0) await YieldAsync(ct);
        }
        return jeIds;
    }

    /// <summary>
    /// Yields to thread pool + small delay so HTTP requests can be served.
    /// Prevents the seed from starving the Kestrel listener.
    /// </summary>
    private async Task YieldAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(YieldSleepMs, ct);
        }
        catch (OperationCanceledException) { /* expected on shutdown */ }
    }

    // ==================== JSON helpers (DEC-087) ====================

    private static string GetStr(Dictionary<string, object> rec, string key, string fallback = "")
    {
        if (rec.TryGetValue(key, out var v) && v != null) return v.ToString() ?? fallback;
        return fallback;
    }

    private static string? GetStrOrNull(Dictionary<string, object> rec, string key)
    {
        if (rec.TryGetValue(key, out var v) && v != null) return v.ToString();
        return null;
    }

    private static bool GetBool(Dictionary<string, object> rec, string key, bool fallback = false)
    {
        if (rec.TryGetValue(key, out var v) && v != null)
        {
            if (v is bool b) return b;
            if (bool.TryParse(v.ToString(), out var parsed)) return parsed;
        }
        return fallback;
    }

    private static int GetInt(Dictionary<string, object> rec, string key, int fallback = 0)
    {
        if (rec.TryGetValue(key, out var v) && v != null)
        {
            if (v is long l) return (int)l;
            if (v is int i) return i;
            if (int.TryParse(v.ToString(), out var parsed)) return parsed;
        }
        return fallback;
    }

    private static decimal GetDecimal(Dictionary<string, object> rec, string key, decimal fallback = 0m)
    {
        if (rec.TryGetValue(key, out var v) && v != null)
        {
            if (v is decimal d) return d;
            if (v is double dbl) return (decimal)dbl;
            if (v is long l) return l;
            if (decimal.TryParse(v.ToString(), out var parsed)) return parsed;
        }
        return fallback;
    }
}
