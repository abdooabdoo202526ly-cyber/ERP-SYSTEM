using Dapper;
using ERPSystem.Shared.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Shared.SeedData;

/// <summary>
/// v1.0.13: Self-healing schema migrator.
/// يطبّق ALTER TABLE statements اللي الـ DataTypeMigrator ما يعملها
/// (الـ DataTypeMigrator بس يضيف أعمدة جديدة، ما يغيّر الـ nullability أو FKs على أعمدة موجودة).
///
/// كل ALTER هنا idempotent — لو العمود بالفعل nullable، الـ ALTER بيعمل no-op.
/// </summary>
public sealed class SchemaMigrationHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SchemaMigrationHostedService> _logger;

    public SchemaMigrationHostedService(IServiceProvider sp, ILogger<SchemaMigrationHostedService> logger)
    {
        _serviceProvider = sp;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var conn = await db.CreateOltpConnectionAsync(cancellationToken);

            _logger.LogInformation("[SchemaMigrator] Running self-healing schema migrations...");

            // v1.0.13: company_id on AR tables was NOT NULL + RESTRICT FK → caused 23503 when service set Guid.Empty
            // الحل: nullable + ON DELETE SET NULL
            await MakeColumnNullableAsync(conn, "customers", "company_id", cancellationToken);
            await DropConstraintAsync(conn, "customers", "fk_customers_company_id", cancellationToken);
            await DropConstraintAsync(conn, "customers", "fk_customers_tenant_company", cancellationToken);
            await AddForeignKeyAsync(conn,
                "customers", "fk_customers_company_id", "company_id",
                "companies", "id", "SET NULL", cancellationToken);

            await MakeColumnNullableAsync(conn, "sales_invoices", "company_id", cancellationToken);
            await DropConstraintAsync(conn, "sales_invoices", "fk_sales_invoices_company_id", cancellationToken);
            await DropConstraintAsync(conn, "sales_invoices", "fk_si_tenant_company", cancellationToken);
            await AddForeignKeyAsync(conn,
                "sales_invoices", "fk_sales_invoices_company_id", "company_id",
                "companies", "id", "SET NULL", cancellationToken);

            await MakeColumnNullableAsync(conn, "receipts", "company_id", cancellationToken);
            await DropConstraintAsync(conn, "receipts", "fk_receipts_company_id", cancellationToken);
            await DropConstraintAsync(conn, "receipts", "fk_receipts_tenant_company", cancellationToken);
            await AddForeignKeyAsync(conn,
                "receipts", "fk_receipts_company_id", "company_id",
                "companies", "id", "SET NULL", cancellationToken);

            // v1.0.10: companies تاخد tax_id/phone/email/address columns
            await AddColumnIfMissingAsync(conn, "companies", "tax_id", "VARCHAR(50)", cancellationToken);
            await AddColumnIfMissingAsync(conn, "companies", "phone", "VARCHAR(50)", cancellationToken);
            await AddColumnIfMissingAsync(conn, "companies", "email", "VARCHAR(200)", cancellationToken);
            await AddColumnIfMissingAsync(conn, "companies", "address", "TEXT", cancellationToken);

            // v1.0.10: units_of_measure تاخد updated_at column
            await AddColumnIfMissingAsync(conn, "units_of_measure", "updated_at", "TIMESTAMPTZ NOT NULL DEFAULT now()", cancellationToken);

            // v1.0.14: audit_log يحتاج sequence أولاً (BIGSERIAL في JSON بس legacy schema يستخدم nextval)
            await EnsureSequenceAsync(conn, "audit_log_id_seq", cancellationToken);

            _logger.LogInformation("[SchemaMigrator] Self-healing schema migrations done");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SchemaMigrator] Self-healing migration failed (non-fatal — app will continue)");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task MakeColumnNullableAsync(System.Data.IDbConnection conn, string table, string column, CancellationToken ct)
    {
        try
        {
            // تحقق: العمود موجود و NOT NULL
            var isNullable = await conn.QueryFirstOrDefaultAsync<string?>(new CommandDefinition(
                $@"SELECT is_nullable FROM information_schema.columns
                   WHERE table_name = '{table}' AND column_name = '{column}'",
                cancellationToken: ct));
            if (isNullable == "YES")
            {
                _logger.LogInformation("[SchemaMigrator] {Table}.{Column} already nullable", table, column);
                return;
            }
            _logger.LogInformation("[SchemaMigrator] Making {Table}.{Column} nullable...", table, column);
            await conn.ExecuteAsync(new CommandDefinition(
                $@"ALTER TABLE {table} ALTER COLUMN {column} DROP NOT NULL",
                cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SchemaMigrator] Failed to make {Table}.{Column} nullable (may already be)", table, column);
        }
    }

    private async Task DropConstraintAsync(System.Data.IDbConnection conn, string table, string constraintName, CancellationToken ct)
    {
        try
        {
            var exists = await conn.QueryFirstOrDefaultAsync<int>(new CommandDefinition(
                $@"SELECT COUNT(*) FROM information_schema.table_constraints
                   WHERE table_name = '{table}' AND constraint_name = '{constraintName}'",
                cancellationToken: ct));
            if (exists == 0)
            {
                return; // already dropped
            }
            _logger.LogInformation("[SchemaMigrator] Dropping constraint {Table}.{Constraint}...", table, constraintName);
            await conn.ExecuteAsync(new CommandDefinition(
                $@"ALTER TABLE {table} DROP CONSTRAINT {constraintName}",
                cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SchemaMigrator] Failed to drop constraint {Table}.{Constraint}", table, constraintName);
        }
    }

    private async Task AddForeignKeyAsync(System.Data.IDbConnection conn, string table, string constraintName, string column, string refTable, string refColumn, string onDelete, CancellationToken ct)
    {
        try
        {
            var exists = await conn.QueryFirstOrDefaultAsync<int>(new CommandDefinition(
                $@"SELECT COUNT(*) FROM information_schema.table_constraints
                   WHERE table_name = '{table}' AND constraint_name = '{constraintName}'",
                cancellationToken: ct));
            if (exists > 0)
            {
                return; // already exists
            }
            _logger.LogInformation("[SchemaMigrator] Adding FK {Table}.{Constraint} -> {RefTable}.{RefColumn} ON DELETE {OnDelete}",
                table, constraintName, refTable, refColumn, onDelete);
            await conn.ExecuteAsync(new CommandDefinition(
                $@"ALTER TABLE {table} ADD CONSTRAINT {constraintName}
                   FOREIGN KEY ({column}) REFERENCES {refTable}({refColumn}) ON DELETE {onDelete}",
                cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SchemaMigrator] Failed to add FK {Table}.{Constraint}", table, constraintName);
        }
    }

    private async Task AddColumnIfMissingAsync(System.Data.IDbConnection conn, string table, string column, string definition, CancellationToken ct)
    {
        try
        {
            var exists = await conn.QueryFirstOrDefaultAsync<int>(new CommandDefinition(
                $@"SELECT COUNT(*) FROM information_schema.columns
                   WHERE table_name = '{table}' AND column_name = '{column}'",
                cancellationToken: ct));
            if (exists > 0) return;
            _logger.LogInformation("[SchemaMigrator] Adding column {Table}.{Column} ({Definition})", table, column, definition);
            await conn.ExecuteAsync(new CommandDefinition(
                $@"ALTER TABLE {table} ADD COLUMN {column} {definition}",
                cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SchemaMigrator] Failed to add column {Table}.{Column}", table, column);
        }
    }

    private async Task EnsureSequenceAsync(System.Data.IDbConnection conn, string sequenceName, CancellationToken ct)
    {
        try
        {
            var exists = await conn.QueryFirstOrDefaultAsync<int>(new CommandDefinition(
                $@"SELECT COUNT(*) FROM information_schema.sequences
                   WHERE sequence_name = '{sequenceName}'",
                cancellationToken: ct));
            if (exists > 0)
            {
                return; // already exists
            }
            _logger.LogInformation("[SchemaMigrator] Creating sequence {Sequence}...", sequenceName);
            await conn.ExecuteAsync(new CommandDefinition(
                $@"CREATE SEQUENCE {sequenceName}",
                cancellationToken: ct));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SchemaMigrator] Failed to create sequence {Sequence}", sequenceName);
        }
    }
}
