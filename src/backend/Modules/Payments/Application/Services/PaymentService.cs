using Dapper;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Finance.Infrastructure;
using ERPSystem.Modules.Payments.Application;
using ERPSystem.Modules.Payments.Entities;
using ERPSystem.Modules.Payments.Infrastructure;
using ERPSystem.Modules.Procurement.Infrastructure;
using ERPSystem.Shared.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Modules.Payments.Application.Services;

public interface IPaymentService
{
    Task<PaymentResult<PaymentResponse>> CreateAsync(Guid tenantId, Guid userId, CreatePaymentRequest req, CancellationToken ct);
    Task<PaymentResult<PaymentResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<PaymentResult<IReadOnlyList<PaymentResponse>>> ListAsync(
        Guid tenantId, string? partyType, Guid? partyId, PaymentStatus? status, int skip, int take, CancellationToken ct);
    Task<PaymentResult<PaymentResponse>> PostAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
    Task<PaymentResult<PaymentResponse>> AllocateAsync(Guid tenantId, Guid userId, Guid id, AllocatePaymentRequest req, CancellationToken ct);
}

/// <summary>
/// Payment service — يدعم AP (دفع مورّد) + AR (دفع عميل/استرداد) عبر PartyType discriminator.
///
/// عند Post:
///   - AP  (Vendor):   Dr 2210 (دائنون لموردين خارجيين) / Cr 1210 (النقدية)
///   - AR  (Customer): Dr 1210 (النقدية) / Cr 1230 (ذمم مدينة عملاء خارجيين)
///   - ينشئ JournalEntry (header + 2 lines) ويربطه بـ journal_entry_id على الـ Payment
///
/// عند Allocate:
///   - يُضيف PaymentAllocation(s) فقط — لا ينشئ قيد إضافي
///   - يتحقق أن sum(allocations) ≤ payment.amount
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _payments;
    private readonly IPaymentSequenceRepository _seq;
    private readonly IVendorRepository _vendors;
    private readonly IAccountRepository _accounts;
    private readonly IJournalEntryRepository _entries;
    private readonly IDbConnectionFactory _db;
    private readonly ERPSystem.Shared.SeedData.IDefaultAccountsHelper _defaultAccounts;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IPaymentRepository payments,
        IPaymentSequenceRepository seq,
        IVendorRepository vendors,
        IAccountRepository accounts,
        IJournalEntryRepository entries,
        IDbConnectionFactory db,
        ERPSystem.Shared.SeedData.IDefaultAccountsHelper defaultAccounts,
        ILogger<PaymentService> logger)
    {
        _payments = payments; _seq = seq; _vendors = vendors;
        _accounts = accounts; _entries = entries; _db = db;
        _defaultAccounts = defaultAccounts; _logger = logger;
    }

    public async Task<PaymentResult<PaymentResponse>> CreateAsync(Guid tenantId, Guid userId, CreatePaymentRequest req, CancellationToken ct)
    {
        // 1) تحقق من وجود الـ Party (Vendor حالياً — Customer مستقبلي)
        if (req.PartyType == PaymentPartyTypes.Vendor)
        {
            var vendor = await _vendors.GetByIdAsync(req.PartyId, ct);
            if (vendor == null || vendor.TenantId != tenantId)
                return PaymentResult<PaymentResponse>.Fail("المورّد غير موجود.", PaymentErrorCode.NotFoundParty);
        }
        else
        {
            // Customer غير موجود حالياً — نرفض بوضوح حتى يُضاف في Sprint لاحق
            return PaymentResult<PaymentResponse>.Fail(
                "نوع الطرف Customer غير مدعوم حالياً (لم تُنشَأ وحدة العملاء بعد).",
                PaymentErrorCode.NotFoundParty);
        }

        // 2) تحقق من الـ allocations (إن وُجدت)
        if (req.Allocations.Any(a => a.AmountApplied <= 0))
            return PaymentResult<PaymentResponse>.Fail("مبالغ التخصيص يجب أن تكون أكبر من صفر.", PaymentErrorCode.ValidationError);

        var allocSum = req.Allocations.Sum(a => a.AmountApplied);
        if (allocSum > req.Amount)
            return PaymentResult<PaymentResponse>.Fail(
                $"مجموع التخصيصات ({allocSum:N4}) أكبر من مبلغ الدفعة ({req.Amount:N4}).",
                PaymentErrorCode.OverAllocation);

        // 3) Validation للـ Refs — VendorBills يجب أن تكون موجودة وفي حالة Posted
        foreach (var a in req.Allocations.Where(x => x.RefType == PaymentRefTypes.VendorBill))
        {
            var ok = await ValidateVendorBillRefAsync(tenantId, a.RefId, a.AmountApplied, ct);
            if (!ok.Succeeded) return PaymentResult<PaymentResponse>.Fail(ok.Error!, ok.ErrorCode ?? PaymentErrorCode.NotFoundRef);
        }

        // 4) إنشاء الـ Payment (Draft)
        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartyType = req.PartyType,
            PartyId = req.PartyId,
            PaymentNumber = await _seq.GetNextPaymentNumberAsync(tenantId, ct),
            PaymentDate = req.PaymentDate,
            Amount = req.Amount,
            CurrencyCode = req.CurrencyCode.ToUpperInvariant(),
            PaymentMethod = req.PaymentMethod,
            BankAccountId = req.BankAccountId,
            Notes = req.Notes,
            Status = PaymentStatus.Draft,
            CreatedAt = now,
            CreatedBy = userId,
            UpdatedAt = now,
            UpdatedBy = userId,
            Allocations = req.Allocations.Select(a => new PaymentAllocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PaymentId = Guid.Empty, // يُملأ بعد Insert
                RefType = a.RefType,
                RefId = a.RefId,
                AmountApplied = a.AmountApplied
            }).ToList()
        };
        payment.Allocations.ForEach(a => a.PaymentId = payment.Id);

        await _payments.InsertAsync(payment, ct);
        if (payment.Allocations.Count > 0)
            await _payments.InsertAllocationsAsync(tenantId, payment.Id, payment.Allocations, ct);

        _logger.LogInformation("تم إنشاء Payment {Number} ({PartyType}/{Amount} {Currency}) لـ tenant {TenantId}",
            payment.PaymentNumber, payment.PartyType, payment.Amount, payment.CurrencyCode, tenantId);

        return PaymentResult<PaymentResponse>.Ok(MapToResponse(payment));
    }

    public async Task<PaymentResult<PaymentResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var p = await _payments.GetByIdAsync(id, ct);
        if (p == null || p.TenantId != tenantId)
            return PaymentResult<PaymentResponse>.Fail("السند غير موجود.", PaymentErrorCode.NotFound);
        return PaymentResult<PaymentResponse>.Ok(MapToResponse(p));
    }

    public async Task<PaymentResult<IReadOnlyList<PaymentResponse>>> ListAsync(
        Guid tenantId, string? partyType, Guid? partyId, PaymentStatus? status,
        int skip, int take, CancellationToken ct)
    {
        if (take is < 1 or > 200) take = 50;
        var list = await _payments.ListAsync(tenantId, partyType, partyId, status, skip, take, ct);
        // تجنّب N+1 — نحمل allocations دفعة واحدة
        foreach (var p in list)
            p.Allocations = (await _payments.GetAllocationsAsync(p.Id, ct)).ToList();
        return PaymentResult<IReadOnlyList<PaymentResponse>>.Ok(list.Select(MapToResponse).ToList());
    }

    public async Task<PaymentResult<PaymentResponse>> PostAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        // v1.0.30: self-heal — أضف الحسابات الناقصة تلقائياً
        await _defaultAccounts.EnsureDefaultAsync(tenantId, ct);
        var payment = await _payments.GetByIdAsync(id, ct);
        if (payment == null || payment.TenantId != tenantId)
            return PaymentResult<PaymentResponse>.Fail("السند غير موجود.", PaymentErrorCode.NotFound);
        if (payment.Status != PaymentStatus.Draft)
            return PaymentResult<PaymentResponse>.Fail(
                $"لا يمكن ترحيل سند في حالة {payment.Status}.", PaymentErrorCode.InvalidStatus);

        // 1) حدد الحسابات بحسب PartyType
        // AP (Vendor):    Dr 2210 (دائنون لموردين) / Cr 1210 (نقدية)
        // AR (Customer):  Dr 1210 (نقدية) / Cr 1230 (ذمم مدينة عملاء)
        string drCode = payment.PartyType == PaymentPartyTypes.Vendor ? "2210" : "1210";
        string crCode = payment.PartyType == PaymentPartyTypes.Vendor ? "1210" : "1230";

        var drAccount = await _accounts.GetByCodeAsync(tenantId, drCode, ct);
        var crAccount = await _accounts.GetByCodeAsync(tenantId, crCode, ct);
        if (drAccount == null || crAccount == null)
            return PaymentResult<PaymentResponse>.Fail(
                $"حساب محاسبي مفقود (Dr={drCode} أو Cr={crCode}) — تأكد من تطبيق Default CoA.",
                PaymentErrorCode.NotFound);

        if (!drAccount.IsPostable || !crAccount.IsPostable)
            return PaymentResult<PaymentResponse>.Fail(
                $"الحساب {drCode} أو {crCode} غير قابل للترحيل (IsPostable=false).",
                PaymentErrorCode.ValidationError);

        // 2) أنشئ القيد
        var entryNumber = await _entries.GetNextEntryNumberAsync(tenantId, ct);
        var now = DateTime.UtcNow;
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntryNumber = entryNumber,
            EntryDate = payment.PaymentDate,
            Description = $"سند دفع {payment.PaymentNumber} ({payment.PartyType})",
            Reference = payment.PaymentNumber,
            Status = JournalEntryStatus.Posted,
            CreatedByUserId = userId,
            PostedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
            Lines = new List<JournalLine>
            {
                new() { Id = Guid.NewGuid(), AccountId = drAccount.Id, Debit = payment.Amount, Credit = 0m, LineNumber = 1,
                        Description = $"Dr {drCode} {drAccount.Name} — {payment.PartyType}" },
                new() { Id = Guid.NewGuid(), AccountId = crAccount.Id, Debit = 0m, Credit = payment.Amount, LineNumber = 2,
                        Description = $"Cr {crCode} {crAccount.Name} — {payment.PartyType}" }
            }
        };

        await _entries.InsertAsync(entry, ct);

        // 3) حدّث الـ Payment → Posted
        payment.JournalEntryId = entry.Id;
        payment.Status = PaymentStatus.Posted;
        payment.PostedAt = now;
        payment.PostedBy = userId;
        payment.UpdatedAt = now;
        payment.UpdatedBy = userId;
        await _payments.UpdateAsync(payment, ct);

        _logger.LogInformation("تم ترحيل Payment {Number} → JournalEntry {Entry} (Dr {DrCode} / Cr {CrCode} × {Amount})",
            payment.PaymentNumber, entry.EntryNumber, drCode, crCode, payment.Amount);

        return PaymentResult<PaymentResponse>.Ok(MapToResponse(payment));
    }

    public async Task<PaymentResult<PaymentResponse>> AllocateAsync(Guid tenantId, Guid userId, Guid id, AllocatePaymentRequest req, CancellationToken ct)
    {
        var payment = await _payments.GetByIdAsync(id, ct);
        if (payment == null || payment.TenantId != tenantId)
            return PaymentResult<PaymentResponse>.Fail("السند غير موجود.", PaymentErrorCode.NotFound);
        if (payment.Status != PaymentStatus.Posted)
            return PaymentResult<PaymentResponse>.Fail(
                "لا يمكن التخصيص إلا على سند مُرحَّل.", PaymentErrorCode.InvalidStatus);

        // مجموع التخصيصات الحالي + الجديد
        var currentTotal = payment.Allocations.Sum(a => a.AmountApplied);
        var newTotal = req.Allocations.Sum(a => a.AmountApplied);
        if (currentTotal + newTotal > payment.Amount)
            return PaymentResult<PaymentResponse>.Fail(
                $"مجموع التخصيصات ({currentTotal + newTotal:N4}) أكبر من مبلغ السند ({payment.Amount:N4}).",
                PaymentErrorCode.OverAllocation);

        // تحقق من Refs
        foreach (var a in req.Allocations.Where(x => x.RefType == PaymentRefTypes.VendorBill))
        {
            var ok = await ValidateVendorBillRefAsync(tenantId, a.RefId, a.AmountApplied, ct);
            if (!ok.Succeeded) return PaymentResult<PaymentResponse>.Fail(ok.Error!, ok.ErrorCode ?? PaymentErrorCode.NotFoundRef);
        }

        var newAllocs = req.Allocations.Select(a => new PaymentAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentId = payment.Id,
            RefType = a.RefType,
            RefId = a.RefId,
            AmountApplied = a.AmountApplied
        }).ToList();

        await _payments.InsertAllocationsAsync(tenantId, payment.Id, newAllocs, ct);
        payment.Allocations.AddRange(newAllocs);

        _logger.LogInformation("أُضيف {Count} تخصيص(ات) على Payment {Number} (إجمالي {Total:N4}/{Amount:N4})",
            newAllocs.Count, payment.PaymentNumber, currentTotal + newTotal, payment.Amount);

        return PaymentResult<PaymentResponse>.Ok(MapToResponse(payment));
    }

    // ============ Helpers ============

    /// <summary>يتحقق أن الفاتورة موجودة + في حالة Posted + لم يُسدَّد كاملاً.</summary>
    private async Task<PaymentResult<bool>> ValidateVendorBillRefAsync(Guid tenantId, Guid billId, decimal amountToApply, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var bill = await conn.QueryFirstOrDefaultAsync<(decimal TotalAmount, string Status, string BillNumber)?>(new CommandDefinition(@"
            SELECT total_amount AS TotalAmount, status AS Status, bill_number AS BillNumber
            FROM vendor_bills WHERE id = @Id AND tenant_id = @TenantId LIMIT 1",
            new { Id = billId, TenantId = tenantId }, cancellationToken: ct));

        if (bill == null)
            return PaymentResult<bool>.Fail("فاتورة المورّد غير موجودة.", PaymentErrorCode.NotFoundRef);
        if (bill.Value.Status != "Posted")
            return PaymentResult<bool>.Fail($"الفاتورة {bill.Value.BillNumber} ليست مُرحَّلة.", PaymentErrorCode.NotFoundRef);

        var alreadyApplied = await _payments.SumAllocationsForRefAsync(tenantId, PaymentRefTypes.VendorBill, billId, ct);
        var outstanding = bill.Value.TotalAmount - alreadyApplied;
        if (amountToApply > outstanding + 0.0001m)
            return PaymentResult<bool>.Fail(
                $"مبلغ التخصيص ({amountToApply:N4}) أكبر من المتبقي على الفاتورة {bill.Value.BillNumber} ({outstanding:N4}).",
                PaymentErrorCode.OverAllocation);

        return PaymentResult<bool>.Ok(true);
    }

    private static PaymentResponse MapToResponse(Payment p) => new()
    {
        Id = p.Id,
        TenantId = p.TenantId,
        CompanyId = p.CompanyId,
        PartyType = p.PartyType,
        PartyId = p.PartyId,
        PaymentNumber = p.PaymentNumber,
        PaymentDate = p.PaymentDate,
        Amount = p.Amount,
        CurrencyCode = p.CurrencyCode,
        PaymentMethod = p.PaymentMethod,
        BankAccountId = p.BankAccountId,
        Notes = p.Notes,
        Status = p.Status,
        PostedAt = p.PostedAt,
        JournalEntryId = p.JournalEntryId,
        CreatedAt = p.CreatedAt,
        Allocations = p.Allocations.Select(a => new PaymentAllocationResponse
        {
            Id = a.Id, RefType = a.RefType, RefId = a.RefId, AmountApplied = a.AmountApplied
        }).ToList()
    };
}
