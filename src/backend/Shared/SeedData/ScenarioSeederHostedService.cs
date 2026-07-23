using System.Data;
using Dapper;
using ERPSystem.Host.Utilities;
using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Application.Services;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Finance.Infrastructure;
using ERPSystem.Modules.HR.Application;
using ERPSystem.Modules.HR.Application.Services;
using ERPSystem.Modules.HR.Entities;
using ERPSystem.Modules.HR.Infrastructure;
using ERPSystem.Modules.Identity.Application.Auth;
using ERPSystem.Modules.Identity.Infrastructure;
using ERPSystem.Modules.Inventory.Application.Services;
using ERPSystem.Modules.Inventory.Entities;
using ERPSystem.Modules.Inventory.Infrastructure;
using ERPSystem.Modules.Payroll.Application;
using ERPSystem.Modules.Payroll.Application.Services;
using ERPSystem.Modules.Payroll.Domain.Entities;
using ERPSystem.Modules.Procurement.Application;
using ERPSystem.Modules.Procurement.Application.Services;
using ERPSystem.Modules.Procurement.Entities;
using ERPSystem.Modules.Projects.Application;
using ERPSystem.Modules.Projects.Application.Services;
using ERPSystem.Modules.Projects.Entities;
using ERPSystem.Shared.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ERPSystem.Shared.SeedData;

/// <summary>
/// يولّد بيانات وهمية تمثل سنة تشغيلية كاملة (2026) لشركة AlFajr Trading &amp; Contracting.
/// يشغّل مرة وحدة على startup إذا كان appsettings Database:SeedScenario = true.
/// </summary>
public sealed class ScenarioSeederHostedService : IHostedService
{
    private readonly IServiceProvider _rootServiceProvider;
    private readonly ILogger<ScenarioSeederHostedService> _logger;
    private readonly IConfiguration _config;

    private const string TenantEmail = "admin@alfajr.local";
    private const string TenantPassword = "Demo1234";

    public ScenarioSeederHostedService(
        IServiceProvider rootServiceProvider,
        ILogger<ScenarioSeederHostedService> logger,
        IConfiguration config)
    {
        _rootServiceProvider = rootServiceProvider;
        _logger = logger;
        _config = config;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var seedEnabled = _config.GetValue<bool?>("Database:SeedScenario") ?? false;
        if (!seedEnabled)
        {
            _logger.LogInformation("ScenarioSeeder: معطّل (Database:SeedScenario = false)");
            return;
        }

        _logger.LogInformation("========================================");
        _logger.LogInformation("ScenarioSeeder: بدء توليد البيانات لشركة AlFajr...");
        _logger.LogInformation("========================================");

        var scope = _rootServiceProvider.CreateScope();
        var services = scope.ServiceProvider;

        // 1. Register tenant + admin user
        var (tenantId, adminUserId) = await RegisterTenantAsync(services, cancellationToken);
        if (tenantId == Guid.Empty) { _logger.LogError("فشل إنشاء المستأجر"); return; }

        // 2. Seed all modules (each creates its own connection)
        await SeedExtraAccountsAsync(services, tenantId, cancellationToken);
        await SeedDepartmentsAsync(services, tenantId, cancellationToken);
        var deptIds = await GetDepartmentIdsAsync(services, tenantId, cancellationToken);
        var empIds = await SeedEmployeesAsync(services, tenantId, adminUserId, deptIds, cancellationToken);
        await SeedSalaryStructuresAsync(services, tenantId, adminUserId, cancellationToken);
        await SeedAttendanceAsync(services, tenantId, empIds, cancellationToken);
        await SeedLeaveRequestsAsync(services, tenantId, adminUserId, empIds, cancellationToken);
        await SeedVendorsAsync(services, tenantId, cancellationToken);
        var vendorIds = await GetVendorIdsAsync(services, tenantId, cancellationToken);
        await SeedWarehousesAndItemsAsync(services, tenantId, adminUserId, cancellationToken);
        var (itemIds, warehouseIds) = await GetItemAndWarehouseIdsAsync(services, tenantId, cancellationToken);
        await SeedPurchaseOrdersAsync(services, tenantId, adminUserId, vendorIds, itemIds, cancellationToken);
        await SeedProcurementFlowAsync(services, tenantId, adminUserId, cancellationToken);
        await SeedPayrollRunsAsync(services, tenantId, adminUserId, cancellationToken);
        await SeedManualJournalEntriesAsync(services, tenantId, adminUserId, cancellationToken);
        await SeedOpeningBalanceAsync(services, tenantId, adminUserId, cancellationToken);  // DEC-075
        await SeedProjectsAsync(services, tenantId, adminUserId, cancellationToken);

        _logger.LogInformation("========================================");
        _logger.LogInformation("ScenarioSeeder: تمت بنجاح!");
        _logger.LogInformation("  Tenant: AlFajr Trading & Contracting");
        _logger.LogInformation("  Login: {Email} / {Password}", TenantEmail, TenantPassword);
        _logger.LogInformation("  TenantId: {TenantId}", tenantId);
        _logger.LogInformation("========================================");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Helper: create a new connection for each operation
    private async Task<NpgsqlConnection> OpenConnectionAsync(IServiceProvider services, CancellationToken ct)
    {
        var factory = services.GetRequiredService<IDbConnectionFactory>();
        var conn = (NpgsqlConnection)(await factory.CreateOltpConnectionAsync(ct));
        return conn;
    }

    // ============================================================
    // STEP 1: Register Tenant + Admin
    // ============================================================
    private async Task<(Guid tenantId, Guid adminUserId)> RegisterTenantAsync(IServiceProvider services, CancellationToken ct)
    {
        var auth = services.GetRequiredService<IAuthService>();
        var req = new RegisterRequest
        {
            TenantName = "AlFajr Trading & Contracting",
            Email = TenantEmail,
            Password = TenantPassword,
            FullName = "محمد أحمد Franco — المدير العام",
            BaseCurrency = "LYD"
        };
        AuthResult? result = null;
        try
        {
            result = await auth.RegisterAsync(req, "127.0.0.1", ct);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
        {
            // Duplicate key (tenant subdomain or email) — fall back to login
            _logger.LogWarning("Register hit duplicate key ({Msg}), falling back to login", ex.MessageText);
            result = null;
        }

        if (result != null && result.Succeeded)
            return (result.Response!.User.TenantId, result.Response.User.Id);

        if (result != null && !result.Succeeded)
            _logger.LogWarning("Register failed ({Error}), trying login", result.Error);

        var login = await auth.LoginAsync(new LoginRequest { Email = TenantEmail, Password = TenantPassword }, "127.0.0.1", ct);
        if (!login.Succeeded)
        {
            _logger.LogError("Login also failed: {Error}", login.Error);
            return (Guid.Empty, Guid.Empty);
        }
        var users = services.GetRequiredService<IUserRepository>();
        var user = await users.GetByEmailAndTenantAsync(TenantEmail, login.Response!.User.TenantId, ct);
        return (login.Response.User.TenantId, user!.Id);
    }

    // ============================================================
    // STEP 2: Extra CoA accounts
    // ============================================================
    private async Task SeedExtraAccountsAsync(IServiceProvider services, Guid tenantId, CancellationToken ct)
    {
        await using var conn = await OpenConnectionAsync(services, ct);

        var holding = await conn.QueryFirstOrDefaultAsync(
            new CommandDefinition(
                "SELECT id FROM companies WHERE tenant_id = @T AND is_group = true LIMIT 1",
                new { T = tenantId }, cancellationToken: ct));

        if (holding == null) { _logger.LogWarning("No holding company found for tenant"); return; }

        var holdingId = (Guid)holding.id;

        // Lookup parent account IDs by code (existing CoA accounts)
        var parentCodes = new[] { "1100", "2200", "4100", "5100" };
        var parentIds = (await conn.QueryAsync<(string code, Guid id)>(new CommandDefinition(
            "SELECT code, id FROM accounts WHERE tenant_id = @T AND code = ANY(@Codes)",
            new { T = tenantId, Codes = parentCodes }, cancellationToken: ct))).ToDictionary(x => x.code, x => x.id);

        var now = DateTime.UtcNow;

        var extraAccounts = new (string Code, string Name, AccountType Type, string? ParentCode, bool IsPostable)[]
        {
            ("1500", "أصول ثابتة تحت الإنشاء", AccountType.Asset, "1100", true),
            ("1600", "عربات ومركبات", AccountType.Asset, "1100", true),
            ("1700", "ضمانات وودائع", AccountType.Asset, "1100", true),
            ("2240", "بنك المدينة", AccountType.Liability, "2200", true),
            ("2250", "صندوق التكافل الاجتماعي", AccountType.Liability, "2200", true),
            ("4120", "مصاريف الكهرباء والمياه", AccountType.Expense, "4100", true),
            ("4130", "مصاريف الاتصالات والإنترنت", AccountType.Expense, "4100", true),
            ("4140", "إيجار المبني", AccountType.Expense, "4100", true),
            ("4150", "مصاريف صيانة المركبات", AccountType.Expense, "4100", true),
            ("4160", "مصاريف التأمين", AccountType.Expense, "4100", true),
            ("4170", "مصاريف قانونية ومحاسبية", AccountType.Expense, "4100", true),
            ("4180", "إهلاك الأصول الثابتة", AccountType.Expense, "4100", true),
            ("4500", "مصاريف أخرى", AccountType.Expense, "4100", true),
            ("5130", "إيرادات خدمات استشارية", AccountType.Revenue, "5100", true),
            ("5140", "إيرادات صيانة", AccountType.Revenue, "5100", true),
            ("6100", "فروق سعر الصرف", AccountType.Expense, "4100", true),
            ("6200", "خصومات مكتسبة", AccountType.Revenue, "5100", true),
        };

        foreach (var acc in extraAccounts)
        {
            var existing = await conn.QueryFirstOrDefaultAsync(
                new CommandDefinition(
                    "SELECT id FROM accounts WHERE tenant_id = @T AND code = @C",
                    new { T = tenantId, C = acc.Code }, cancellationToken: ct));
            if (existing != null) continue;

            Guid? parentId = null;
            if (acc.ParentCode != null && parentIds.TryGetValue(acc.ParentCode, out var pid))
                parentId = pid;

            var nb = acc.Type == AccountType.Asset || acc.Type == AccountType.Expense ? NormalBalance.Debit : NormalBalance.Credit;
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO accounts (id, tenant_id, company_id, code, name, type, normal_balance,
                    parent_account_id, is_postable, is_active, is_intercompany, created_at, updated_at)
                VALUES (gen_random_uuid(), @T, @CompId, @Code, @Name, @Type, @Nb, @ParentId, @IsPostable, true, false, @Now, @Now)",
                new { T = tenantId, CompId = holdingId, Code = acc.Code, Name = acc.Name, Type = (int)acc.Type, Nb = (int)nb, ParentId = parentId, IsPostable = acc.IsPostable, Now = now },
                cancellationToken: ct));
        }
        _logger.LogInformation("  ✅ Extra CoA accounts seeded ({N})", extraAccounts.Length);
    }

    // ============================================================
    // STEP 3: Departments
    // ============================================================
    private async Task SeedDepartmentsAsync(IServiceProvider services, Guid tenantId, CancellationToken ct)
    {
        var deptSvc = services.GetRequiredService<IDepartmentService>();
        var deptData = new (string Code, string Name)[]
        {
            ("DEPT-ADMIN", "الإدارة"),
            ("DEPT-FIN", "الشؤون المالية"),
            ("DEPT-HR", "الموارد البشرية"),
            ("DEPT-PROC", "المشتريات والمخازن"),
            ("DEPT-ENG", "الهندسة والمشاريع"),
        };
        foreach (var d in deptData)
            await deptSvc.CreateAsync(tenantId, new CreateDepartmentRequest { Code = d.Code, Name = d.Name }, ct);
        _logger.LogInformation("  ✅ Departments seeded ({N})", deptData.Length);
    }

    private async Task<Dictionary<string, Guid>> GetDepartmentIdsAsync(IServiceProvider services, Guid tenantId, CancellationToken ct)
    {
        await using var conn = await OpenConnectionAsync(services, ct);
        var rows = await conn.QueryAsync(
            new CommandDefinition("SELECT code, id FROM departments WHERE tenant_id = @T",
                new { T = tenantId }, cancellationToken: ct));
        return rows.ToDictionary(r => (string)r.code, r => (Guid)r.id);
    }

    // ============================================================
    // STEP 4: Employees
    // ============================================================
    private async Task<List<Guid>> SeedEmployeesAsync(IServiceProvider services, Guid tenantId, Guid adminUserId, Dictionary<string, Guid> deptIds, CancellationToken ct)
    {
        var empSvc = services.GetRequiredService<IEmployeeService>();

        // Idempotency: if employees exist, return their IDs (for re-runs)
        var existing = await empSvc.ListAsync(tenantId, null, true, 0, 200, ct);
        if (existing.Succeeded && existing.Value!.Count >= 12)
        {
            _logger.LogInformation("  ⏭ Employees already seeded ({N})", existing.Value.Count);
            return existing.Value.Select(e => e.Id).ToList();
        }

        var hiredate2024 = new DateTime(2024, 1, 15);

        var employees = new (string FullName, string Email, string Phone, string NationalId, string DeptCode, string JobTitle, decimal Salary)[]
        {
            ("Mohamed Ahmed Franco", "mohamed@alfajr.local", "0911223344", "1001001001", "DEPT-ADMIN", "مدير عام", 8500m),
            ("Ahmed Abdullah", "ahmed.abdullah@alfajr.local", "0911334455", "1001001002", "DEPT-FIN", "محاسب أول", 3800m),
            ("Fatima Hamida", "fatima.hamida@alfajr.local", "0911445566", "1001001003", "DEPT-FIN", "محاسب", 3200m),
            ("Khaled Mohamed", "khaled.mohamed@alfajr.local", "0911556677", "1001001004", "DEPT-HR", "مسؤول موارد بشرية", 3500m),
            ("Sara Ali", "sara.ali@alfajr.local", "0911667788", "1001001005", "DEPT-ADMIN", "أمين سر", 2800m),
            ("Omar Youssef", "omar.youssef@alfajr.local", "0911778899", "1001001006", "DEPT-ENG", "مهندس موقع", 4000m),
            ("Abdulbasit Salem", "abdulbasit.salem@alfajr.local", "0911889900", "1001001007", "DEPT-ENG", "فني بناء أول", 2200m),
            ("Ali Omar", "ali.omar@alfajr.local", "0911990011", "1001001008", "DEPT-ENG", "فني بناء", 2200m),
            ("Hussein Mansour", "hussein.mansour@alfajr.local", "0921001122", "1001001009", "DEPT-PROC", "أمين مخزن أول", 2500m),
            ("Kamal Ramadan", "kamal.ramadan@alfajr.local", "0921112233", "1001001010", "DEPT-PROC", "أمين مخزن", 2200m),
            ("Naseer Ali", "naseer.ali@alfajr.local", "0921223344", "1001001011", "DEPT-PROC", "مسؤول مشتريات", 3000m),
            ("Rida Khalil", "rida.khalil@alfajr.local", "0921334455", "1001001012", "DEPT-FIN", "كاشير", 2400m),
        };

        var empIds = new List<Guid>();
        foreach (var e in employees)
        {
            var hireDate = hiredate2024.AddDays(Random.Shared.Next(0, 200));
            var r = await empSvc.CreateAsync(tenantId, adminUserId, new CreateEmployeeRequest
            {
                FullName = e.FullName, Email = e.Email, Phone = e.Phone, NationalId = e.NationalId,
                DepartmentId = deptIds.GetValueOrDefault(e.DeptCode), JobTitle = e.JobTitle,
                HireDate = hireDate, BaseSalary = e.Salary
            }, ct);
            if (r.Succeeded) empIds.Add(r.Value!.Id);
        }
        _logger.LogInformation("  ✅ Employees seeded ({N})", empIds.Count);
        return empIds;
    }

    // ============================================================
    // STEP 5: Salary Structures
    // ============================================================
    private async Task SeedSalaryStructuresAsync(IServiceProvider services, Guid tenantId, Guid adminUserId, CancellationToken ct)
    {
        await using var conn = await OpenConnectionAsync(services, ct);
        var now = DateTime.UtcNow;

        var emps = (await conn.QueryAsync<Employee>(
            new CommandDefinition("SELECT * FROM employees WHERE tenant_id = @T AND is_active = true",
                new { T = tenantId }, cancellationToken: ct))).ToList();

        foreach (var emp in emps)
        {
            var structId = Guid.NewGuid();
            var structCode = $"EMP-{structId:N}"[..20];
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO salary_structures (id, tenant_id, name, code, currency, is_active, created_at, updated_at, created_by, updated_by)
                VALUES (@Id, @T, @Name, @Code, 'LYD', true, @Now, @Now, @By, @By)",
                new { Id = structId, T = tenantId, Name = $"هيكلة {emp.FullName}", Code = structCode, By = adminUserId, Now = now },
                cancellationToken: ct));

            var lines = new (SalaryComponentType Type, string Name, decimal Amount, int Sort)[]
            {
                (SalaryComponentType.Earning, "بدل السكن", Math.Round(emp.BaseSalary * 0.20m, 4), 1),
                (SalaryComponentType.Earning, "بدل المواصلات", Math.Round(emp.BaseSalary * 0.10m, 4), 2),
                (SalaryComponentType.Earning, "بدل الغذاء", Math.Round(emp.BaseSalary * 0.15m, 4), 3),
                (SalaryComponentType.Deduction, "تأمينات اجتماعية (موظف)", Math.Round(emp.BaseSalary * 0.0375m, 4), 4),
            };

            foreach (var ln in lines)
            {
                await conn.ExecuteAsync(new CommandDefinition(@"
                    INSERT INTO salary_structure_lines (id, tenant_id, salary_structure_id, type, name, amount, sort_order)
                    VALUES (gen_random_uuid(), @T, @SID, @Type, @Name, @Amt, @Sort)",
                    new { T = tenantId, SID = structId, Type = ln.Type.ToString(), Name = ln.Name, Amt = ln.Amount, Sort = ln.Sort },
                    cancellationToken: ct));
            }
        }
        _logger.LogInformation("  ✅ Salary structures seeded ({N})", emps.Count);
    }

    // ============================================================
    // STEP 6: Attendance
    // ============================================================
    private async Task SeedAttendanceAsync(IServiceProvider services, Guid tenantId, List<Guid> empIds, CancellationToken ct)
    {
        // SPRINT-4 Day 2 (DEC-042): wrap heavy DB work in RetryPolicy for transient errors
        // (network blips, deadlocks). Non-transient errors (duplicate keys) won't retry.
        await RetryPolicy.ExecuteWithRetryAsync(
            async (attempt, retryCt) =>
            {
                await using var conn = await OpenConnectionAsync(services, retryCt);

                var existing = await conn.ExecuteScalarAsync<int>(
                    new CommandDefinition("SELECT COUNT(*) FROM attendance WHERE tenant_id = @T",
                        new { T = tenantId }, cancellationToken: retryCt));
                if (existing > 0) { _logger.LogInformation("  ⏭ Attendance already seeded ({N} records)", existing); return; }

                var totalRecords = 0;
                var workingDaysPerMonth = new[] { 22, 20, 23, 21, 22, 22, 23, 22, 22, 23, 21, 22 };

                for (var empIdx = 0; empIdx < empIds.Count; empIdx++)
                {
                    var empId = empIds[empIdx];
                    for (var mi = 0; mi < 12; mi++)
                    {
                        var month = mi + 1;
                        var workingDays = workingDaysPerMonth[mi];
                        var absentDays = Random.Shared.Next(0, 3);

                        for (var day = 1; day <= DateTime.DaysInMonth(2026, month); day++)
                        {
                            var date = new DateTime(2026, month, day);
                            if (date.DayOfWeek == DayOfWeek.Friday || date.DayOfWeek == DayOfWeek.Saturday) continue;
                            if (Random.Shared.NextDouble() < 0.015 * absentDays) continue;

                            var checkIn = date.AddHours(8).AddMinutes(Random.Shared.Next(0, 25));
                            var checkOut = date.AddHours(16).AddMinutes(Random.Shared.Next(-10, 30));
                            var now = DateTime.UtcNow;

                            await conn.ExecuteAsync(new CommandDefinition(@"
                                INSERT INTO attendance (id, tenant_id, employee_id, type, timestamp, notes, ip_address, created_at)
                                VALUES (gen_random_uuid(), @T, @EID, @TypeIn, @TS, NULL, '127.0.0.1', @Now)",
                                new { T = tenantId, EID = empId, TypeIn = AttendanceType.CheckIn.ToString(), TS = checkIn, Now = now }, cancellationToken: retryCt));

                            await conn.ExecuteAsync(new CommandDefinition(@"
                                INSERT INTO attendance (id, tenant_id, employee_id, type, timestamp, notes, ip_address, created_at)
                                VALUES (gen_random_uuid(), @T, @EID, @TypeOut, @TS, NULL, '127.0.0.1', @Now)",
                                new { T = tenantId, EID = empId, TypeOut = AttendanceType.CheckOut.ToString(), TS = checkOut, Now = now }, cancellationToken: retryCt));

                            totalRecords += 2;
                        }
                    }
                    if ((empIdx + 1) % 4 == 0)
                        _logger.LogInformation("  ... attendance: {Done}/{Total} employees", empIdx + 1, empIds.Count);
                }
                _logger.LogInformation("  ✅ Attendance seeded ({N} records for {Emps} employees)", totalRecords, empIds.Count);
            },
            _logger,
            maxRetries: RetryPolicy.DefaultMaxRetries,
            opName: "SeedAttendance",
            ct: ct);
    }

    // ============================================================
    // STEP 7: Leave Requests
    // ============================================================
    private async Task SeedLeaveRequestsAsync(IServiceProvider services, Guid tenantId, Guid adminUserId, List<Guid> empIds, CancellationToken ct)
    {
        var leaveSvc = services.GetRequiredService<ILeaveRequestService>();

        var leaves = new (int EmpIdx, LeaveType Type, int StartM, int StartD, int EndM, int EndD, string Reason)[]
        {
            (0, LeaveType.Annual, 3, 15, 3, 28, "إجازة سنوية"),
            (1, LeaveType.Annual, 7, 1, 7, 14, "إجازة صيفية"),
            (2, LeaveType.Sick, 5, 10, 5, 12, "مراجعة طبية"),
            (3, LeaveType.Annual, 9, 20, 10, 3, "إجازة سنوية ممتدة"),
            (4, LeaveType.Emergency, 2, 5, 2, 6, "ظرف طارئ"),
            (5, LeaveType.Annual, 6, 15, 6, 28, "إجازة صيفية"),
            (6, LeaveType.Sick, 11, 1, 11, 3, "مرض"),
            (7, LeaveType.Annual, 4, 1, 4, 10, "إجازة سنوية"),
            (8, LeaveType.Emergency, 8, 10, 8, 12, "ظرف عائلي"),
            (10, LeaveType.Annual, 10, 15, 10, 25, "إجازة سنوية"),
        };

        foreach (var l in leaves)
        {
            if (l.EmpIdx >= empIds.Count) continue;
            var r = await leaveSvc.CreateAsync(tenantId, adminUserId, new CreateLeaveRequestDto
            {
                EmployeeId = empIds[l.EmpIdx], LeaveType = l.Type,
                StartDate = new DateTime(2026, l.StartM, l.StartD),
                EndDate = new DateTime(2026, l.EndM, l.EndD),
                Reason = l.Reason
            }, ct);
            if (r.Succeeded) await leaveSvc.ApproveAsync(tenantId, adminUserId, r.Value!.Id, ct);
        }
        _logger.LogInformation("  ✅ Leave requests seeded ({N})", leaves.Length);
    }

    // ============================================================
    // STEP 8: Vendors
    // ============================================================
    private async Task SeedVendorsAsync(IServiceProvider services, Guid tenantId, CancellationToken ct)
    {
        var vendorSvc = services.GetRequiredService<IVendorService>();
        var vendors = new (string Code, string Name, string Email, string Phone, string Address, string TaxId)[]
        {
            ("V-001", "مكتب المدينة للبناء", "info@almadinabuild.ly", "0911001100", "طرابلس - طريق الميناء", "TAX-001"),
            ("V-002", "شركة النور للأدوات المكتبية", "sales@alnourstationery.ly", "0912112211", "طرابلس - شارع النور", "TAX-002"),
            ("V-003", "مؤسسة الوفاء للغذاء", "info@alwafaafood.ly", "0913223311", "طرابلس - المنطقة الصناعية", "TAX-003"),
            ("V-004", "شركة النظافة الخضراء", "info@greenclean.ly", "0914334411", "طرابلس - سوق الجمعة", "TAX-004"),
        };
        foreach (var v in vendors)
            await vendorSvc.CreateAsync(tenantId, Guid.Empty, new CreateVendorRequest
            {
                Code = v.Code, Name = v.Name, Email = v.Email, Phone = v.Phone,
                Address = v.Address, TaxNumber = v.TaxId, Currency = "LYD", PaymentTerms = "Net30"
            }, ct);
        _logger.LogInformation("  ✅ Vendors seeded ({N})", vendors.Length);
    }

    private async Task<Dictionary<string, Guid>> GetVendorIdsAsync(IServiceProvider services, Guid tenantId, CancellationToken ct)
    {
        await using var conn = await OpenConnectionAsync(services, ct);
        var rows = await conn.QueryAsync(
            new CommandDefinition("SELECT code, id FROM vendors WHERE tenant_id = @T",
                new { T = tenantId }, cancellationToken: ct));
        return rows.ToDictionary(r => (string)r.code, r => (Guid)r.id);
    }

    // ============================================================
    // STEP 9: Warehouses + Items
    // ============================================================
    private async Task SeedWarehousesAndItemsAsync(IServiceProvider services, Guid tenantId, Guid adminUserId, CancellationToken ct)
    {
        await using var conn = await OpenConnectionAsync(services, ct);
        var now = DateTime.UtcNow;

        var holding = await conn.QueryFirstOrDefaultAsync<dynamic>(
            new CommandDefinition("SELECT id FROM companies WHERE tenant_id = @T AND is_group = true",
                new { T = tenantId }, cancellationToken: ct));
        var companyId = holding == null ? Guid.Empty : (Guid)holding.id;

        var wh1Id = Guid.NewGuid(); var wh2Id = Guid.NewGuid();

        // Idempotency: check if warehouses exist
        var existingWh = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM warehouses WHERE tenant_id = @T",
                new { T = tenantId }, cancellationToken: ct));
        if (existingWh >= 2)
        {
            _logger.LogInformation("  ⏭ Warehouses already seeded ({N})", existingWh);
        }
        else
        {
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO warehouses (id, tenant_id, company_id, code, name, location, is_active, created_at, updated_at, created_by, updated_by)
                VALUES (@Id, @T, @CID, 'WH-001', 'مستودع المواد الرئيسية', 'طرابلس - المنطقة الصناعية', true, @Now, @Now, @By, @By)",
                new { Id = wh1Id, T = tenantId, CID = companyId, By = adminUserId, Now = now }, cancellationToken: ct));
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO warehouses (id, tenant_id, company_id, code, name, location, is_active, created_at, updated_at, created_by, updated_by)
                VALUES (@Id, @T, @CID, 'WH-002', 'مستودع القرطاسية والمكتبات', 'طرابلس - المقر الإداري', true, @Now, @Now, @By, @By)",
                new { Id = wh2Id, T = tenantId, CID = companyId, By = adminUserId, Now = now }, cancellationToken: ct));
        }

        var uoms = (await conn.QueryAsync<(string Code, Guid Id)>(
            new CommandDefinition("SELECT code, id FROM units_of_measure WHERE tenant_id = @T",
                new { T = tenantId }, cancellationToken: ct))).ToDictionary(r => r.Code, r => r.Id);
        var cats = (await conn.QueryAsync<(string Code, Guid Id)>(
            new CommandDefinition("SELECT code, id FROM item_categories WHERE tenant_id = @T",
                new { T = tenantId }, cancellationToken: ct))).ToDictionary(r => r.Code, r => r.Id);

        // Idempotency: skip items if already seeded
        var existingItems = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COUNT(*) FROM items WHERE tenant_id = @T",
                new { T = tenantId }, cancellationToken: ct));
        if (existingItems >= 15)
        {
            _logger.LogInformation("  ⏭ Items already seeded ({N})", existingItems);
            return;
        }

        var items = new (string Sku, string Name, ItemType Type, string UoM, string Cat, decimal Cost, decimal Reorder)[]
        {
            ("MAT-001", "إسمنت بورتلاندي", ItemType.RawMaterial, "kg", "RM", 28m, 500m),
            ("MAT-002", "حديد تشكيلي 10mm", ItemType.RawMaterial, "kg", "RM", 85m, 200m),
            ("MAT-003", "رمل نظيف", ItemType.RawMaterial, "m3", "RM", 15m, 100m),
            ("MAT-004", "بلوك أسمنتي 20cm", ItemType.RawMaterial, "pcs", "RM", 1.5m, 5000m),
            ("MAT-005", "طوب أحمر", ItemType.RawMaterial, "pcs", "RM", 0.8m, 10000m),
            ("OFF-001", "ورق A4", ItemType.Consumable, "pcs", "OFF", 45m, 50m),
            ("OFF-002", "حبر طابعة HP 304", ItemType.Consumable, "pcs", "OFF", 85m, 20m),
            ("OFF-003", "أدوات كتابة متنوعة", ItemType.Consumable, "pcs", "OFF", 25m, 30m),
            ("FOO-001", "مواد غذائية خام", ItemType.RawMaterial, "l", "CON", 150m, 50m),
            ("FOO-002", "مشروبات باردة", ItemType.Consumable, "pcs", "CON", 35m, 200m),
            ("SVC-001", "خدمة نقل", ItemType.Service, "pcs", "SVC", 500m, 0m),
            ("SVC-002", "خدمة تنظيف", ItemType.Service, "pcs", "SVC", 200m, 0m),
            ("CLE-001", "مواد تنظيف", ItemType.Consumable, "l", "CON", 60m, 30m),
            ("EQP-001", "معدات حماية شخصية", ItemType.Consumable, "pcs", "OFF", 120m, 50m),
            ("EQP-002", "قطع غيار مركبات", ItemType.Consumable, "pcs", "OFF", 350m, 20m),
        };

        foreach (var it in items)
        {
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO items (id, tenant_id, company_id, sku, name, item_type, costing_method,
                    average_cost, standard_cost, category_id, unit_of_measure_id,
                    reorder_level, reorder_quantity, is_active, created_at, updated_at, created_by, updated_by)
                VALUES (gen_random_uuid(), @T, @CID, @Sku, @Name, @Type, 3, @Cost, @Cost, @CatId, @UoMId,
                    @Reorder, 0, true, @Now, @Now, @By, @By)",
                new { T = tenantId, CID = companyId, Sku = it.Sku, Name = it.Name, Type = (int)it.Type,
                    Cost = it.Cost, CatId = cats.GetValueOrDefault(it.Cat), UoMId = uoms.GetValueOrDefault(it.UoM),
                    Reorder = it.Reorder, By = adminUserId, Now = now },
                cancellationToken: ct));
        }
        _logger.LogInformation("  ✅ Warehouses + Items seeded ({Items} items, 2 warehouses)", items.Length);
    }

    private async Task<(List<Guid> itemIds, List<Guid> warehouseIds)> GetItemAndWarehouseIdsAsync(IServiceProvider services, Guid tenantId, CancellationToken ct)
    {
        await using var conn = await OpenConnectionAsync(services, ct);
        var items = (await conn.QueryAsync<Guid>(
            new CommandDefinition("SELECT id FROM items WHERE tenant_id = @T",
                new { T = tenantId }, cancellationToken: ct))).ToList();
        var warehouses = (await conn.QueryAsync<Guid>(
            new CommandDefinition("SELECT id FROM warehouses WHERE tenant_id = @T",
                new { T = tenantId }, cancellationToken: ct))).ToList();
        return (items, warehouses);
    }

    // ============================================================
    // STEP 10: Purchase Orders
    // ============================================================
    private async Task SeedPurchaseOrdersAsync(IServiceProvider services, Guid tenantId, Guid adminUserId, Dictionary<string, Guid> vendorIds, List<Guid> itemIds, CancellationToken ct)
    {
        var poSvc = services.GetRequiredService<IPurchaseOrderService>();
        var vendorList = vendorIds.Values.ToList();

        var poData = new (int Month, int Day, int VendorIdx, int[] ItemIndices, string Note)[]
        {
            (1, 5, 0, new[] { 0 }, "طلب شراء مواد بناء - يناير"),
            (1, 12, 0, new[] { 0, 1 }, "طلب شراء إضافي"),
            (1, 20, 1, new[] { 5, 6 }, "مستلزمات مكتبية"),
            (2, 3, 0, new[] { 1, 2 }, "حديد + إسمنت"),
            (2, 15, 2, new[] { 8, 9 }, "مواد غذائية"),
            (3, 8, 0, new[] { 3, 4 }, "بلوك + طوب"),
            (3, 18, 1, new[] { 5, 6, 7 }, "قرطاسية شهرية"),
            (3, 25, 3, new[] { 12 }, "خدمات نظافة"),
            (4, 6, 0, new[] { 0 }, "مواد بناء"),
            (4, 22, 2, new[] { 8, 9 }, "إعاشة"),
            (5, 4, 1, new[] { 5, 6 }, "أدوات مكتبية"),
            (5, 15, 0, new[] { 0, 1 }, "مواد بناء"),
            (5, 28, 3, new[] { 11 }, "خدمات"),
            (6, 7, 0, new[] { 1 }, "حديد"),
            (6, 19, 2, new[] { 8, 9 }, "مستلزمات"),
            (7, 3, 1, new[] { 5, 6 }, "قرطاسية"),
            (7, 14, 0, new[] { 0, 1 }, "مواد"),
            (7, 28, 3, new[] { 12 }, "نظافة"),
            (8, 5, 2, new[] { 8, 9 }, "إعاشة"),
            (8, 20, 0, new[] { 0, 1 }, "حديد + إسمنت"),
            (9, 4, 1, new[] { 5, 6 }, "مكتبية"),
            (9, 16, 3, new[] { 12 }, "خدمات"),
            (9, 28, 0, new[] { 0, 1 }, "مواد بناء"),
            (10, 8, 2, new[] { 5, 6 }, "قرطاسية"),
            (10, 22, 0, new[] { 0 }, "مواد"),
            (11, 5, 1, new[] { 5, 6 }, "مستلزمات"),
            (11, 18, 3, new[] { 12 }, "خدمات نظافة"),
            (12, 3, 0, new[] { 0, 1 }, "طلب نهاية العام"),
            (12, 15, 2, new[] { 5, 6, 7 }, "مستلزمات شتوية"),
        };

        var created = 0;
        foreach (var po in poData)
        {
            if (po.VendorIdx >= vendorList.Count) continue;
            var orderDate = new DateTime(2026, po.Month, po.Day);

            var lines = po.ItemIndices
                .Where(i => i < itemIds.Count)
                .Select(i => new CreatePurchaseOrderLineRequest
                {
                    ItemId = itemIds[i],
                    Quantity = Random.Shared.Next(5, 200),
                    UnitPrice = Random.Shared.NextDouble() > 0.5 ? 85m : 28m,
                    TaxRate = 0m
                }).ToList();

            if (lines.Count == 0) continue;

            var r = await poSvc.CreateAsync(tenantId, adminUserId, new CreatePurchaseOrderRequest
            {
                VendorId = vendorList[po.VendorIdx],
                OrderDate = orderDate,
                ExpectedDate = orderDate.AddDays(Random.Shared.Next(7, 21)),
                Currency = "LYD",
                Notes = po.Note,
                Lines = lines
            }, ct);

            if (r.Succeeded)
            {
                await poSvc.ApproveAsync(tenantId, adminUserId, r.Value!.Id, ct);
                await poSvc.SendAsync(tenantId, adminUserId, r.Value!.Id, ct);
                created++;
            }
        }
        _logger.LogInformation("  ✅ Purchase Orders seeded ({N} POs)", created);
    }

    // ============================================================
    // STEP 11: Goods Receipts + Vendor Bills
    // ============================================================
    private async Task SeedProcurementFlowAsync(IServiceProvider services, Guid tenantId, Guid adminUserId, CancellationToken ct)
    {
        var grSvc = services.GetRequiredService<IGoodsReceiptService>();
        var billSvc = services.GetRequiredService<IVendorBillService>();

        await using var conn = await OpenConnectionAsync(services, ct);

        // DEC-073: Select order_date too so GR dates can be spread realistically
        var pos = (await conn.QueryAsync<dynamic>(
            new CommandDefinition(
                "SELECT id, vendor_id, order_date FROM purchase_orders WHERE tenant_id = @T AND status = 'Sent' ORDER BY created_at LIMIT 20",
                new { T = tenantId }, cancellationToken: ct))).ToList();

        var wh = await conn.QueryFirstOrDefaultAsync<Guid>(
            new CommandDefinition("SELECT id FROM warehouses WHERE tenant_id = @T LIMIT 1",
                new { T = tenantId }, cancellationToken: ct));

        var grCount = 0; var billCount = 0;
        foreach (var po in pos)
        {
            var poId = (Guid)po.id;
            // DEC-073: Use PO order_date as anchor, spread GR date 7-60 days after PO
            var poDate = po.order_date == null
                ? DateTime.UtcNow.AddDays(-Random.Shared.Next(60, 365))
                : (DateTime)po.order_date;
            var grDate = poDate.AddDays(Random.Shared.Next(7, 60));
            // Ensure GR date is in the past (not future)
            if (grDate > DateTime.UtcNow) grDate = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 7));

            var poLines = (await conn.QueryAsync<dynamic>(
                new CommandDefinition("SELECT item_id, quantity, unit_price FROM purchase_order_lines WHERE purchase_order_id = @POID",
                    new { POID = poId }, cancellationToken: ct))).ToList();

            var grLines = poLines.Select(l => new CreateGoodsReceiptLineRequest
            {
                ItemId = (Guid)l.item_id,
                Quantity = Random.Shared.NextDouble() > 0.15 ? (decimal)l.quantity : (decimal)l.quantity * 0.8m,
                UnitCost = (decimal)l.unit_price
            }).ToList();

            var gr = await grSvc.CreateAsync(tenantId, adminUserId, new CreateGoodsReceiptRequest
            {
                PurchaseOrderId = poId,
                ReceivedDate = grDate,  // DEC-073: spread over past, anchored to PO date
                WarehouseId = wh,
                Lines = grLines
            }, ct);

            if (!gr.Succeeded) continue;
            try
            {
                await grSvc.ReceiveAsync(tenantId, adminUserId, gr.Value!.Id, ct);
                grCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("  ⚠️  GR Receive failed for PO {PO}: {Msg}", poId, ex.Message);
                continue;
            }

            if (Random.Shared.NextDouble() > 0.10) // 90% create bill
            {
                var billLines = grLines.Select(l => new CreateVendorBillLineRequest
                {
                    ItemId = l.ItemId, Quantity = l.Quantity, UnitPrice = l.UnitCost, TaxRate = 0m
                }).ToList();

                // DEC-073: Bill date 1-30 days after GR (realistic vendor billing cycle)
                var billDate = grDate.AddDays(Random.Shared.Next(1, 30));
                if (billDate > DateTime.UtcNow) billDate = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 7));
                var bill = await billSvc.CreateAsync(tenantId, adminUserId, new CreateVendorBillRequest
                {
                    GoodsReceiptId = gr.Value!.Id,
                    BillDate = billDate,
                    DueDate = billDate.AddDays(30),
                    Currency = "LYD",
                    Lines = billLines
                }, ct);

                if (bill.Succeeded && Random.Shared.NextDouble() > 0.20) // 80% post
                {
                    try { await billSvc.PostAsync(tenantId, adminUserId, bill.Value!.Id, ct); billCount++; }
                    catch (Exception ex) { _logger.LogWarning("  ⚠️  Bill Post failed: {Msg}", ex.Message); }
                }
            }
        }
        _logger.LogInformation("  ✅ Procurement: {GR} GRs, {Bills} Bills posted", grCount, billCount);
    }

    // ============================================================
    // STEP 12: Payroll (12 months Jan-Dec 2026)
    // ============================================================
    private async Task SeedPayrollRunsAsync(IServiceProvider services, Guid tenantId, Guid adminUserId, CancellationToken ct)
    {
        var payrollSvc = services.GetRequiredService<IPayrollService>();

        // Idempotency: لو في 12 دورة رواتب موجودة بالفعل، نتخطى الخطوة (آمن لإعادة التشغيل)
        var existingRuns = await payrollSvc.ListRunsAsync(tenantId, null, 0, 50, ct);
        if (existingRuns.Succeeded && existingRuns.Value!.Count >= 12)
        {
            _logger.LogInformation("  ⏭ Payroll runs already seeded ({N} runs)", existingRuns.Value.Count);
            return;
        }

        var months = new (int Month, string Name)[]
        {
            (1, "يناير"), (2, "فبراير"), (3, "مارس"), (4, "أبريل"),
            (5, "مايو"), (6, "يونيو"), (7, "يوليو"), (8, "أغسطس"),
            (9, "سبتمبر"), (10, "أكتوبر"), (11, "نوفمبر"), (12, "ديسمبر")
        };

        var processed = 0;
        foreach (var (month, name) in months)
        {
            var start = new DateTime(2026, month, 1);
            var end = start.AddMonths(1).AddDays(-1);
            var note = month == 12
                ? $"رواتب شهر {name} 2026 (مع مكافأة نهاية العام)"
                : $"رواتب شهر {name} 2026";

            // Check if payroll run for this period already exists (extra safety)
            await using var conn = await OpenConnectionAsync(services, ct);
            var existingForMonth = await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    "SELECT COUNT(*) FROM payroll_runs WHERE tenant_id = @T AND period_start::date = @PS::date",
                    new { T = tenantId, PS = start }, cancellationToken: ct));
            if (existingForMonth > 0)
            {
                _logger.LogInformation("  ⏭ Payroll for {M}/{Y} already exists, skipping", month, 2026);
                continue;
            }

            var createR = await payrollSvc.CreateRunAsync(tenantId, adminUserId, new CreatePayrollRunRequest
            {
                PeriodStart = start, PeriodEnd = end, Notes = note
            }, ct);

            if (!createR.Succeeded) { _logger.LogWarning("Payroll create failed {M}: {Err}", month, createR.Error); continue; }
            if (!payrollSvc.ProcessRunAsync(tenantId, adminUserId, createR.Value!.Id, ct).Result.Succeeded)
                { _logger.LogWarning("Payroll process failed {M}", month); continue; }
            if (!payrollSvc.PostRunAsync(tenantId, adminUserId, createR.Value!.Id, ct).Result.Succeeded)
                { _logger.LogWarning("Payroll post failed {M}", month); continue; }

            processed++;
            if (month % 3 == 0) _logger.LogInformation("  ... payroll: {M}/12 ({Name})", month, name);
        }
        _logger.LogInformation("  ✅ Payroll runs seeded ({N} months — all Posted)", processed);
    }

    // ============================================================
    // STEP 12.5: Opening Balance Journal Entry (DEC-075)
    // Dr Cash 5M / Cr Owner's Capital 5M
    // Provides proper starting position for Trial Balance
    // ============================================================
    private async Task SeedOpeningBalanceAsync(IServiceProvider services, Guid tenantId, Guid adminUserId, CancellationToken ct)
    {
        var journalSvc = services.GetRequiredService<ERPSystem.Modules.Finance.Application.Services.IJournalEntryService>();

        await using var conn = await OpenConnectionAsync(services, ct);

        // Idempotency: skip if opening balance already exists
        var existingOpening = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM journal_entries WHERE tenant_id = @T AND reference = 'OPENING-BALANCE'",
            new { T = tenantId }, cancellationToken: ct));
        if (existingOpening > 0)
        {
            _logger.LogInformation("  ⏭ Opening balance already exists, skipping");
            return;
        }

        // Look up Cash (1210) and Capital (3100) account IDs
        var accts = await conn.QueryAsync<(string Code, Guid Id)>(new CommandDefinition(
            "SELECT code, id FROM accounts WHERE tenant_id = @T AND code IN ('1210', '3100')",
            new { T = tenantId }, cancellationToken: ct));
        Guid? cashId = null, capitalId = null;
        foreach (var (code, id) in accts)
        {
            if (code == "1210") cashId = id;
            if (code == "3100") capitalId = id;
        }

        if (cashId == null || capitalId == null)
        {
            _logger.LogWarning("  ⚠️ Opening balance skipped — accounts missing (1210={Cash}, 3100={Cap})",
                cashId, capitalId);
            return;
        }

        const decimal openingAmount = 5_000_000m;  // 5M LYD starting capital
        var draft = await journalSvc.CreateDraftAsync(tenantId, adminUserId, new ERPSystem.Modules.Finance.Application.PostJournalEntryRequest
        {
            EntryDate = new DateTime(2024, 1, 1),  // opening of business
            Description = "Opening Balance — Owner's Capital Investment",
            Reference = "OPENING-BALANCE",
            Lines = new List<ERPSystem.Modules.Finance.Application.PostJournalLineRequest>
            {
                new() { AccountId = cashId.Value, Debit = openingAmount, Credit = 0m,
                        Description = "Cash on hand (opening)" },
                new() { AccountId = capitalId.Value, Debit = 0m, Credit = openingAmount,
                        Description = "Owner's capital contribution" }
            }
        }, ct);

        if (!draft.Succeeded)
        {
            _logger.LogWarning("  ⚠️ Opening balance draft failed: {Err}", draft.Error);
            return;
        }

        var post = await journalSvc.PostAsync(tenantId, adminUserId, draft.Value!.Id, ct);
        if (post.Succeeded)
            _logger.LogInformation("  ✅ Opening balance JE created ({N} LYD)", openingAmount);
        else
            _logger.LogWarning("  ⚠️ Opening balance post failed: {Err}", post.Error);
    }

    // ============================================================
    // STEP 13: Manual Journal Entries
    // ============================================================
    private async Task SeedManualJournalEntriesAsync(IServiceProvider services, Guid tenantId, Guid adminUserId, CancellationToken ct)
    {
        var journalSvc = services.GetRequiredService<IJournalEntryService>();

        await using var conn = await OpenConnectionAsync(services, ct);
        var accountMap = (await conn.QueryAsync<(string Code, Guid Id)>(
            new CommandDefinition("SELECT code, id FROM accounts WHERE tenant_id = @T",
                new { T = tenantId }, cancellationToken: ct))).ToDictionary(r => r.Code, r => r.Id);

        var entries = new (int Month, int Day, string Desc, string Ref, (string Code, decimal Dr, decimal Cr)[] Lines)[]
        {
            (1, 5, "إيجار المبني - يناير 2026", "RENT-2026-01",
                new[] { ("4140", 5000m, 0m), ("1210", 0m, 5000m) }),
            (1, 8, "الكهرباء والمياه - يناير", "UTIL-2026-01",
                new[] { ("4120", 1200m, 0m), ("1210", 0m, 1200m) }),
            (1, 10, "فاتورة هاتف شهر يناير", "TEL-2026-01",
                new[] { ("4130", 450m, 0m), ("1210", 0m, 450m) }),
            (1, 15, "صيانة مركبات - ورشة النور", "VEH-2026-01",
                new[] { ("4150", 2800m, 0m), ("1210", 0m, 2800m) }),
            (2, 3, "إيجار المبني - فبراير", "RENT-2026-02",
                new[] { ("4140", 5000m, 0m), ("1210", 0m, 5000m) }),
            (2, 10, "مصاريف قانونية - محامي", "LEGAL-2026-02",
                new[] { ("4170", 3500m, 0m), ("1210", 0m, 3500m) }),
            (2, 20, "شراء مستلزمات مكتبية", "OFF-2026-02",
                new[] { ("OFF-001", 4500m, 0m), ("1210", 0m, 4500m) }),
            (3, 1, "تجديد تأمين سنوي", "INS-2026",
                new[] { ("4160", 12000m, 0m), ("1210", 0m, 12000m) }),
            (3, 5, "إيجار المبني - مارس", "RENT-2026-03",
                new[] { ("4140", 5000m, 0m), ("1210", 0m, 5000m) }),
            (4, 5, "إيجار المبني - أبريل", "RENT-2026-04",
                new[] { ("4140", 5000m, 0m), ("1210", 0m, 5000m) }),
            (4, 15, "صيانة مكيفات", "MAINT-2026-04",
                new[] { ("4150", 1800m, 0m), ("1210", 0m, 1800m) }),
            (5, 5, "إيجار المبني - مايو", "RENT-2026-05",
                new[] { ("4140", 5000m, 0m), ("1210", 0m, 5000m) }),
            (6, 3, "إيجار المبني - يونيو", "RENT-2026-06",
                new[] { ("4140", 5000m, 0m), ("1210", 0m, 5000m) }),
            (6, 10, "تجديد رخصة Municipality", "LIC-2026",
                new[] { ("4170", 3000m, 0m), ("1210", 0m, 3000m) }),
            (7, 5, "إيجار المبني - يوليو", "RENT-2026-07",
                new[] { ("4140", 5000m, 0m), ("1210", 0m, 5000m) }),
            (8, 5, "إيجار المبني - أغسطس", "RENT-2026-08",
                new[] { ("4140", 5000m, 0m), ("1210", 0m, 5000m) }),
            (8, 15, "صيانة مكيفات صيفية", "COOL-2026-08",
                new[] { ("4150", 2200m, 0m), ("1210", 0m, 2200m) }),
            (9, 5, "إيجار المبني - سبتمبر", "RENT-2026-09",
                new[] { ("4140", 5000m, 0m), ("1210", 0m, 5000m) }),
            (10, 5, "إيجار المبني - أكتوبر", "RENT-2026-10",
                new[] { ("4140", 5000m, 0m), ("1210", 0m, 5000m) }),
            (10, 20, "مستلزمات مكتبية - ربع سنوي", "OFF-2026-Q4",
                new[] { ("OFF-001", 4500m, 0m), ("1210", 0m, 4500m) }),
            (11, 5, "إيجار المبني - نوفمبر", "RENT-2026-11",
                new[] { ("4140", 5000m, 0m), ("1210", 0m, 5000m) }),
            (12, 3, "إيجار المبني - ديسمبر", "RENT-2026-12",
                new[] { ("4140", 5000m, 0m), ("1210", 0m, 5000m) }),
            (12, 15, "مكافآت نهاية السنة", "BONUS-2026",
                new[] { ("4200", 25000m, 0m), ("1210", 0m, 25000m) }),
            (12, 28, "إهلاك الأصول الثابتة - سنوي", "DEP-2026",
                new[] { ("4180", 8000m, 0m), ("1600", 0m, 8000m) }),
        };

        var created = 0;
        foreach (var e in entries)
        {
            var journalLines = e.Lines
                .Where(l => accountMap.ContainsKey(l.Code))
                .Select(l => new PostJournalLineRequest
                {
                    AccountId = accountMap[l.Code], Debit = l.Dr, Credit = l.Cr
                }).ToList();

            if (journalLines.Count < 2) continue;

            // DEC-073: Spread JEs over past 24 months (2024-2025)
            // Jan-Jun → 2025 (Jul-Dec 2024 = 6-12 months ago, Jan-Jun 2025 = 13-18 months ago)
            // Jul-Dec → 2024 (Jul-Dec 2024 = 6-12 months ago)
            var year = e.Month >= 7 ? 2024 : 2025;
            var yearSuffix = year % 100;

            var r = await journalSvc.CreateDraftAsync(tenantId, adminUserId, new PostJournalEntryRequest
            {
                EntryDate = new DateTime(year, e.Month, e.Day),
                Description = e.Desc,
                Reference = e.Ref.Replace("-2026-", $"-20{yearSuffix}-"),  // update reference to match year
                Lines = journalLines
            }, ct);

            if (r.Succeeded) { await journalSvc.PostAsync(tenantId, adminUserId, r.Value!.Id, ct); created++; }
            else _logger.LogWarning("JE failed: {Desc} — {Err}", e.Desc, r.Error);
        }
        _logger.LogInformation("  ✅ Manual Journal Entries seeded ({N})", created);
    }

    // ============================================================
    // STEP 14: Projects
    // ============================================================
    private async Task SeedProjectsAsync(IServiceProvider services, Guid tenantId, Guid adminUserId, CancellationToken ct)
    {
        var projectSvc = services.GetRequiredService<IProjectService>();

        await using var conn = await OpenConnectionAsync(services, ct);
        var company = await conn.QueryFirstOrDefaultAsync<dynamic>(
            new CommandDefinition("SELECT id FROM companies WHERE tenant_id = @T LIMIT 1",
                new { T = tenantId }, cancellationToken: ct));
        var companyId = company == null ? (Guid?)null : (Guid)company.id;

        var projects = new (string Code, string Name, decimal Budget, DateTime Start, DateTime End, string Desc)[]
        {
            ("P-2026-001", "مشروع بناء فيلات السُّرَّي", 450000m, new DateTime(2026, 1, 10), new DateTime(2026, 12, 31),
                "مشروع بناء 4 فيلات متكاملة مع التشطيبات"),
            ("P-2026-002", "تجديد مبنى municipal", 180000m, new DateTime(2026, 3, 1), new DateTime(2026, 10, 31),
                "تجديد شامل تشمل الهيكل والتشطيبات"),
            ("P-2026-003", "مشروع صيانة طرق", 95000m, new DateTime(2026, 6, 1), new DateTime(2026, 12, 31),
                "صيانة وإعادة رصف طرق في 3 أحياء"),
        };

        if (!companyId.HasValue)
        {
            _logger.LogWarning("  ⚠️  لم يُنشأ قسم Projects — لا توجد شركة افتراضية للـ tenant");
            return;
        }

        foreach (var p in projects)
        {
            var r = await projectSvc.CreateAsync(tenantId, adminUserId, new CreateProjectRequest
            {
                Code = p.Code, Name = p.Name, Budget = p.Budget,
                Description = p.Desc, StartDate = p.Start, EndDate = p.End, CompanyId = companyId!.Value
            }, ct);
            // Activate first two projects
            if (r.Succeeded && (p.Code == "P-2026-001" || p.Code == "P-2026-002"))
                await projectSvc.ChangeStatusAsync(tenantId, adminUserId, r.Value!.Id, ProjectStatus.Active, ct);
        }
        _logger.LogInformation("  ✅ Projects seeded ({N})", projects.Length);
    }
}
