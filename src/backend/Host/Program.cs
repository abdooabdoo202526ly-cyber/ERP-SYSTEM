// ERP-SYSTEM Backend Entry Point
// Phase 0: Foundation + Identity Module
// Phase 1: Finance Core
// Phase 1.5: Multi-Company Foundation
// Phase 2.1: Projects Module

using System.Text;
using System.Text.Json.Serialization;
using Dapper;
using ERPSystem.Host.Middleware;
using ERPSystem.Shared.Audit;
using ERPSystem.Shared.DataTypes;
using ERPSystem.Modules.Companies.Application.Services;
using ERPSystem.Modules.Companies.Infrastructure;
using ERPSystem.Modules.Finance.Application.Services;
using ERPSystem.Modules.Identity.Application.Auth;
using ERPSystem.Modules.Identity.Infrastructure;
using ERPSystem.Modules.Projects.Application;
using ERPSystem.Modules.Projects.Application.Services;
using ERPSystem.Modules.Projects.Infrastructure;
using ERPSystem.Modules.Inventory.Application;
using ERPSystem.Modules.Inventory.Application.Services;
using ERPSystem.Modules.Inventory.Infrastructure;
using ERPSystem.Modules.Procurement.Application;
using ERPSystem.Modules.Procurement.Application.Services;
using ERPSystem.Modules.Procurement.Infrastructure;
using ERPSystem.Modules.HR.Application;
using ERPSystem.Modules.HR.Application.Services;
using ERPSystem.Modules.HR.Infrastructure;
using ERPSystem.Modules.Payroll.Application;
using ERPSystem.Modules.Payroll.Application.Services;
using ERPSystem.Modules.Payroll.Domain.Calculators;
using ERPSystem.Modules.Payroll.Infrastructure;
using ERPSystem.Modules.AccountsReceivable.Application;
using ERPSystem.Modules.AccountsReceivable.Application.Services;
using ERPSystem.Modules.AccountsReceivable.Infrastructure;
using ERPSystem.Modules.Reports.Application.Services;
using ERPSystem.Modules.Notifications.Application.Services;
using ERPSystem.Modules.Notifications.Infrastructure;
using ERPSystem.Modules.Finance.Application.EventHandlers;
using ERPSystem.Modules.Finance.Infrastructure;
using ERPSystem.Shared.Events;
using ERPSystem.Shared.Events.Application.Services;
using ERPSystem.Shared.Events.Infrastructure;
using ERPSystem.Shared.Infrastructure;
using ERPSystem.Shared.Migrations;
using ERPSystem.Shared.SeedData;
using ERPSystem.Shared.MultiTenancy;
using FluentMigrator.Runner;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ============ Error tracking (Sentry — optional) ============
// Sprint-4 Day 3 (DEC-045). Disabled unless Sentry__Dsn env var is set.
var sentryDsn = builder.Configuration["Sentry:Dsn"] ?? builder.Configuration["Sentry__Dsn"];
if (!string.IsNullOrWhiteSpace(sentryDsn))
{
    builder.WebHost.UseSentry(o =>
    {
        o.Dsn = sentryDsn;
        o.Environment = builder.Environment.EnvironmentName;
        o.Release = "erp-system@1.0.0";
        o.TracesSampleRate = 0.2; // 20% of transactions (HF free tier)
        o.SendDefaultPii = false; // GDPR-safe default
        o.AttachStacktrace = true;
    });
    Console.WriteLine($"[SENTRY] Error tracking enabled (env={builder.Environment.EnvironmentName})");
}
else
{
    Console.WriteLine("[SENTRY] Error tracking disabled (no Sentry__Dsn env var)");
}

// ============ Logging ============
// Sprint-4 Day 3 (DEC-045): structured JSON logging.
// In Development: human-readable output. In Production: JSON for log aggregation.
builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
      .Enrich.FromLogContext()
      .Enrich.WithMachineName()
      .Enrich.WithThreadId()
      .Enrich.WithProperty("Application", "ERP-SYSTEM")
      .Enrich.WithProperty("Environment", ctx.HostingEnvironment.EnvironmentName);

    if (ctx.HostingEnvironment.IsDevelopment())
    {
        lc.WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj} {Properties:j}{NewLine}{Exception}");
    }
    else
    {
        // Production: Compact JSON for log shippers (Loki, Elasticsearch, CloudWatch, etc.)
        lc.WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter());
    }

    // DEC-111: Sentry DSN logged (sink not added due to package API mismatch).
    // Future: Sentry.AspNetCore SDK integration via UseSentry() in pipeline.
    var sentryDsn = ctx.Configuration["Sentry:Dsn"];
    if (!string.IsNullOrWhiteSpace(sentryDsn))
    {
        lc.Enrich.WithProperty("SentryDsn", sentryDsn);
    }
});

// ============ Configuration ============
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()
    ?? throw new InvalidOperationException("JwtSettings غير معرّف.");
builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(jwtSettings));

builder.Services.Configure<NpgsqlConnectionOptions>(opts =>
{
    opts.OltpConnectionString = builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("ConnectionStrings:Postgres غير معرّف.");
    opts.EventStoreConnectionString = builder.Configuration.GetSection("Marten")["ConnectionString"];
});

// ============================================
// TODO: Enable Marten in Sprint-5 (DEC-017)
// ============================================
// Marten package is installed and configured but NOT yet wired up.
// Event store using PostgreSQL LISTEN/NOTIFY planned for Sprint-5+.
//
// When enabling:
//   1. Uncomment below
//   2. Add IDocumentSession to OutboxEventPublisher
//   3. Create projections for materialized views
//
// Why deferred: Event sourcing adds complexity. We need feature flag
// infrastructure first (Sprint-4) before activating Marten.
//
// Reference: DEC-017 (2026-07-05)
// ============================================
// builder.Services.AddMarten(opts =>
//     opts.Connection(builder.Configuration["Marten:ConnectionString"]!));

// ============ Infrastructure ============
builder.Services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
// Dapper TypeHandlers: تخزين الـ enums كـ string في DB + قراءة صحيحة
SqlMapper.AddTypeHandler(new EnumStringTypeHandler<ERPSystem.Modules.HR.Entities.LeaveStatus>());
SqlMapper.AddTypeHandler(new EnumStringTypeHandler<ERPSystem.Modules.Procurement.Entities.PurchaseOrderStatus>());
SqlMapper.AddTypeHandler(new EnumStringTypeHandler<ERPSystem.Modules.Procurement.Entities.GoodsReceiptStatus>());
SqlMapper.AddTypeHandler(new EnumStringTypeHandler<ERPSystem.Modules.Procurement.Entities.VendorBillStatus>());
SqlMapper.AddTypeHandler(new EnumStringTypeHandler<ERPSystem.Modules.Payroll.Domain.Entities.PayrollRunStatus>());
SqlMapper.AddTypeHandler(new EnumStringTypeHandler<ERPSystem.Modules.Payroll.Domain.Entities.PayrollItemStatus>());
// DEC-107 / DL 82: Response caching
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ERPSystem.Host.Utilities.ITenantCache, ERPSystem.Host.Utilities.TenantCache>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<ERPSystem.Modules.Identity.Application.Services.IRoleManagementService, ERPSystem.Modules.Identity.Application.Services.RoleManagementService>();
builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();
builder.Services.AddScoped<ICostCenterRepository, CostCenterRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IResourceRepository, ResourceRepository>();
builder.Services.AddScoped<IProjectBudgetRepository, ProjectBudgetRepository>();
builder.Services.AddScoped<IResourceAssignmentRepository, ResourceAssignmentRepository>();
builder.Services.AddScoped<IItemRepository, ItemRepository>();
builder.Services.AddScoped<IWarehouseRepository, WarehouseRepository>();
builder.Services.AddScoped<IUnitOfMeasureRepository, UnitOfMeasureRepository>();
builder.Services.AddScoped<IItemCategoryRepository, ItemCategoryRepository>();
builder.Services.AddScoped<IStockMovementRepository, StockMovementRepository>();
builder.Services.AddScoped<IStockLevelRepository, StockLevelRepository>();
builder.Services.AddScoped<IStockReservationRepository, StockReservationRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IVendorRepository, VendorRepository>();
builder.Services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
builder.Services.AddScoped<IGoodsReceiptRepository, GoodsReceiptRepository>();
builder.Services.AddScoped<IVendorBillRepository, VendorBillRepository>();
builder.Services.AddScoped<IDocumentSequenceRepository, DocumentSequenceRepository>();
builder.Services.AddScoped<IDepartmentRepository, DepartmentRepository>();
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
builder.Services.AddScoped<ILeaveRequestRepository, LeaveRequestRepository>();
builder.Services.AddScoped<IHRDocumentSequenceRepository, HRDocumentSequenceRepository>();
builder.Services.AddScoped<IPayrollRepository, PayrollRepository>();
builder.Services.AddScoped<ISalaryStructureRepository, SalaryStructureRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ERPSystem.Shared.SeedData.IDefaultAccountsHelper, ERPSystem.Shared.SeedData.DefaultAccountsHelper>();
builder.Services.AddScoped<IJournalEntryRepository, JournalEntryRepository>();
builder.Services.AddScoped<IPostingRuleRepository, PostingRuleRepository>();
// AR module (Phase 5 Sprint 1 — Finance AR)
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ISalesInvoiceRepository, SalesInvoiceRepository>();
builder.Services.AddScoped<IReceiptRepository, ReceiptRepository>();
builder.Services.AddScoped<IArDocumentSequenceRepository, ArDocumentSequenceRepository>();
builder.Services.AddScoped<IProcessedEventsRepository, ProcessedEventsRepository>();
builder.Services.AddScoped<IProcessedEventsRepository, ProcessedEventsRepository>();

// ============ Multi-tenancy ============
builder.Services.AddScoped<ITenantContext, TenantContext>();

// ============ Audit (Sprint-4.5 / DEC-056) ============
builder.Services.AddScoped<IAuditLogger, AuditLogger>();

// ============ Domain Events (Sprint-4.5 T-010 / DEC-057) ============
// In-process Pub/Sub — lightweight cross-module integration without outbox overhead.
builder.Services.AddSingleton<IDomainEventPublisher, DomainEventPublisher>();

// ============ Services ============
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<CompanyService>();
builder.Services.AddScoped<ITenantBootstrap>(sp => sp.GetRequiredService<CompanyService>());
builder.Services.AddScoped<ICompanyService>(sp => sp.GetRequiredService<CompanyService>());
builder.Services.AddScoped<ICostCenterService, CostCenterService>();
builder.Services.AddScoped<IChartOfAccountsService, ChartOfAccountsService>();
builder.Services.AddScoped<IJournalEntryService, JournalEntryService>();
builder.Services.AddScoped<IGeneralLedgerService, GeneralLedgerService>();
builder.Services.AddScoped<IPostingRulesService, PostingRulesService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IResourceService, ResourceService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<IResourceAssignmentService, ResourceAssignmentService>();
builder.Services.AddScoped<IItemService, ItemService>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();
builder.Services.AddScoped<IUnitOfMeasureService, UnitOfMeasureService>();
builder.Services.AddScoped<IItemCategoryService, ItemCategoryService>();
builder.Services.AddScoped<IInventoryBootstrapper, InventoryBootstrapper>();
builder.Services.AddScoped<IStockMovementService, StockMovementService>();
builder.Services.AddScoped<IStockLevelService, StockLevelService>();
builder.Services.AddScoped<IStockReservationService, StockReservationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IVendorService, VendorService>();
builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
builder.Services.AddScoped<IGoodsReceiptService, GoodsReceiptService>();

// DEC-100 / DL 69: Register Payments services (was missing → 500 on /api/payments)
builder.Services.AddScoped<ERPSystem.Modules.Payments.Application.Services.IPaymentService, ERPSystem.Modules.Payments.Application.Services.PaymentService>();
builder.Services.AddScoped<ERPSystem.Modules.Payments.Infrastructure.IPaymentRepository, ERPSystem.Modules.Payments.Infrastructure.PaymentRepository>();
builder.Services.AddScoped<ERPSystem.Modules.Payments.Infrastructure.IPaymentSequenceRepository, ERPSystem.Modules.Payments.Infrastructure.PaymentSequenceRepository>();
builder.Services.AddValidatorsFromAssemblyContaining<ERPSystem.Modules.Payments.Application.CreatePaymentRequestValidator>();
builder.Services.AddScoped<IVendorBillService, VendorBillService>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<ILeaveRequestService, LeaveRequestService>();
builder.Services.AddScoped<IPayrollService, PayrollService>();
builder.Services.AddScoped<ISalaryStructureService, SalaryStructureService>();
// AR module (Phase 5 Sprint 1 — Finance AR)
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ISalesInvoiceService, SalesInvoiceService>();
builder.Services.AddScoped<IReceiptService, ReceiptService>();
builder.Services.AddScoped<IEosService, EosService>();
builder.Services.AddScoped<ILibyaTaxCalculator, LibyaTaxCalculator>();
builder.Services.AddScoped<IEosCalculator, EosCalculator>();
builder.Services.AddScoped<ISocialInsuranceCalculator, SocialInsuranceCalculator>();
builder.Services.AddScoped<IProjectReportService, ProjectReportService>();
builder.Services.AddScoped<IInventoryReportService, InventoryReportService>();
builder.Services.AddScoped<IFinanceReportService, FinanceReportService>();
builder.Services.AddScoped<IEventBus, EventBus>();
builder.Services.AddScoped<IIntegrationEventHandler<StockReceivedEvent>, StockReceivedEventHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<StockIssuedEvent>, StockIssuedEventHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<StockTransferredEvent>, StockTransferredEventHandler>();
builder.Services.AddScoped<IIntegrationEventHandler<StockAdjustedEvent>, StockAdjustedEventHandler>();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateProjectRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateItemRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<ReceiveStockRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateVendorRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreatePurchaseOrderRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateGoodsReceiptRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateVendorBillRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateDepartmentRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateEmployeeRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CheckInOutRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateLeaveRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<UpdateLeaveRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreatePayrollRunRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateSalesInvoiceRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CreateReceiptRequestValidator>();

// ============ Redis ============
// Redis اختياري في dev. لو connection string فاضي، ما نسجّل IConnectionMultiplexer
// (HealthController يطلبه اختيارياً: `IConnectionMultiplexer?`).
// لو connection string موجود لكن Redis مش شغّال، نستخدم `AbortOnConnectFail=false`
// عشان الـ multiplexer يستمر في إعادة المحاولة بدل رمي exception عند أول request.
var redisConn = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConn))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        var configOptions = ConfigurationOptions.Parse(redisConn);
        configOptions.AbortOnConnectFail = false;  // لا تفشل عند أول connect — استمر في إعادة المحاولة
        configOptions.ConnectRetry = 3;
        configOptions.ConnectTimeout = 1000;       // timeout قصير (1s) عشان ما نطوّل startup
        configOptions.SyncTimeout = 500;           // PingAsync / sync ops ترجع بسرعة
        configOptions.AsyncTimeout = 500;          // async ops ترجع بسرعة
        return ConnectionMultiplexer.Connect(configOptions);
    });
}

// ============ FluentMigrator ============
builder.Services.AddFluentMigratorCore()
    .ConfigureRunner(rb => rb
        .AddPostgres()
        .WithGlobalConnectionString(builder.Configuration.GetConnectionString("Postgres"))
        .ScanIn(typeof(CreateIdentityTables).Assembly).For.Migrations())
    .AddLogging(lb => lb.AddSerilog());
builder.Services.AddHostedService<MigrationRunnerHostedService>();
builder.Services.AddHostedService<SchemaMigrationHostedService>();  // v1.0.13: self-healing migration (nullable + FK on existing columns)
builder.Services.AddHostedService<DataTypeHostedService>();  // DEC-079: JSON-driven additive schema migrator (runs after FluentMigrator)
builder.Services.AddHostedService<OutboxProcessorHostedService>();

// ============ Seeder feature flags (Sprint-4 Day 1, DEC-023/DEC-032 + DEC-064) ============
// 2026-07-23: CLEAN AdminUserSeederHostedService replaces the broken JSON-driven
// seeders. We register the clean one and explicitly DISABLE the broken ones to
// prevent them from spamming the logs with errors.
var seedAdminUser = builder.Configuration.GetValue<bool>("Database:SeedAdminUser", true);
if (seedAdminUser)
{
    builder.Services.AddHostedService<AdminUserSeederHostedService>();
    Console.WriteLine("[CLEAN] AdminUserSeederHostedService registered (creates admin@alfajr.local / Demo1234).");
}
else
{
    Console.WriteLine("[CLEAN] AdminUserSeederHostedService SKIPPED (Database:SeedAdminUser=false).");
}

// The legacy seeders depend on stale JSON data files (DEC-086/087/088) and a
// buggy audit_log migration (DEC-082). They are explicitly disabled until
// those issues are resolved. Do NOT re-enable without fixing the underlying bugs.
Console.WriteLine("[LEGACY] ScenarioSeederHostedService + RealisticSeedHostedService DISABLED (broken JSON seeders).");
//var seedAlFajr = builder.Configuration.GetValue<bool>("Database:SeedAlFajrScenario", true);
//var seedAlBurj = builder.Configuration.GetValue<bool>("Database:SeedAlBurjScenario", false);
//var seedRealistic = builder.Configuration.GetValue<bool>("Database:SeedRealisticScenario", false);
//if (seedAlFajr) builder.Services.AddHostedService<ScenarioSeederHostedService>();
//if (seedRealistic) builder.Services.AddHostedService<RealisticSeedHostedService>();

// ============ Auth ============
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer, ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        };
    });
builder.Services.AddAuthorization();

// ============ CORS ============
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ============ MVC + Swagger ============
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ERP-SYSTEM API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// DEC-100 / DL 69: Surface unhandled exceptions with JSON body in production
// (default behavior is empty 500). Catch exceptions anywhere in the pipeline.
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception | path={Path} method={Method}", ctx.Request.Path, ctx.Request.Method);
        if (!ctx.Response.HasStarted)
        {
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "UnhandledException",
                message = ex.Message,
                detail = ex.InnerException?.Message,
                path = ctx.Request.Path.Value,
                type = ex.GetType().Name,
            });
        }
    }
});

// Sprint-4 Day 3 (DEC-045): request tracking FIRST so all downstream logs have RequestId.
app.UseRequestTracking();
app.UseSerilogRequestLogging();
app.UseMiddleware<RequestTimingMiddleware>(); // DEC-111
app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantMiddleware>();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
public partial class Program { }
