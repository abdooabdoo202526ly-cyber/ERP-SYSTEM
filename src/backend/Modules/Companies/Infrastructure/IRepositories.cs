using ERPSystem.Modules.Companies.Entities;

namespace ERPSystem.Modules.Companies.Infrastructure;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Company?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct);
    Task<IReadOnlyList<Company>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct);
    Task<IReadOnlyList<Company>> ListSubsidiariesAsync(Guid parentCompanyId, CancellationToken ct);
    Task<Guid?> GetHoldingCompanyIdAsync(Guid tenantId, CancellationToken ct);
    Task InsertAsync(Company company, CancellationToken ct);
    Task UpdateAsync(Company company, CancellationToken ct);
}

public interface ICostCenterRepository
{
    Task<CostCenter?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<CostCenter?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct);
    Task<IReadOnlyList<CostCenter>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct); // v1.0.34
    Task<IReadOnlyList<CostCenter>> ListAsync(Guid tenantId, Guid? companyId, CostCenterType? type, bool includeInactive, CancellationToken ct);
    Task<IReadOnlyList<CostCenter>> ListChildrenAsync(Guid parentId, CancellationToken ct);
    Task InsertAsync(CostCenter cc, CancellationToken ct);
    Task UpdateAsync(CostCenter cc, CancellationToken ct);
}
