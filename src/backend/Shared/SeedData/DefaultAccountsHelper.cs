using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Application.Services;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Finance.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Shared.SeedData;

/// <summary>
/// v1.0.30: Helper لتأكيد وجود الحسابات الافتراضية (1210/1230/1110/2120) قبل الترحيل.
/// يُستدعى من ReceiptService / PaymentService / SalesInvoiceService / PayrollService
/// كـ "self-healing" — إذا الـ tenant لا يحوي CoA (مثلاً الـ seeder فشل أو الـ data مهاجرة)،
/// نضيف الحسابات الناقصة فقط (لا نـ overwrite).
/// </summary>
public interface IDefaultAccountsHelper
{
    /// <summary>يتأكد من وجود (Cash, AR, AP, Bank). Idempotent — safe to call multiple times.</summary>
    /// <returns>true إذا تمت إضافة حساب واحد على الأقل، false إذا كلها موجودة.</returns>
    Task<EnsureResult> EnsureDefaultAsync(Guid tenantId, CancellationToken ct);
}

public sealed class EnsureResult
{
    public bool CreatedAny { get; init; }
    public List<string> Created { get; init; } = new();
    public List<string> AlreadyExisted { get; init; } = new();
}

public sealed class DefaultAccountsHelper : IDefaultAccountsHelper
{
    private readonly IAccountRepository _accounts;
    private readonly ILogger<DefaultAccountsHelper> _logger;

    public DefaultAccountsHelper(IAccountRepository accounts, ILogger<DefaultAccountsHelper> logger)
    {
        _accounts = accounts;
        _logger = logger;
    }

    public async Task<EnsureResult> EnsureDefaultAsync(Guid tenantId, CancellationToken ct)
    {
        var created = new List<string>();
        var existed = new List<string>();

        // (code, name, type, parentCode, isPostable, isControl)
        var defaults = new (string Code, string Name, AccountType Type, string? Parent, bool Postable, bool Control)[]
        {
            // البنوك والنقدية
            ("1110", "البنك", AccountType.Asset, "1100", true, false),
            ("1210", "النقدية", AccountType.Asset, "1200", true, false),
            // الذمم
            ("1230", "ذمم مدينة (عملاء خارجيين)", AccountType.Asset, "1200", true, false),
            ("2120", "ذمم دائنة (موردين)", AccountType.Liability, "2100", true, false),
            // الإيرادات
            ("4110", "إيرادات المبيعات", AccountType.Revenue, "4100", true, false),
            // المصروفات
            ("5110", "تكلفة المبيعات", AccountType.Expense, "5100", true, false),
            ("5500", "مصروف الرواتب", AccountType.Expense, "5500", true, false),
        };

        foreach (var (code, name, type, parentCode, isPostable, isControl) in defaults)
        {
            var existing = await _accounts.GetByCodeAsync(tenantId, code, ct);
            if (existing != null)
            {
                existed.Add(code);
                continue;
            }
            // resolve parent if any
            Guid? parentId = null;
            if (!string.IsNullOrEmpty(parentCode))
            {
                var parent = await _accounts.GetByCodeAsync(tenantId, parentCode, ct);
                if (parent != null) parentId = parent.Id;
            }
            var account = new Account
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = name,
                Type = type,
                ParentAccountId = parentId,
                IsPostable = isPostable,
                IsActive = true,
                NormalBalance = type == AccountType.Asset || type == AccountType.Expense ? NormalBalance.Debit : NormalBalance.Credit,
                CreatedAt = DateTime.UtcNow,
            };
            try
            {
                await _accounts.InsertAsync(account, ct);
                created.Add(code);
                _logger.LogInformation("DefaultAccountsHelper: created account {Code} ({Name}) for tenant {TenantId}", code, name, tenantId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DefaultAccountsHelper: failed to create account {Code} for tenant {TenantId}", code, tenantId);
            }
        }

        return new EnsureResult
        {
            CreatedAny = created.Count > 0,
            Created = created,
            AlreadyExisted = existed,
        };
    }
}
