using ERPSystem.Modules.AccountsReceivable.Application;
using ERPSystem.Modules.AccountsReceivable.Entities;
using ERPSystem.Modules.AccountsReceivable.Infrastructure;
using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Application.Services;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Finance.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Modules.AccountsReceivable.Application.Services;

public interface IReceiptService
{
    Task<ArResult<ReceiptResponse>> CreateAsync(Guid tenantId, Guid userId, CreateReceiptRequest req, CancellationToken ct);
    Task<ArResult<ReceiptResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<ArResult<IReadOnlyList<ReceiptResponse>>> ListAsync(Guid tenantId, Guid? customerId, int skip, int take, CancellationToken ct);
    Task<ArResult<ReceiptResponse>> PostAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
    Task<ArResult<ReceiptResponse>> ReverseAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
}

/// <summary>
/// خدمة سندات القبض — الحسابات الافتراضية (CoA seed):
/// - 1210: النقدية (postable) — Dr عند Post
/// - 1230: ذمم مدينة (postable) — Cr عند Post
/// </summary>
public sealed class ReceiptService : IReceiptService
{
    private readonly IReceiptRepository _receipts;
    private readonly ICustomerRepository _customers;
    private readonly ISalesInvoiceRepository _invoices;
    private readonly IArDocumentSequenceRepository _seq;
    private readonly IJournalEntryService _journalEntries;
    private readonly IAccountRepository _accounts;
    private readonly ERPSystem.Shared.SeedData.IDefaultAccountsHelper _defaultAccounts;
    private readonly ILogger<ReceiptService> _logger;

    public ReceiptService(
        IReceiptRepository receipts,
        ICustomerRepository customers,
        ISalesInvoiceRepository invoices,
        IArDocumentSequenceRepository seq,
        IJournalEntryService journalEntries,
        IAccountRepository accounts,
        ERPSystem.Shared.SeedData.IDefaultAccountsHelper defaultAccounts,
        ILogger<ReceiptService> logger)
    {
        _receipts = receipts; _customers = customers; _invoices = invoices;
        _seq = seq; _journalEntries = journalEntries; _accounts = accounts;
        _defaultAccounts = defaultAccounts; _logger = logger;
    }

    public async Task<ArResult<ReceiptResponse>> CreateAsync(Guid tenantId, Guid userId, CreateReceiptRequest req, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(req.CustomerId, ct);
        if (customer == null || customer.TenantId != tenantId)
            return ArResult<ReceiptResponse>.Fail("العميل غير موجود.", ArErrorCode.NotFound);
        if (!customer.IsActive)
            return ArResult<ReceiptResponse>.Fail("العميل غير نشط.", ArErrorCode.BusinessRuleViolation);

        // مجموع التخصيصات يجب أن يساوي المبلغ
        var totalAllocated = req.Allocations?.Sum(a => a.AmountApplied) ?? 0m;
        if (Math.Abs(totalAllocated - req.Amount) > 0.0001m)
            return ArResult<ReceiptResponse>.Fail(
                $"مجموع التخصيصات ({totalAllocated:N4}) لا يساوي مبلغ السند ({req.Amount:N4}).", ArErrorCode.ValidationError);

        // التحقق من الفواتير
        var allocations = new List<ReceiptAllocation>();
        if (req.Allocations != null)
        {
            foreach (var a in req.Allocations)
            {
                var inv = await _invoices.GetByIdAsync(a.SalesInvoiceId, ct);
                if (inv == null || inv.TenantId != tenantId)
                    return ArResult<ReceiptResponse>.Fail($"الفاتورة {a.SalesInvoiceId} غير موجودة.", ArErrorCode.NotFound);
                if (inv.CustomerId != req.CustomerId)
                    return ArResult<ReceiptResponse>.Fail($"الفاتورة {inv.InvoiceNumber} لا تخص هذا العميل.", ArErrorCode.BusinessRuleViolation);
                if (inv.Status == SalesInvoiceStatus.Cancelled)
                    return ArResult<ReceiptResponse>.Fail($"الفاتورة {inv.InvoiceNumber} ملغاة.", ArErrorCode.BusinessRuleViolation);

                var outstanding = inv.TotalAmount - inv.PaidAmount;
                if (a.AmountApplied > outstanding + 0.0001m)
                    return ArResult<ReceiptResponse>.Fail(
                        $"التخصيص على الفاتورة {inv.InvoiceNumber} ({a.AmountApplied:N4}) يتجاوز المتبقي ({outstanding:N4}).",
                        ArErrorCode.BusinessRuleViolation);

                allocations.Add(new ReceiptAllocation
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SalesInvoiceId = a.SalesInvoiceId,
                    AmountApplied = a.AmountApplied,
                });
            }
        }

        // Idempotency: لو receiptNumber موجود، نرجّعه
        if (!string.IsNullOrWhiteSpace(req.PaymentMethod) && !PaymentMethod.All.Contains(req.PaymentMethod))
            return ArResult<ReceiptResponse>.Fail($"PaymentMethod غير صالح.", ArErrorCode.ValidationError);

        var receiptNumber = await _seq.GetNextNumberAsync(tenantId, "RC", ct);
        var now = DateTime.UtcNow;
        var receipt = new Receipt
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = req.CompanyId, // v1.0.11: nullable - was Guid.Empty causing FK violation
            CustomerId = req.CustomerId,
            ReceiptNumber = receiptNumber,
            ReceiptDate = req.ReceiptDate,
            Amount = req.Amount,
            CurrencyCode = req.CurrencyCode.ToUpperInvariant(),
            PaymentMethod = req.PaymentMethod,
            Notes = req.Notes,
            CreatedAt = now, CreatedBy = userId,
            UpdatedAt = now, UpdatedBy = userId,
            Allocations = allocations,
        };
        await _receipts.InsertAsync(receipt, ct);
        await _receipts.InsertAllocationsAsync(tenantId, receipt.Id, allocations, ct);

        _logger.LogInformation("تم إنشاء سند قبض {Rc} للعميل {Customer} بقيمة {Amount}", receiptNumber, customer.Code, req.Amount);

        if (req.PostImmediately)
        {
            var postResult = await PostInternalAsync(tenantId, userId, receipt, allocations, ct);
            if (!postResult.Succeeded)
                return ArResult<ReceiptResponse>.Fail($"تم إنشاء السند لكن فشل الترحيل: {postResult.Error}", postResult.ErrorCode ?? ArErrorCode.Internal);
            return postResult;
        }
        return ArResult<ReceiptResponse>.Ok(MapToResponse(receipt, customer.Name));
    }

    public async Task<ArResult<ReceiptResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var r = await _receipts.GetByIdAsync(id, ct);
        if (r == null || r.TenantId != tenantId)
            return ArResult<ReceiptResponse>.Fail("غير موجود.", ArErrorCode.NotFound);
        var customer = await _customers.GetByIdAsync(r.CustomerId, ct);
        return ArResult<ReceiptResponse>.Ok(MapToResponse(r, customer?.Name));
    }

    public async Task<ArResult<IReadOnlyList<ReceiptResponse>>> ListAsync(Guid tenantId, Guid? customerId, int skip, int take, CancellationToken ct)
    {
        if (take is < 1 or > 200) take = 50;
        var list = await _receipts.ListAsync(tenantId, customerId, skip, take, ct);
        var customerMap = new Dictionary<Guid, string>();
        foreach (var r in list)
        {
            if (!customerMap.ContainsKey(r.CustomerId))
            {
                var c = await _customers.GetByIdAsync(r.CustomerId, ct);
                if (c != null) customerMap[r.CustomerId] = c.Name;
            }
        }
        return ArResult<IReadOnlyList<ReceiptResponse>>.Ok(
            list.Select(r => MapToResponse(r, customerMap.GetValueOrDefault(r.CustomerId))).ToList());
    }

    public async Task<ArResult<ReceiptResponse>> PostAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        var receipt = await _receipts.GetByIdAsync(id, ct);
        if (receipt == null || receipt.TenantId != tenantId)
            return ArResult<ReceiptResponse>.Fail("غير موجود.", ArErrorCode.NotFound);
        if (receipt.PostedAt != null)
            return ArResult<ReceiptResponse>.Fail("السند مُرحّل بالفعل.", ArErrorCode.InvalidStatusTransition);

        return await PostInternalAsync(tenantId, userId, receipt, receipt.Allocations, ct);
    }

    /// <summary>المنطق الداخلي للترحيل: ينشئ القيد (Dr 1210 / Cr 1230) ويحدّث حالة الفواتير.</summary>
    private async Task<ArResult<ReceiptResponse>> PostInternalAsync(Guid tenantId, Guid userId, Receipt receipt, List<ReceiptAllocation> allocations, CancellationToken ct)
    {
        // v1.0.30: self-heal — إذا الحسابات الافتراضية ناقصة، أضفها تلقائياً قبل الترحيل
        await _defaultAccounts.EnsureDefaultAsync(tenantId, ct);
        var cashAccount = await _accounts.GetByCodeAsync(tenantId, "1210", ct);
        var arAccount = await _accounts.GetByCodeAsync(tenantId, "1230", ct)
            ?? await _accounts.GetByCodeAsync(tenantId, "1220", ct);
        if (cashAccount == null)
            return ArResult<ReceiptResponse>.Fail("حساب النقدية (1210) غير موجود في دليل الحسابات.", ArErrorCode.BusinessRuleViolation);
        if (arAccount == null)
            return ArResult<ReceiptResponse>.Fail("حساب الذمم (1230) غير موجود في دليل الحسابات.", ArErrorCode.BusinessRuleViolation);
        if (!cashAccount.IsPostable || !arAccount.IsPostable)
            return ArResult<ReceiptResponse>.Fail("حساب النقدية أو الذمم ليس قابلاً للترحيل.", ArErrorCode.BusinessRuleViolation);

        // إنشاء القيد
        var journalReq = new PostJournalEntryRequest
        {
            EntryDate = receipt.ReceiptDate,
            Description = $"سند قبض {receipt.ReceiptNumber}",
            Reference = $"RC:{receipt.Id}",
            Lines = new List<PostJournalLineRequest>
            {
                new() { AccountId = cashAccount.Id, Debit = receipt.Amount, Credit = 0, Description = "مدين - نقدية" },
                new() { AccountId = arAccount.Id, Debit = 0, Credit = receipt.Amount, Description = "دائن - ذمم مدينة" },
            }
        };
        var draft = await _journalEntries.CreateDraftAsync(tenantId, userId, journalReq, ct);
        if (!draft.Succeeded)
            return ArResult<ReceiptResponse>.Fail($"فشل إنشاء القيد: {draft.Error}", ArErrorCode.Internal);

        var posted = await _journalEntries.PostAsync(tenantId, userId, draft.Value!.Id, ct);
        if (!posted.Succeeded)
            return ArResult<ReceiptResponse>.Fail($"فشل ترحيل القيد: {posted.Error}", ArErrorCode.Internal);

        // تحديث السند
        receipt.JournalEntryId = posted.Value!.Id;
        receipt.PostedAt = DateTime.UtcNow;
        receipt.PostedBy = userId;
        receipt.UpdatedAt = DateTime.UtcNow;
        receipt.UpdatedBy = userId;
        await _receipts.UpdateAsync(receipt, ct);

        // تحديث الفواتير المخصصة
        foreach (var a in allocations)
        {
            var inv = await _invoices.GetByIdAsync(a.SalesInvoiceId, ct);
            if (inv == null) continue;

            inv.PaidAmount += a.AmountApplied;
            inv.Outstanding = inv.TotalAmount - inv.PaidAmount;
            // تحديث الحالة
            if (inv.Outstanding <= 0.0001m)
                inv.Status = SalesInvoiceStatus.Paid;
            else if (inv.PaidAmount > 0)
                inv.Status = SalesInvoiceStatus.PartiallyPaid;
            inv.UpdatedAt = DateTime.UtcNow;
            inv.UpdatedBy = userId;
            await _invoices.UpdateAsync(inv, ct);
        }

        _logger.LogInformation("تم ترحيل سند القبض {Rc} — القيد {Je}", receipt.ReceiptNumber, posted.Value.EntryNumber);
        var customer = await _customers.GetByIdAsync(receipt.CustomerId, ct);
        return ArResult<ReceiptResponse>.Ok(MapToResponse(receipt, customer?.Name));
    }

    public async Task<ArResult<ReceiptResponse>> ReverseAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        var receipt = await _receipts.GetByIdAsync(id, ct);
        if (receipt == null || receipt.TenantId != tenantId)
            return ArResult<ReceiptResponse>.Fail("غير موجود.", ArErrorCode.NotFound);
        if (receipt.PostedAt == null)
            return ArResult<ReceiptResponse>.Fail("لا يمكن عكس سند غير مُرحّل.", ArErrorCode.InvalidStatusTransition);

        // إنشاء قيد عكسي: Dr 1230 / Cr 1210 (نفس المبلغ)
        var cashAccount = await _accounts.GetByCodeAsync(tenantId, "1210", ct);
        var arAccount = await _accounts.GetByCodeAsync(tenantId, "1230", ct)
            ?? await _accounts.GetByCodeAsync(tenantId, "1220", ct);
        if (cashAccount == null || arAccount == null)
            return ArResult<ReceiptResponse>.Fail("الحسابات النقدية أو الذمم غير موجودة.", ArErrorCode.BusinessRuleViolation);

        var journalReq = new PostJournalEntryRequest
        {
            EntryDate = DateTime.UtcNow,
            Description = $"عكس سند قبض {receipt.ReceiptNumber}",
            Reference = $"RC-REV:{receipt.Id}",
            Lines = new List<PostJournalLineRequest>
            {
                new() { AccountId = arAccount.Id, Debit = receipt.Amount, Credit = 0, Description = "عكس - ذمم مدينة" },
                new() { AccountId = cashAccount.Id, Debit = 0, Credit = receipt.Amount, Description = "عكس - نقدية" },
            }
        };
        var draft = await _journalEntries.CreateDraftAsync(tenantId, userId, journalReq, ct);
        if (!draft.Succeeded)
            return ArResult<ReceiptResponse>.Fail($"فشل إنشاء القيد العكسي: {draft.Error}", ArErrorCode.Internal);
        var posted = await _journalEntries.PostAsync(tenantId, userId, draft.Value!.Id, ct);
        if (!posted.Succeeded)
            return ArResult<ReceiptResponse>.Fail($"فشل ترحيل القيد العكسي: {posted.Error}", ArErrorCode.Internal);

        // استرجاع مبالغ الفواتير
        foreach (var a in receipt.Allocations)
        {
            var inv = await _invoices.GetByIdAsync(a.SalesInvoiceId, ct);
            if (inv == null) continue;
            inv.PaidAmount = Math.Max(0, inv.PaidAmount - a.AmountApplied);
            inv.Outstanding = inv.TotalAmount - inv.PaidAmount;
            if (inv.PaidAmount <= 0)
                inv.Status = inv.PostedAt.HasValue ? SalesInvoiceStatus.Sent : SalesInvoiceStatus.Draft;
            else
                inv.Status = SalesInvoiceStatus.PartiallyPaid;
            inv.UpdatedAt = DateTime.UtcNow;
            inv.UpdatedBy = userId;
            await _invoices.UpdateAsync(inv, ct);
        }

        // نُلغي الـ PostedAt (السند الآن معكوس)
        receipt.PostedAt = null;
        receipt.PostedBy = null;
        receipt.Notes = (receipt.Notes ?? string.Empty) + $" [معكوس بقيد {posted.Value!.EntryNumber} في {DateTime.UtcNow:yyyy-MM-dd}]";
        receipt.UpdatedAt = DateTime.UtcNow;
        receipt.UpdatedBy = userId;
        await _receipts.UpdateAsync(receipt, ct);

        _logger.LogInformation("تم عكس سند القبض {Rc} بقيد {Je}", receipt.ReceiptNumber, posted.Value.EntryNumber);
        var customer = await _customers.GetByIdAsync(receipt.CustomerId, ct);
        return ArResult<ReceiptResponse>.Ok(MapToResponse(receipt, customer?.Name));
    }

    private static ReceiptResponse MapToResponse(Receipt r, string? customerName) => new()
    {
        Id = r.Id, TenantId = r.TenantId, CustomerId = r.CustomerId, CustomerName = customerName,
        ReceiptNumber = r.ReceiptNumber, ReceiptDate = r.ReceiptDate, Amount = r.Amount,
        CurrencyCode = r.CurrencyCode, PaymentMethod = r.PaymentMethod, Notes = r.Notes,
        PostedAt = r.PostedAt, PostedBy = r.PostedBy, JournalEntryId = r.JournalEntryId,
        CreatedAt = r.CreatedAt,
        Allocations = r.Allocations.Select(a => new ReceiptAllocationResponse
        {
            Id = a.Id, SalesInvoiceId = a.SalesInvoiceId, AmountApplied = a.AmountApplied,
        }).ToList(),
    };
}
