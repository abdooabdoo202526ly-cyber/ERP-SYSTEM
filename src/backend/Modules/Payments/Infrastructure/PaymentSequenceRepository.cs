using Dapper;
using ERPSystem.Shared.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Modules.Payments.Infrastructure;

/// <summary>
/// عداد أرقام مستندات الـ Payments — يعيد استخدام نفس جدول procurement_document_sequences
/// (الموجود في Procuremet) لتجنّب جدول منفصل. الـ prefix المدعوم هنا: "PAY".
///
/// ملاحظة معمارية: الجدول مش 1:1 مع Procurement في الـ concept — الأفضل لاحقاً نقله إلى
/// Finance (يدفع كل الـ tenants). الآن نعيد استخدامه لتجنّب migration إضافية.
/// </summary>
public sealed class PaymentSequenceRepository : IPaymentSequenceRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<PaymentSequenceRepository> _logger;
    public PaymentSequenceRepository(IDbConnectionFactory db, ILogger<PaymentSequenceRepository> logger)
    {
        _db = db; _logger = logger;
    }

    public async Task<string> GetNextPaymentNumberAsync(Guid tenantId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        // تأكد من وجود الجدول — آمن لو الـ migration لم تُشغَّل بعد
        await conn.ExecuteAsync(new CommandDefinition(@"
            CREATE TABLE IF NOT EXISTS procurement_document_sequences (
                tenant_id UUID NOT NULL,
                prefix VARCHAR(20) NOT NULL,
                last_number INT NOT NULL DEFAULT 0,
                PRIMARY KEY (tenant_id, prefix)
            )", cancellationToken: ct));

        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO procurement_document_sequences (tenant_id, prefix, last_number)
            VALUES (@TenantId, @Prefix, 1)
            ON CONFLICT (tenant_id, prefix) DO UPDATE
            SET last_number = procurement_document_sequences.last_number + 1",
            new { TenantId = tenantId, Prefix = "PAY" }, cancellationToken: ct));

        var last = await conn.QueryFirstOrDefaultAsync<int>(new CommandDefinition(
            "SELECT last_number FROM procurement_document_sequences WHERE tenant_id = @TenantId AND prefix = 'PAY'",
            new { TenantId = tenantId }, cancellationToken: ct));

        var year = DateTime.UtcNow.Year;
        var number = $"PAY-{year}-{last:D4}";
        _logger.LogDebug("Generated payment number {Number} for tenant {TenantId}", number, tenantId);
        return number;
    }
}

public interface IPaymentSequenceRepository
{
    Task<string> GetNextPaymentNumberAsync(Guid tenantId, CancellationToken ct);
}
