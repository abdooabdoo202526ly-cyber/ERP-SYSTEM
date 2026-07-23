using ERPSystem.Modules.Finance.Application;

namespace ERPSystem.Modules.Finance.Application.Services;

public interface IChartOfAccountsService
{
    Task<FinanceResult<AccountResponse>> CreateAsync(Guid tenantId, CreateAccountRequest request, CancellationToken ct);
    Task<FinanceResult<AccountResponse>> UpdateAsync(Guid tenantId, Guid id, UpdateAccountRequest request, CancellationToken ct);
    Task<FinanceResult<AccountResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<FinanceResult<AccountResponse>> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct);
    Task<FinanceResult<IReadOnlyList<AccountResponse>>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct);
    Task<FinanceResult<bool>> DeleteAsync(Guid tenantId, Guid id, CancellationToken ct);
}

/// <summary>نتيجة موحّدة لعمليات Finance</summary>
public sealed class FinanceResult<T>
{
    public bool Succeeded { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public FinanceErrorCode? ErrorCode { get; init; }

    public static FinanceResult<T> Ok(T value) => new() { Succeeded = true, Value = value };
    public static FinanceResult<T> Fail(string error, FinanceErrorCode code) => new() { Succeeded = false, Error = error, ErrorCode = code };
}

public enum FinanceErrorCode
{
    NotFound,
    AlreadyExists,
    ValidationError,
    InUse,        // الحساب عليه حركات — لا يُحذف
    HasChildren,  // الحساب له حسابات فرعية — لا يُحذف
    Unbalanced,   // القيد غير متوازن (Σ debit != Σ credit)
    InvalidAccount, // الحساب غير postable
    TenantMismatch,
    Internal
}
