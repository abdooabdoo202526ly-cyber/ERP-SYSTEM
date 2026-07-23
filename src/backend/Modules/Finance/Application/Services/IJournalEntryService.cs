namespace ERPSystem.Modules.Finance.Application.Services;

using ERPSystem.Modules.Finance.Entities;

public interface IJournalEntryService
{
    /// <summary>إنشاء قيد جديد (Draft). يتحقق من توازن المدين/الدائن ويتأكد أن الحسابات postable.</summary>
    Task<FinanceResult<JournalEntryResponse>> CreateDraftAsync(Guid tenantId, Guid userId, PostJournalEntryRequest request, CancellationToken ct);

    /// <summary>ترحيل القيد (Draft -> Posted) — يجعله يؤثر على General Ledger.</summary>
    Task<FinanceResult<JournalEntryResponse>> PostAsync(Guid tenantId, Guid userId, Guid entryId, CancellationToken ct);

    /// <summary>تفاصيل قيد مع سطوره</summary>
    Task<FinanceResult<JournalEntryResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// <summary>قائمة القيود (مع pagination و filter)</summary>
    Task<FinanceResult<IReadOnlyList<JournalEntryResponse>>> ListAsync(Guid tenantId, DateTime? from, DateTime? to, JournalEntryStatus? status, int skip, int take, CancellationToken ct);
}
