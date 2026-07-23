using Dapper;
using ERPSystem.Modules.Finance.Application;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.Finance.Application.Services;

public interface IAPAgingService
{
    Task<APAgingReportResponse> GetAsync(Guid tenantId, DateTime asOfDate, CancellationToken ct);
}

/// <summary>
/// AP Aging — أعمار الذمم الدائنة (مستحقات الموردين).
///
/// لكل VendorBill في حالة Posted:
///   Outstanding = total_amount - sum(allocations applied via PaymentAllocation)
///   Age = asOfDate - bill.due_date (أيام)
///
/// Buckets:
///   Current  : 0-30 يوم
///   31-60    : 31-60 يوم
///   61-90    : 61-90 يوم
///   91+      : أكبر من 90 يوم
///
/// ملاحظة: الفواتير بلا due_date (Cash) تُعتبر Current دائماً (age = 0).
/// </summary>
public sealed class APAgingService : IAPAgingService
{
    private readonly IDbConnectionFactory _db;
    public APAgingService(IDbConnectionFactory db) => _db = db;

    public async Task<APAgingReportResponse> GetAsync(Guid tenantId, DateTime asOfDate, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);

        // استعلام واحد: لكل bill في حالة Posted، نحسب المبلغ المُسدَّد عبر payment_allocations
        // نُمرّر Status كـ Dapper parameter (EnumStringTypeHandler يكتبها كنص) لتجنّب ambiguity
        // بين الـ schema المُعدَّل و الـ migration الأصلي.
        const string sql = @"
            SELECT vb.id AS BillId, vb.bill_number AS BillNumber, vb.vendor_id AS VendorId,
                   v.code AS VendorCode, v.name AS VendorName,
                   vb.due_date AS DueDate, vb.total_amount AS TotalAmount,
                   COALESCE((
                       SELECT SUM(pa.amount_applied)
                       FROM payment_allocations pa
                       INNER JOIN payments p ON p.id = pa.payment_id
                       WHERE p.tenant_id = vb.tenant_id
                         AND p.status = @PostedStatus
                         AND pa.ref_type = 'VendorBill'
                         AND pa.ref_id = vb.id
                   ), 0) AS PaidAmount
            FROM vendor_bills vb
            INNER JOIN vendors v ON v.id = vb.vendor_id
            WHERE vb.tenant_id = @TenantId
              AND vb.status = @PostedStatus
              AND vb.total_amount > 0
            ORDER BY v.code, vb.bill_number";

        var rows = (await conn.QueryAsync<AgingRow>(new CommandDefinition(sql,
            new { TenantId = tenantId, AsOfDate = asOfDate, PostedStatus = "Posted" }, cancellationToken: ct))).ToList();

        // تجميع per vendor
        var byVendor = new Dictionary<Guid, APAgingVendorBucket>();
        foreach (var r in rows)
        {
            var outstanding = r.TotalAmount - r.PaidAmount;
            if (outstanding <= 0.0001m) continue; // مدفوع بالكامل — لا يدخل

            // Age: نحسب من due_date (أو bill_date لو due_date null)
            var ageBase = r.DueDate ?? r.BillDate;
            var days = (int)(asOfDate.Date - ageBase.Date).TotalDays;

            if (!byVendor.TryGetValue(r.VendorId, out var bucket))
            {
                bucket = new APAgingVendorBucket
                {
                    VendorCode = r.VendorCode,
                    VendorName = r.VendorName
                };
                byVendor[r.VendorId] = bucket;
            }

            if (days <= 30) bucket.Current += outstanding;
            else if (days <= 60) bucket.Days31To60 += outstanding;
            else if (days <= 90) bucket.Days61To90 += outstanding;
            else bucket.Days91Plus += outstanding;
        }

        return new APAgingReportResponse
        {
            AsOfDate = asOfDate,
            Vendors = byVendor.Values
                .OrderByDescending(v => v.Total)
                .ToList()
        };
    }

    private sealed class AgingRow
    {
        public Guid BillId { get; set; }
        public string BillNumber { get; set; } = string.Empty;
        public Guid VendorId { get; set; }
        public string VendorCode { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public DateTime BillDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
    }
}
