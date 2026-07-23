using ERPSystem.Shared.SeedData;

namespace ERPSystem.Tests.Fakes;

// v1.0.30: fake IDefaultAccountsHelper (no-op) for unit tests
internal class FakeDefaultAccountsHelper : IDefaultAccountsHelper
{
    public Task<EnsureResult> EnsureDefaultAsync(Guid tenantId, CancellationToken ct) =>
        Task.FromResult(new EnsureResult { CreatedAny = false });
}
