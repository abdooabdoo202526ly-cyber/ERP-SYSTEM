using ERPSystem.Modules.HR.Entities;
using ERPSystem.Modules.HR.Infrastructure;
using ERPSystem.Modules.Payroll.Application;
using ERPSystem.Modules.Payroll.Domain.Calculators;
using ERPSystem.Modules.Payroll.Domain.Entities;
using ERPSystem.Modules.Payroll.Infrastructure;
using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Application.Services;
using ERPSystem.Modules.Finance.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Modules.Payroll.Application.Services;

// ============== Result envelope (نفس النمط الموحَّد) ==============

/// <summary>غلاف نتيجة موحَّد لكل عمليات الـ Payroll (Service Layer).</summary>
public sealed class PayrollResult<T>
{
    public bool Succeeded { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public PayrollErrorCode? ErrorCode { get; init; }
    public static PayrollResult<T> Ok(T v) => new() { Succeeded = true, Value = v };
    public static PayrollResult<T> Fail(string e, PayrollErrorCode c) => new() { Succeeded = false, Error = e, ErrorCode = c };
}

/// <summary>تصنيفات أخطاء الـ Payroll (تُستخدم في Problem Details).</summary>
public enum PayrollErrorCode
{
    NotFound,
    AlreadyExists,
    ValidationError,
    InvalidStatusTransition,
    BusinessRuleViolation,
    Internal
}

// ============== Service Contract ==============

/// <summary>عقد خدمة Payroll — إنشاء/معالجة/ترحيل دورة رواتب + استعلام payslips.</summary>
public interface IPayrollService
{
    /// <summary>قائمة دورات الرواتب للـ tenant (مع filter اختياري على الحالة).</summary>
    Task<PayrollResult<IReadOnlyList<PayrollRunResponse>>> ListRunsAsync(Guid tenantId, PayrollRunStatus? status, int skip, int take, CancellationToken ct);

    /// <summary>تفاصيل دورة رواتب واحدة (Run header فقط، بدون items).</summary>
    Task<PayrollResult<PayrollRunResponse>> GetRunAsync(Guid tenantId, Guid runId, CancellationToken ct);

    /// <summary>إنشاء دورة رواتب جديدة في حالة Draft.</summary>
    Task<PayrollResult<PayrollRunResponse>> CreateRunAsync(Guid tenantId, Guid userId, CreatePayrollRunRequest req, CancellationToken ct);

    /// <summary>معالجة الدورة: يحسب payslip لكل موظف نشط عبر الـ calculators ويحدّث الحالة إلى Processing.</summary>
    Task<PayrollResult<PayrollRunResponse>> ProcessRunAsync(Guid tenantId, Guid userId, Guid runId, CancellationToken ct);

    /// <summary>ترحيل الدورة: ينشئ JournalEntry (Dr Salary Expense / Cr Cash) ويحدّث الحالة إلى Posted.</summary>
    Task<PayrollResult<PayrollRunResponse>> PostRunAsync(Guid tenantId, Guid userId, Guid runId, CancellationToken ct);

    /// <summary>قائمة payslips الدورة.</summary>
    Task<PayrollResult<IReadOnlyList<PayslipResponse>>> GetItemsAsync(Guid tenantId, Guid runId, CancellationToken ct);

    /// <summary>تفاصيل payslip موظف واحد ضمن الدورة.</summary>
    Task<PayrollResult<PayslipResponse>> GetPayslipAsync(Guid tenantId, Guid runId, Guid employeeId, CancellationToken ct);
}

// ============== Service Implementation ==============

/// <summary>
/// تنفيذ خدمة Payroll. يتبع Clean Architecture:
/// - Domain (entities + calculators) ← تحتها
/// - Infrastructure (repositories) ← عبر interfaces
/// - HR module ← عبر interface فقط (لا coupling مباشر على Dapper repos)
/// - Finance ← عبر IJournalEntryService لإنشاء القيد المحاسبي
///
/// State machine:
/// Draft → Processing → Posted
///   ↓         ↓
/// Cancelled  Cancelled
/// </summary>
public sealed class PayrollService : IPayrollService
{
    private readonly IPayrollRepository _runs;
    private readonly ISalaryStructureRepository _structures;
    private readonly IEmployeeRepository _employees;
    private readonly ILibyaTaxCalculator _taxCalc;
    private readonly ISocialInsuranceCalculator _siCalc;
    private readonly IJournalEntryService _journalService;
    private readonly IAccountRepository _accounts;
    private readonly ERPSystem.Shared.SeedData.IDefaultAccountsHelper _defaultAccounts;
    private readonly ILogger<PayrollService> _logger;

    public PayrollService(
        IPayrollRepository runs,
        ISalaryStructureRepository structures,
        IEmployeeRepository employees,
        ILibyaTaxCalculator taxCalc,
        ISocialInsuranceCalculator siCalc,
        IJournalEntryService journalService,
        IAccountRepository accounts,
        ERPSystem.Shared.SeedData.IDefaultAccountsHelper defaultAccounts,
        ILogger<PayrollService> logger)
    {
        _runs = runs; _structures = structures; _employees = employees;
        _taxCalc = taxCalc; _siCalc = siCalc;
        _journalService = journalService; _accounts = accounts;
        _defaultAccounts = defaultAccounts; _logger = logger;
    }

    // ---------- ListRunsAsync ----------

    public async Task<PayrollResult<IReadOnlyList<PayrollRunResponse>>> ListRunsAsync(Guid tenantId, PayrollRunStatus? status, int skip, int take, CancellationToken ct)
    {
        var runs = await _runs.ListRunsAsync(tenantId, status, skip, take, ct);
        var itemsByRun = new Dictionary<Guid, int>();
        foreach (var r in runs)
        {
            var its = await _runs.GetItemsByRunAsync(r.Id, ct);
            itemsByRun[r.Id] = its.Count;
        }
        var result = runs.Select(r => MapRunToResponse(r, itemsByRun.TryGetValue(r.Id, out var n) ? n : 0)).ToList();
        return PayrollResult<IReadOnlyList<PayrollRunResponse>>.Ok(result);
    }

    // ---------- GetRunAsync ----------

    public async Task<PayrollResult<PayrollRunResponse>> GetRunAsync(Guid tenantId, Guid runId, CancellationToken ct)
    {
        var run = await _runs.GetRunByIdForTenantAsync(tenantId, runId, ct);
        if (run == null)
            return PayrollResult<PayrollRunResponse>.Fail("الدورة غير موجودة.", PayrollErrorCode.NotFound);
        var its = await _runs.GetItemsByRunAsync(runId, ct);
        return PayrollResult<PayrollRunResponse>.Ok(MapRunToResponse(run, its.Count));
    }

    // ---------- CreateRunAsync ----------

    public async Task<PayrollResult<PayrollRunResponse>> CreateRunAsync(Guid tenantId, Guid userId, CreatePayrollRunRequest req, CancellationToken ct)
    {
        // الـ validator يفحص PeriodEnd >= PeriodStart (لكن defense-in-depth هنا أيضاً).
        if (req.PeriodEnd < req.PeriodStart)
            return PayrollResult<PayrollRunResponse>.Fail("تاريخ النهاية يجب أن يكون >= البداية.", PayrollErrorCode.ValidationError);

        // التحقق من عدم وجود دورة متداخلة (Active) في نفس الفترة لنفس الـ tenant.
        var existing = await _runs.ListRunsAsync(tenantId, null, 0, 200, ct);
        var overlap = existing.FirstOrDefault(r =>
            r.Status != PayrollRunStatus.Cancelled &&
            r.Status != PayrollRunStatus.Posted &&
            !(r.PeriodEnd < req.PeriodStart || r.PeriodStart > req.PeriodEnd));
        if (overlap != null)
            return PayrollResult<PayrollRunResponse>.Fail(
                $"يوجد دورة رواتب متداخلة في الفترة ({overlap.PeriodStart:yyyy-MM-dd} → {overlap.PeriodEnd:yyyy-MM-dd}).",
                PayrollErrorCode.BusinessRuleViolation);

        var now = DateTime.UtcNow;
        var run = new PayrollRun
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            PeriodStart = req.PeriodStart.Date,
            PeriodEnd = req.PeriodEnd.Date,
            Status = PayrollRunStatus.Draft,
            Notes = req.Notes,
            CreatedAt = now, CreatedBy = userId, UpdatedAt = now, UpdatedBy = userId
        };
        await _runs.InsertRunAsync(run, ct);
        _logger.LogInformation("تم إنشاء دورة رواتب {RunId} للفترة {Start:yyyy-MM-dd} → {End:yyyy-MM-dd}",
            run.Id, run.PeriodStart, run.PeriodEnd);

        return PayrollResult<PayrollRunResponse>.Ok(MapRunToResponse(run, itemsCount: 0));
    }

    // ---------- ProcessRunAsync ----------

    public async Task<PayrollResult<PayrollRunResponse>> ProcessRunAsync(Guid tenantId, Guid userId, Guid runId, CancellationToken ct)
    {
        var run = await _runs.GetRunByIdForTenantAsync(tenantId, runId, ct);
        if (run == null)
            return PayrollResult<PayrollRunResponse>.Fail("الدورة غير موجودة.", PayrollErrorCode.NotFound);

        try
        {
            run.MarkProcessing(DateTime.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return PayrollResult<PayrollRunResponse>.Fail(ex.Message, PayrollErrorCode.InvalidStatusTransition);
        }

        // 1) جلب كل الموظفين النشطين للـ tenant (pagination-safe: max 1000 موظف).
        var employees = await _employees.ListAsync(tenantId, departmentId: null, includeInactive: false, skip: 0, take: 1000, ct);
        if (employees.Count == 0)
        {
            // نحدّث الحالة لـ Processing ونرجع — لا items لتوليدها.
            run.UpdatedBy = userId;
            await _runs.UpdateRunAsync(run, ct);
            _logger.LogWarning("الدورة {RunId} فارغة — لا موظفين نشطين", runId);
            return PayrollResult<PayrollRunResponse>.Ok(MapRunToResponse(run, itemsCount: 0));
        }

        // 2) جلب هيكل الرواتب الافتراضي (إن وُجد) لاستخدام earning/deduction defaults.
        var defaultStructure = await _runs.GetStructureByCodeAsync(tenantId, "DEFAULT", ct);
        IReadOnlyList<SalaryStructureLine>? structureLines = defaultStructure != null
            ? await _structures.GetLinesAsync(defaultStructure.Id, ct)
            : null;

        decimal totalGross = 0m;
        decimal totalNet = 0m;
        var created = 0;

        // 3) لكل موظف نشط: نولّد PayrollItem + PayslipComponents.
        foreach (var emp in employees)
        {
            // موظف مُنهى خدمته قبل بداية الفترة → نتجاهله.
            if (emp.TerminationDate.HasValue && emp.TerminationDate.Value.Date < run.PeriodStart.Date)
                continue;
            // موظف لم يُعيَّن بعد في بداية الفترة → نتجاهله (لا راتب قبل التعيين).
            if (emp.HireDate.Date > run.PeriodEnd.Date)
                continue;

            var baseSalary = emp.BaseSalary;
            var gross = baseSalary; // Basic only في الـ MVP — البدلات تأتي من SalaryStructure (إن وُجدت).

            var tax = _taxCalc.CalculateMonthlyTax(gross);
            var siEmp = _siCalc.EmployeeContribution(gross);
            var net = Math.Max(0m, gross - tax - siEmp);

            // Payslip components: الأساسي + الضريبة + التأمينات (إن وُجد هيكل، نضيف بقية السطور).
            var components = new List<PayslipComponent>();
            var sort = 0;
            components.Add(NewComponent(tenantId, SalaryComponentType.Earning, "الراتب الأساسي", baseSalary, sort++));
            if (structureLines != null)
            {
                foreach (var ln in structureLines.Where(l => l.Type == SalaryComponentType.Earning && !string.Equals(l.Name, "الراتب الأساسي", StringComparison.OrdinalIgnoreCase)))
                {
                    components.Add(NewComponent(tenantId, SalaryComponentType.Earning, ln.Name, ln.Amount, sort++));
                    gross += ln.Amount; // البدلات تُضاف للـ Gross
                }
                // إعادة احتساب tax/net بعد إضافة البدلات.
                if (gross != baseSalary)
                {
                    tax = _taxCalc.CalculateMonthlyTax(gross);
                    siEmp = _siCalc.EmployeeContribution(gross);
                    net = Math.Max(0m, gross - tax - siEmp);
                }
                // إضافة deductions من الهيكل.
                foreach (var ln in structureLines.Where(l => l.Type == SalaryComponentType.Deduction))
                {
                    components.Add(NewComponent(tenantId, SalaryComponentType.Deduction, ln.Name, ln.Amount, sort++));
                    net -= ln.Amount;
                }
            }
            components.Add(NewComponent(tenantId, SalaryComponentType.Deduction, "ضريبة الدخل (GDT)", tax, sort++));
            components.Add(NewComponent(tenantId, SalaryComponentType.Deduction, "التأمينات الاجتماعية", siEmp, sort++));

            net = Math.Max(0m, net);

            var item = new PayrollItem
            {
                Id = Guid.NewGuid(), TenantId = tenantId, PayrollRunId = run.Id, EmployeeId = emp.Id,
                BaseSalary = baseSalary, GrossSalary = gross, TaxAmount = tax,
                SocialInsuranceEmployee = siEmp, NetSalary = net,
                Status = PayrollItemStatus.Processed, PaymentDays = 30,
                CreatedAt = DateTime.UtcNow, CreatedBy = userId, UpdatedAt = DateTime.UtcNow
            };

            // ربط الـ PayslipComponents بالـ PayrollItem (FK fk_payslip_components_item)
            foreach (var c in components) c.PayrollItemId = item.Id;

            await _runs.AddItemAsync(item, components, ct);

            totalGross += gross;
            totalNet += net;
            created++;
        }

        run.TotalGross = totalGross;
        run.TotalNet = totalNet;
        run.UpdatedBy = userId;
        await _runs.UpdateRunAsync(run, ct);

        _logger.LogInformation("تمت معالجة الدورة {RunId}: {Count} payslip، TotalGross={Gross}, TotalNet={Net}",
            run.Id, created, totalGross, totalNet);
        return PayrollResult<PayrollRunResponse>.Ok(MapRunToResponse(run, itemsCount: created));
    }

    // ---------- PostRunAsync ----------

    public async Task<PayrollResult<PayrollRunResponse>> PostRunAsync(Guid tenantId, Guid userId, Guid runId, CancellationToken ct)
    {
        var run = await _runs.GetRunByIdForTenantAsync(tenantId, runId, ct);
        if (run == null)
            return PayrollResult<PayrollRunResponse>.Fail("الدورة غير موجودة.", PayrollErrorCode.NotFound);

        try
        {
            run.MarkPosted(DateTime.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return PayrollResult<PayrollRunResponse>.Fail(ex.Message, PayrollErrorCode.InvalidStatusTransition);
        }

        // 1) جلب payslips الدورة لاحتساب إجمالي Net.
        var items = await _runs.GetItemsByRunAsync(run.Id, ct);
        if (items.Count == 0)
            return PayrollResult<PayrollRunResponse>.Fail("لا توجد payslips لترحيلها. عالج الدورة أولاً.", PayrollErrorCode.BusinessRuleViolation);

        var totalNet = items.Sum(i => i.NetSalary);
        if (totalNet <= 0)
            return PayrollResult<PayrollRunResponse>.Fail("إجمالي صافي الرواتب صفر — لا يمكن الترحيل.", PayrollErrorCode.BusinessRuleViolation);

        // 2) جلب الحسابات المطلوبة (Salary Expense + Cash) حسب الكود الفعلي في CoA الافتراضي.
        //    CoA الافتراضي (DefaultCoASeed): 4200 G&A Expenses (Salary Expense proxy) + 1210 Cash.
        //    الـ gap-analysis ذكر 5500/1100 لكن الكودين غير postable في الـ CoA الفعلي.
        // v1.0.30: self-heal — أضف الحسابات الناقصة تلقائياً
        await _defaultAccounts.EnsureDefaultAsync(tenantId, ct);
        var salaryAccount = await _accounts.GetByCodeAsync(tenantId, "4200", ct)
            ?? await _accounts.GetByCodeAsync(tenantId, "5500", ct);
        var cashAccount = await _accounts.GetByCodeAsync(tenantId, "1210", ct)
            ?? await _accounts.GetByCodeAsync(tenantId, "1100", ct);

        if (salaryAccount == null || !salaryAccount.IsPostable)
            return PayrollResult<PayrollRunResponse>.Fail("حساب مصروف الرواتب (4200) غير موجود أو غير قابل للترحيل.", PayrollErrorCode.BusinessRuleViolation);
        if (cashAccount == null || !cashAccount.IsPostable)
            return PayrollResult<PayrollRunResponse>.Fail("حساب النقدية (1210) غير موجود أو غير قابل للترحيل.", PayrollErrorCode.BusinessRuleViolation);

        // 3) إنشاء القيد المحاسبي: Dr Salary Expense / Cr Cash (net فقط — الضريبة والتأمينات تُرحَّل بقيود منفصلة في Phase 4.5).
        var jeReq = new PostJournalEntryRequest
        {
            EntryDate = run.PeriodEnd,
            Description = $"Payroll run {run.Id} — {run.PeriodStart:yyyy-MM-dd} → {run.PeriodEnd:yyyy-MM-dd}",
            Reference = $"PAYROLL/{run.Id}",
            Lines = new List<PostJournalLineRequest>
            {
                new() { AccountId = salaryAccount.Id, Debit = totalNet, Credit = 0m, Description = "Salary Expense" },
                new() { AccountId = cashAccount.Id,   Debit = 0m,     Credit = totalNet, Description = "Cash paid" }
            }
        };
        var draftRes = await _journalService.CreateDraftAsync(tenantId, userId, jeReq, ct);
        if (!draftRes.Succeeded)
            return PayrollResult<PayrollRunResponse>.Fail($"فشل إنشاء القيد: {draftRes.Error}", PayrollErrorCode.Internal);

        var postRes = await _journalService.PostAsync(tenantId, userId, draftRes.Value!.Id, ct);
        if (!postRes.Succeeded)
            return PayrollResult<PayrollRunResponse>.Fail($"فشل ترحيل القيد: {postRes.Error}", PayrollErrorCode.Internal);

        // 4) حفظ حالة Posted + reference القيد في الملاحظات.
        run.Notes = (run.Notes ?? string.Empty) + $" | JE:{draftRes.Value.EntryNumber}";
        run.UpdatedBy = userId;
        await _runs.UpdateRunAsync(run, ct);

        _logger.LogInformation("تم ترحيل الدورة {RunId}: JournalEntry {Number} (Net={Net})",
            run.Id, draftRes.Value.EntryNumber, totalNet);

        return PayrollResult<PayrollRunResponse>.Ok(MapRunToResponse(run, itemsCount: items.Count));
    }

    // ---------- GetItemsAsync ----------

    public async Task<PayrollResult<IReadOnlyList<PayslipResponse>>> GetItemsAsync(Guid tenantId, Guid runId, CancellationToken ct)
    {
        var run = await _runs.GetRunByIdForTenantAsync(tenantId, runId, ct);
        if (run == null)
            return PayrollResult<IReadOnlyList<PayslipResponse>>.Fail("الدورة غير موجودة.", PayrollErrorCode.NotFound);

        var items = await _runs.GetItemsByRunAsync(runId, ct);
        var result = new List<PayslipResponse>(items.Count);
        foreach (var i in items)
        {
            var emp = await _employees.GetByIdAsync(i.EmployeeId, ct);
            var components = await _runs.GetComponentsByItemAsync(i.Id, ct);
            var resp = MapItemToResponse(i, emp);
            resp.Components = components.Select(c => new PayslipComponentResponse
            {
                Id = c.Id,
                ComponentType = c.ComponentType,
                Name = c.Name,
                Amount = c.Amount,
                SortOrder = c.SortOrder
            }).ToList();
            result.Add(resp);
        }
        return PayrollResult<IReadOnlyList<PayslipResponse>>.Ok(result);
    }

    // ---------- GetPayslipAsync ----------

    public async Task<PayrollResult<PayslipResponse>> GetPayslipAsync(Guid tenantId, Guid runId, Guid employeeId, CancellationToken ct)
    {
        var run = await _runs.GetRunByIdForTenantAsync(tenantId, runId, ct);
        if (run == null)
            return PayrollResult<PayslipResponse>.Fail("الدورة غير موجودة.", PayrollErrorCode.NotFound);

        var items = await _runs.GetItemsByRunAsync(runId, ct);
        var item = items.FirstOrDefault(i => i.EmployeeId == employeeId);
        if (item == null)
            return PayrollResult<PayslipResponse>.Fail("Payslip غير موجود لهذا الموظف.", PayrollErrorCode.NotFound);

        var emp = await _employees.GetByIdAsync(employeeId, ct);
        var components = await _runs.GetComponentsByItemAsync(item.Id, ct);
        var resp = MapItemToResponse(item, emp);
        resp.Components = components.Select(c => new PayslipComponentResponse
        {
            Id = c.Id,
            ComponentType = c.ComponentType,
            Name = c.Name,
            Amount = c.Amount,
            SortOrder = c.SortOrder
        }).ToList();
        return PayrollResult<PayslipResponse>.Ok(resp);
    }

    // ============== Helpers ==============

    private static PayslipComponent NewComponent(Guid tenantId, SalaryComponentType type, string name, decimal amount, int sortOrder)
        => new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ComponentType = type,
            Name = name, Amount = amount, SortOrder = sortOrder
        };

    private static PayrollRunResponse MapRunToResponse(PayrollRun r, int itemsCount)
        => new()
        {
            Id = r.Id, TenantId = r.TenantId,
            PeriodStart = r.PeriodStart, PeriodEnd = r.PeriodEnd,
            Status = r.Status, TotalGross = r.TotalGross, TotalNet = r.TotalNet,
            ProcessedAt = r.ProcessedAt, PostedAt = r.PostedAt,
            Notes = r.Notes, CreatedAt = r.CreatedAt, ItemsCount = itemsCount
        };

    private static PayslipResponse MapItemToResponse(PayrollItem i, Employee? emp)
        => new()
        {
            Id = i.Id, TenantId = i.TenantId, PayrollRunId = i.PayrollRunId, EmployeeId = i.EmployeeId,
            EmployeeNumber = emp?.EmployeeNumber, EmployeeName = emp?.FullName,
            BaseSalary = i.BaseSalary, GrossSalary = i.GrossSalary, TaxAmount = i.TaxAmount,
            SocialInsuranceEmployee = i.SocialInsuranceEmployee, NetSalary = i.NetSalary,
            Status = i.Status, PaymentDays = i.PaymentDays, Notes = i.Notes
        };
}