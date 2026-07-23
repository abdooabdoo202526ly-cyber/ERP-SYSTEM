using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Finance.Infrastructure;

namespace ERPSystem.Modules.Finance.Application.Services;

public sealed class ChartOfAccountsService : IChartOfAccountsService
{
    private readonly IAccountRepository _accounts;
    private readonly ILogger<ChartOfAccountsService> _logger;

    public ChartOfAccountsService(IAccountRepository accounts, ILogger<ChartOfAccountsService> logger)
    {
        _accounts = accounts;
        _logger = logger;
    }

    public async Task<FinanceResult<AccountResponse>> CreateAsync(Guid tenantId, CreateAccountRequest request, CancellationToken ct)
    {
        if (await _accounts.GetByCodeAsync(tenantId, request.Code, ct) != null)
        {
            return FinanceResult<AccountResponse>.Fail(
                $"كود الحساب '{request.Code}' مستخدم بالفعل داخل هذا المستأجر.",
                FinanceErrorCode.AlreadyExists);
        }

        Guid? parentId = null;
        if (request.ParentAccountId.HasValue)
        {
            var parent = await _accounts.GetByIdAsync(request.ParentAccountId.Value, ct);
            if (parent == null || parent.TenantId != tenantId)
            {
                return FinanceResult<AccountResponse>.Fail("الحساب الأب غير موجود.", FinanceErrorCode.NotFound);
            }
            parentId = parent.Id;
        }

        var now = DateTime.UtcNow;
        var acc = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description,
            Type = request.Type,
            NormalBalance = request.NormalBalance ?? (request.Type switch
            {
                AccountType.Asset or AccountType.Expense => NormalBalance.Debit,
                _ => NormalBalance.Credit
            }),
            ParentAccountId = parentId,
            IsPostable = request.IsPostable,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _accounts.InsertAsync(acc, ct);
        _logger.LogInformation("تم إنشاء حساب جديد {Code} للمستأجر {TenantId}", acc.Code, tenantId);
        return FinanceResult<AccountResponse>.Ok(MapToResponse(acc));
    }

    public async Task<FinanceResult<AccountResponse>> UpdateAsync(Guid tenantId, Guid id, UpdateAccountRequest request, CancellationToken ct)
    {
        var acc = await _accounts.GetByIdAsync(id, ct);
        if (acc == null || acc.TenantId != tenantId)
        {
            return FinanceResult<AccountResponse>.Fail("الحساب غير موجود.", FinanceErrorCode.NotFound);
        }

        // Code لا نسمح بتعديله (المعرّف المحاسبي)
        // v1.0.24: Type و NormalBalance أصبحا قابلين للتعديل
        if (request.Name != null) acc.Name = request.Name.Trim();
        if (request.Description != null) acc.Description = request.Description;
        if (request.Type.HasValue) acc.Type = request.Type.Value;
        if (request.NormalBalance.HasValue) acc.NormalBalance = request.NormalBalance.Value;
        if (request.ParentAccountId.HasValue)
        {
            if (request.ParentAccountId.Value == id)
                return FinanceResult<AccountResponse>.Fail("لا يمكن أن يكون الحساب أباً لنفسه.", FinanceErrorCode.ValidationError);
            var parent = await _accounts.GetByIdAsync(request.ParentAccountId.Value, ct);
            if (parent == null || parent.TenantId != tenantId)
                return FinanceResult<AccountResponse>.Fail("الحساب الأب غير موجود.", FinanceErrorCode.NotFound);
            // Cycle detection: تأكد أن الأب الجديد ليس ابناً لهذا الحساب
            var descendants = await CollectDescendantIdsAsync(id, ct);
            if (descendants.Contains(request.ParentAccountId.Value))
                return FinanceResult<AccountResponse>.Fail("سيُسبّب هذا تعديل حلقة دائرية في شجرة الحسابات.", FinanceErrorCode.ValidationError);
            acc.ParentAccountId = request.ParentAccountId;
        }
        if (request.IsPostable.HasValue) acc.IsPostable = request.IsPostable.Value;
        if (request.IsActive.HasValue) acc.IsActive = request.IsActive.Value;
        acc.UpdatedAt = DateTime.UtcNow;

        await _accounts.UpdateAsync(acc, ct);
        _logger.LogInformation("تم تعديل الحساب {Code} ({Name}) للمستأجر {TenantId}", acc.Code, acc.Name, tenantId);
        return FinanceResult<AccountResponse>.Ok(MapToResponse(acc));
    }

    private async Task<HashSet<Guid>> CollectDescendantIdsAsync(Guid accountId, CancellationToken ct)
    {
        var result = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(accountId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var children = await _accounts.ListChildrenAsync(current, ct);
            foreach (var c in children)
            {
                if (result.Add(c.Id)) queue.Enqueue(c.Id);
            }
        }
        return result;
    }

    public async Task<FinanceResult<AccountResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var acc = await _accounts.GetByIdAsync(id, ct);
        if (acc == null || acc.TenantId != tenantId)
        {
            return FinanceResult<AccountResponse>.Fail("الحساب غير موجود.", FinanceErrorCode.NotFound);
        }
        return FinanceResult<AccountResponse>.Ok(MapToResponse(acc));
    }

    public async Task<FinanceResult<AccountResponse>> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct)
    {
        var acc = await _accounts.GetByCodeAsync(tenantId, code, ct);
        if (acc == null) return FinanceResult<AccountResponse>.Fail("الحساب غير موجود.", FinanceErrorCode.NotFound);
        return FinanceResult<AccountResponse>.Ok(MapToResponse(acc));
    }

    public async Task<FinanceResult<IReadOnlyList<AccountResponse>>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct)
    {
        var list = await _accounts.ListAsync(tenantId, includeInactive, ct);
        return FinanceResult<IReadOnlyList<AccountResponse>>.Ok(list.Select(MapToResponse).ToList());
    }

    public async Task<FinanceResult<bool>> DeleteAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var acc = await _accounts.GetByIdAsync(id, ct);
        if (acc == null || acc.TenantId != tenantId)
        {
            return FinanceResult<bool>.Fail("الحساب غير موجود.", FinanceErrorCode.NotFound);
        }

        // ممنوع الحذف إن كان عليه حركات
        var postings = await _accounts.CountPostingsAsync(id, ct);
        if (postings > 0)
        {
            return FinanceResult<bool>.Fail("لا يمكن حذف حساب عليه حركات — استخدم IsActive=false بدلاً من ذلك.",
                FinanceErrorCode.InUse);
        }

        // ممنوع الحذف إن كان له حسابات فرعية
        var children = await _accounts.ListChildrenAsync(id, ct);
        if (children.Count > 0)
        {
            return FinanceResult<bool>.Fail("لا يمكن حذف حساب له حسابات فرعية.", FinanceErrorCode.HasChildren);
        }

        // soft-delete عبر IsActive=false (لا نحذف فعلياً للحفاظ على الـ audit trail)
        acc.IsActive = false;
        acc.UpdatedAt = DateTime.UtcNow;
        await _accounts.UpdateAsync(acc, ct);
        return FinanceResult<bool>.Ok(true);
    }

    private static AccountResponse MapToResponse(Account a) => new()
    {
        Id = a.Id,
        TenantId = a.TenantId,
        Code = a.Code,
        Name = a.Name,
        Description = a.Description,
        Type = a.Type,
        NormalBalance = a.NormalBalance,
        ParentAccountId = a.ParentAccountId,
        IsPostable = a.IsPostable,
        IsActive = a.IsActive,
    };
}
