using ERPSystem.Modules.AccountsReceivable.Application;
using ERPSystem.Modules.AccountsReceivable.Entities;
using ERPSystem.Modules.AccountsReceivable.Infrastructure;
using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Application.Services;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Finance.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Modules.AccountsReceivable.Application.Services;

public interface ISalesInvoiceService
{
    Task<ArResult<SalesInvoiceResponse>> CreateAsync(Guid tenantId, Guid userId, CreateSalesInvoiceRequest req, CancellationToken ct);
    Task<ArResult<SalesInvoiceResponse>> UpdateAsync(Guid tenantId, Guid userId, Guid id, UpdateSalesInvoiceRequest req, CancellationToken ct);
    Task<ArResult<SalesInvoiceResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<ArResult<IReadOnlyList<SalesInvoiceResponse>>> ListAsync(Guid tenantId, Guid? customerId, SalesInvoiceStatus? status, int skip, int take, CancellationToken ct);
    Task<ArResult<SalesInvoiceResponse>> PostAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
    Task<ArResult<SalesInvoiceResponse>> CancelAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
    Task<ArResult<ArAgingReport>> GetAgingReportAsync(Guid tenantId, DateTime asOfDate, CancellationToken ct);
}

/// <summary>
/// خدمة فواتير المبيعات — الحسابات الافتراضية (CoA seed):
/// - 1230: ذمم مدينة (عملاء خارجيين) — postable
/// - 5110: إيرادات المشاريع — postable (الـ Cr عند Post)
/// - 1210: النقدية — postable
/// الـ fallback chain (5110 ?? 4110) — في حال تغيّر الشجرة لاحقاً.
/// </summary>
public sealed class SalesInvoiceService : ISalesInvoiceService
{
    private readonly ISalesInvoiceRepository _invoices;
    private readonly ICustomerRepository _customers;
    private readonly IArDocumentSequenceRepository _seq;
    private readonly IJournalEntryService _journalEntries;
    private readonly IAccountRepository _accounts;
    private readonly ERPSystem.Shared.SeedData.IDefaultAccountsHelper _defaultAccounts;
    private readonly ILogger<SalesInvoiceService> _logger;

    public SalesInvoiceService(
        ISalesInvoiceRepository invoices,
        ICustomerRepository customers,
        IArDocumentSequenceRepository seq,
        IJournalEntryService journalEntries,
        IAccountRepository accounts,
        ERPSystem.Shared.SeedData.IDefaultAccountsHelper defaultAccounts,
        ILogger<SalesInvoiceService> logger)
    {
        _invoices = invoices; _customers = customers; _seq = seq;
        _journalEntries = journalEntries; _accounts = accounts;
        _defaultAccounts = defaultAccounts; _logger = logger;
    }

    public async Task<ArResult<SalesInvoiceResponse>> CreateAsync(Guid tenantId, Guid userId, CreateSalesInvoiceRequest req, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(req.CustomerId, ct);
        if (customer == null || customer.TenantId != tenantId)
            return ArResult<SalesInvoiceResponse>.Fail("العميل غير موجود.", ArErrorCode.NotFound);
        if (!customer.IsActive)
            return ArResult<SalesInvoiceResponse>.Fail("العميل غير نشط.", ArErrorCode.BusinessRuleViolation);
        if (req.Lines == null || req.Lines.Count == 0)
            return ArResult<SalesInvoiceResponse>.Fail("الفاتورة يجب أن تحتوي على بند واحد على الأقل.", ArErrorCode.ValidationError);

        // توليد رقم فاتورة تلقائي
        var invoiceNumber = await _seq.GetNextNumberAsync(tenantId, "SI", ct);

        // حساب الـ totals
        decimal subtotal = 0, taxAmount = 0;
        var lineEntities = new List<SalesInvoiceLine>();
        for (int i = 0; i < req.Lines.Count; i++)
        {
            var l = req.Lines[i];
            var lineSub = l.Quantity * l.UnitPrice;
            var lineTax = lineSub * l.TaxRate;
            subtotal += lineSub;
            taxAmount += lineTax;
            lineEntities.Add(new SalesInvoiceLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ItemId = l.ItemId,
                Description = l.Description.Trim(),
                LineNumber = i + 1,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                TaxRate = l.TaxRate,
                LineTotal = lineSub,
            });
        }
        var total = subtotal + taxAmount;

        // التحقق من حد الائتمان (تحذير — لا نمنع للـ MVP)
        // Future: لو Total + outstanding > credit limit، ارفض

        var now = DateTime.UtcNow;
        var inv = new SalesInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = req.CompanyId, // v1.0.11: nullable - was Guid.Empty causing FK violation
            CustomerId = req.CustomerId,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = req.InvoiceDate,
            DueDate = req.DueDate,
            CurrencyCode = req.CurrencyCode.ToUpperInvariant(),
            ExchangeRate = req.ExchangeRate,
            Subtotal = subtotal,
            TaxAmount = taxAmount,
            TotalAmount = total,
            PaidAmount = 0,
            Outstanding = total,
            Status = SalesInvoiceStatus.Draft,
            Notes = req.Notes,
            ProjectId = req.ProjectId,
            CreatedAt = now, CreatedBy = userId,
            UpdatedAt = now, UpdatedBy = userId,
            Lines = lineEntities,
        };
        await _invoices.InsertAsync(inv, ct);
        await _invoices.InsertLinesAsync(tenantId, inv.Id, lineEntities, ct);
        _logger.LogInformation("تم إنشاء فاتورة {Inv} للعميل {Customer} بقيمة {Total}", invoiceNumber, customer.Code, total);

        if (req.PostImmediately)
        {
            var postResult = await PostInternalAsync(tenantId, userId, inv, ct);
            if (!postResult.Succeeded)
                return ArResult<SalesInvoiceResponse>.Fail($"تم إنشاء الفاتورة لكن فشل الترحيل: {postResult.Error}", postResult.ErrorCode ?? ArErrorCode.Internal);
            return postResult;
        }
        return ArResult<SalesInvoiceResponse>.Ok(MapToResponse(inv, customer.Name));
    }

    public async Task<ArResult<SalesInvoiceResponse>> UpdateAsync(Guid tenantId, Guid userId, Guid id, UpdateSalesInvoiceRequest req, CancellationToken ct)
    {
        var inv = await _invoices.GetByIdAsync(id, ct);
        if (inv == null || inv.TenantId != tenantId)
            return ArResult<SalesInvoiceResponse>.Fail("غير موجود.", ArErrorCode.NotFound);
        if (inv.Status != SalesInvoiceStatus.Draft)
            return ArResult<SalesInvoiceResponse>.Fail("لا يمكن تعديل فاتورة غير مسودة.", ArErrorCode.InvalidStatusTransition);

        // إعادة حساب الـ totals
        decimal subtotal = 0, taxAmount = 0;
        var newLines = new List<SalesInvoiceLine>();
        for (int i = 0; i < req.Lines.Count; i++)
        {
            var l = req.Lines[i];
            var lineSub = l.Quantity * l.UnitPrice;
            var lineTax = lineSub * l.TaxRate;
            subtotal += lineSub;
            taxAmount += lineTax;
            newLines.Add(new SalesInvoiceLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SalesInvoiceId = inv.Id,
                ItemId = l.ItemId,
                Description = l.Description.Trim(),
                LineNumber = i + 1,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                TaxRate = l.TaxRate,
                LineTotal = lineSub,
            });
        }
        var total = subtotal + taxAmount;

        inv.DueDate = req.DueDate;
        inv.CurrencyCode = req.CurrencyCode.ToUpperInvariant();
        inv.ExchangeRate = req.ExchangeRate;
        inv.Notes = req.Notes;
        inv.ProjectId = req.ProjectId;
        inv.Subtotal = subtotal;
        inv.TaxAmount = taxAmount;
        inv.TotalAmount = total;
        inv.Outstanding = total - inv.PaidAmount;
        inv.UpdatedAt = DateTime.UtcNow;
        inv.UpdatedBy = userId;
        await _invoices.UpdateAsync(inv, ct);
        await _invoices.UpdateLinesAsync(tenantId, inv.Id, newLines, ct);
        inv.Lines = newLines;
        return ArResult<SalesInvoiceResponse>.Ok(MapToResponse(inv, null));
    }

    public async Task<ArResult<SalesInvoiceResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var inv = await _invoices.GetByIdAsync(id, ct);
        if (inv == null || inv.TenantId != tenantId)
            return ArResult<SalesInvoiceResponse>.Fail("غير موجود.", ArErrorCode.NotFound);
        var customer = await _customers.GetByIdAsync(inv.CustomerId, ct);
        return ArResult<SalesInvoiceResponse>.Ok(MapToResponse(inv, customer?.Name));
    }

    public async Task<ArResult<IReadOnlyList<SalesInvoiceResponse>>> ListAsync(Guid tenantId, Guid? customerId, SalesInvoiceStatus? status, int skip, int take, CancellationToken ct)
    {
        if (take is < 1 or > 200) take = 50;
        var list = await _invoices.ListAsync(tenantId, customerId, status, skip, take, ct);
        // جلب أسماء العملاء (دفعة واحدة)
        var customerIds = list.Select(i => i.CustomerId).Distinct().ToList();
        var customerMap = new Dictionary<Guid, string>();
        foreach (var cid in customerIds)
        {
            var c = await _customers.GetByIdAsync(cid, ct);
            if (c != null) customerMap[cid] = c.Name;
        }
        return ArResult<IReadOnlyList<SalesInvoiceResponse>>.Ok(
            list.Select(i => MapToResponse(i, customerMap.GetValueOrDefault(i.CustomerId))).ToList());
    }

    public async Task<ArResult<SalesInvoiceResponse>> PostAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        var inv = await _invoices.GetByIdAsync(id, ct);
        if (inv == null || inv.TenantId != tenantId)
            return ArResult<SalesInvoiceResponse>.Fail("غير موجود.", ArErrorCode.NotFound);
        if (inv.Status != SalesInvoiceStatus.Draft)
            return ArResult<SalesInvoiceResponse>.Fail(
                $"لا يمكن ترحيل فاتورة في حالة {inv.Status}.", ArErrorCode.InvalidStatusTransition);

        return await PostInternalAsync(tenantId, userId, inv, ct);
    }

    /// <summary>المنطق الداخلي للترحيل: ينشئ القيد (Dr 1230 / Cr 5110) ويرفع الحالة إلى Sent.</summary>
    private async Task<ArResult<SalesInvoiceResponse>> PostInternalAsync(Guid tenantId, Guid userId, SalesInvoice inv, CancellationToken ct)
    {
        // v1.0.30: self-heal — أضف الحسابات الناقصة تلقائياً
        await _defaultAccounts.EnsureDefaultAsync(tenantId, ct);
        // جلب الحسابات (fallback chain)
        var arAccount = await _accounts.GetByCodeAsync(tenantId, "1230", ct)
            ?? await _accounts.GetByCodeAsync(tenantId, "1220", ct);
        var revenueAccount = await _accounts.GetByCodeAsync(tenantId, "5110", ct)
            ?? await _accounts.GetByCodeAsync(tenantId, "4100", ct);

        if (arAccount == null)
            return ArResult<SalesInvoiceResponse>.Fail("حساب الذمم المدينة (1230) غير موجود في دليل الحسابات.", ArErrorCode.BusinessRuleViolation);
        if (revenueAccount == null)
            return ArResult<SalesInvoiceResponse>.Fail("حساب الإيرادات (5110) غير موجود في دليل الحسابات.", ArErrorCode.BusinessRuleViolation);
        if (!arAccount.IsPostable || !revenueAccount.IsPostable)
            return ArResult<SalesInvoiceResponse>.Fail("حساب الذمم أو الإيرادات ليس قابلاً للترحيل (IsPostable=false).", ArErrorCode.BusinessRuleViolation);

        // إنشاء القيد: Dr 1230 / Cr 5110
        var journalReq = new PostJournalEntryRequest
        {
            EntryDate = inv.InvoiceDate,
            Description = $"فاتورة مبيعات {inv.InvoiceNumber}",
            Reference = $"SI:{inv.Id}", // يحفظ SalesInvoiceId في الـ reference
            Lines = new List<PostJournalLineRequest>
            {
                new() { AccountId = arAccount.Id, Debit = inv.TotalAmount, Credit = 0, Description = "مدين - ذمم مدينة" },
                new() { AccountId = revenueAccount.Id, Debit = 0, Credit = inv.TotalAmount, Description = "دائن - إيرادات" },
            }
        };
        var draft = await _journalEntries.CreateDraftAsync(tenantId, userId, journalReq, ct);
        if (!draft.Succeeded)
            return ArResult<SalesInvoiceResponse>.Fail($"فشل إنشاء القيد: {draft.Error}", draft.ErrorCode == FinanceErrorCode.NotFound ? ArErrorCode.NotFound : ArErrorCode.Internal);

        // ترحيل القيد
        var posted = await _journalEntries.PostAsync(tenantId, userId, draft.Value!.Id, ct);
        if (!posted.Succeeded)
            return ArResult<SalesInvoiceResponse>.Fail($"فشل ترحيل القيد: {posted.Error}", ArErrorCode.Internal);

        // تحديث الفاتورة
        inv.JournalEntryId = posted.Value!.Id;
        inv.Status = SalesInvoiceStatus.Sent;
        inv.PostedAt = DateTime.UtcNow;
        inv.PostedBy = userId;
        inv.PaidAmount = 0;
        inv.Outstanding = inv.TotalAmount;
        inv.UpdatedAt = DateTime.UtcNow;
        inv.UpdatedBy = userId;
        await _invoices.UpdateAsync(inv, ct);

        _logger.LogInformation("تم ترحيل فاتورة {Inv} — القيد {Je}", inv.InvoiceNumber, posted.Value.EntryNumber);
        var customer = await _customers.GetByIdAsync(inv.CustomerId, ct);
        return ArResult<SalesInvoiceResponse>.Ok(MapToResponse(inv, customer?.Name));
    }

    public async Task<ArResult<SalesInvoiceResponse>> CancelAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        var inv = await _invoices.GetByIdAsync(id, ct);
        if (inv == null || inv.TenantId != tenantId)
            return ArResult<SalesInvoiceResponse>.Fail("غير موجود.", ArErrorCode.NotFound);
        if (inv.Status == SalesInvoiceStatus.Cancelled)
            return ArResult<SalesInvoiceResponse>.Fail("الفاتورة ملغاة بالفعل.", ArErrorCode.InvalidStatusTransition);
        if (inv.PaidAmount > 0)
            return ArResult<SalesInvoiceResponse>.Fail("لا يمكن إلغاء فاتورة عليها مدفوعات.", ArErrorCode.BusinessRuleViolation);

        inv.Status = SalesInvoiceStatus.Cancelled;
        inv.UpdatedAt = DateTime.UtcNow;
        inv.UpdatedBy = userId;
        await _invoices.UpdateAsync(inv, ct);
        return ArResult<SalesInvoiceResponse>.Ok(MapToResponse(inv, null));
    }

    public async Task<ArResult<ArAgingReport>> GetAgingReportAsync(Guid tenantId, DateTime asOfDate, CancellationToken ct)
    {
        var openInvoices = await _invoices.ListAllOpenAsync(tenantId, ct);
        var rows = new Dictionary<Guid, ArAgingRow>();
        foreach (var inv in openInvoices)
        {
            // المبلغ غير المسدد
            var outstanding = inv.TotalAmount - inv.PaidAmount;
            if (outstanding <= 0) continue;

            // الأيام من تاريخ الفاتورة (أو من dueDate؟ المعيار: days past due = asOfDate - DueDate)
            // للأعمار: نستخدم days past due (تأخير) كما في FRD
            var daysPast = (asOfDate.Date - (inv.DueDate ?? inv.InvoiceDate).Date).Days;
            decimal bucket = 0;
            if (daysPast <= 30) bucket = outstanding;            // 0-30 (يشمل الفواتير غير المستحقة بعد: days < 0)
            else if (daysPast <= 60) bucket = outstanding;        // 31-60
            else if (daysPast <= 90) bucket = outstanding;        // 61-90
            else if (daysPast <= 120) bucket = outstanding;       // 91-120
            else bucket = outstanding;                            // 120+

            if (!rows.TryGetValue(inv.CustomerId, out var row))
            {
                row = new ArAgingRow
                {
                    CustomerId = inv.CustomerId,
                    CustomerCode = string.Empty,
                    CustomerName = string.Empty,
                };
                rows[inv.CustomerId] = row;
            }
            if (daysPast <= 30) row.Buckets.Bucket0To30 += bucket;
            else if (daysPast <= 60) row.Buckets.Bucket31To60 += bucket;
            else if (daysPast <= 90) row.Buckets.Bucket61To90 += bucket;
            else if (daysPast <= 120) row.Buckets.Bucket91To120 += bucket;
            else row.Buckets.Bucket120Plus += bucket;
        }

        // املأ أسماء العملاء
        var result = new List<ArAgingRow>();
        var grand = new ArAgingBucket();
        foreach (var row in rows.Values)
        {
            var c = await _customers.GetByIdAsync(row.CustomerId, ct);
            if (c != null)
            {
                row.CustomerCode = c.Code;
                row.CustomerName = c.Name;
            }
            row.Buckets.Total = row.Buckets.Bucket0To30 + row.Buckets.Bucket31To60 +
                                row.Buckets.Bucket61To90 + row.Buckets.Bucket91To120 + row.Buckets.Bucket120Plus;
            grand.Bucket0To30 += row.Buckets.Bucket0To30;
            grand.Bucket31To60 += row.Buckets.Bucket31To60;
            grand.Bucket61To90 += row.Buckets.Bucket61To90;
            grand.Bucket91To120 += row.Buckets.Bucket91To120;
            grand.Bucket120Plus += row.Buckets.Bucket120Plus;
            grand.Total += row.Buckets.Total;
            result.Add(row);
        }
        // ترتيب حسب الإجمالي تنازلياً
        result = result.OrderByDescending(r => r.Buckets.Total).ToList();

        return ArResult<ArAgingReport>.Ok(new ArAgingReport
        {
            AsOfDate = asOfDate,
            Rows = result,
            GrandTotal = grand,
        });
    }

    private static SalesInvoiceResponse MapToResponse(SalesInvoice inv, string? customerName) => new()
    {
        Id = inv.Id, TenantId = inv.TenantId, CustomerId = inv.CustomerId, CustomerName = customerName,
        InvoiceNumber = inv.InvoiceNumber, InvoiceDate = inv.InvoiceDate, DueDate = inv.DueDate,
        CurrencyCode = inv.CurrencyCode, ExchangeRate = inv.ExchangeRate,
        Subtotal = inv.Subtotal, TaxAmount = inv.TaxAmount, TotalAmount = inv.TotalAmount,
        PaidAmount = inv.PaidAmount, Outstanding = inv.TotalAmount - inv.PaidAmount,
        Status = inv.Status, Notes = inv.Notes, ProjectId = inv.ProjectId,
        PostedAt = inv.PostedAt, PostedBy = inv.PostedBy, JournalEntryId = inv.JournalEntryId,
        CreatedAt = inv.CreatedAt,
        Lines = inv.Lines.Select(l => new SalesInvoiceLineResponse
        {
            Id = l.Id, LineNumber = l.LineNumber, Description = l.Description, ItemId = l.ItemId,
            Quantity = l.Quantity, UnitPrice = l.UnitPrice, TaxRate = l.TaxRate, LineTotal = l.LineTotal,
        }).ToList(),
    };
}
