using ERPSystem.Modules.Companies.Application.Services;
using ERPSystem.Modules.Companies.Entities;

namespace ERPSystem.Tests.Fakes;

// v1.0.25: shared fake ICompanyService (was internal in ProjectServiceTests)
internal class FakeCompanyService : ICompanyService
{
    private readonly Dictionary<Guid, Company> _items = new();
    public List<Company> Created { get; } = new();
    public Task<CompanyResult<Company>> CreateHoldingAsync(Guid tenantId, string code, string name, string legalName, string baseCurrency, CancellationToken ct)
    {
        var c = new Company { Id = Guid.NewGuid(), TenantId = tenantId, Code = code, Name = name, LegalName = legalName, IsGroup = true, BaseCurrency = baseCurrency, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _items[c.Id] = c; Created.Add(c);
        return Task.FromResult(CompanyResult<Company>.Ok(c));
    }
    public Task<CompanyResult<Company>> AddSubsidiaryAsync(Guid tenantId, Guid parentCompanyId, string code, string name, string? legalName, CancellationToken ct) =>
        Task.FromResult(CompanyResult<Company>.Fail("not impl", CompanyErrorCode.Internal));
    public Task<CompanyResult<Company>> UpdateAsync(Guid tenantId, Guid id, string? name, string? legalName, string? taxId, string? phone, string? email, string? address, bool? isActive, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var c) ? CompanyResult<Company>.Ok(c) : CompanyResult<Company>.Fail("not found", CompanyErrorCode.NotFound));
    public Task<CompanyResult<Company>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var c) ? CompanyResult<Company>.Ok(c) : CompanyResult<Company>.Fail("not found", CompanyErrorCode.NotFound));
    public Task<CompanyResult<IReadOnlyList<Company>>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct) =>
        Task.FromResult(CompanyResult<IReadOnlyList<Company>>.Ok(_items.Values.ToList()));
    public Task<CompanyResult<IReadOnlyList<Company>>> GetSubsidiariesAsync(Guid parentCompanyId, CancellationToken ct) =>
        Task.FromResult(CompanyResult<IReadOnlyList<Company>>.Ok(new List<Company>()));
    public Task<CompanyResult<CompanyTreeNode>> GetTreeAsync(Guid tenantId, CancellationToken ct) =>
        Task.FromResult(CompanyResult<CompanyTreeNode>.Ok(new CompanyTreeNode()));
    public Task<CompanyResult<bool>> DeactivateAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        Task.FromResult(CompanyResult<bool>.Ok(true));
    public Task<Guid> EnsureDefaultHoldingAsync(Guid tenantId, string tenantName, string baseCurrency, CancellationToken ct)
    {
        var c = new Company { Id = Guid.NewGuid(), TenantId = tenantId, Code = "DEFAULT", Name = tenantName, LegalName = tenantName, IsGroup = true, BaseCurrency = baseCurrency, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _items[c.Id] = c; Created.Add(c);
        return Task.FromResult(c.Id);
    }
}
