using ERPSystem.Modules.Companies.Entities;
using ERPSystem.Modules.Companies.Infrastructure;

namespace ERPSystem.Modules.Companies.Application.Services;

public sealed class CreateCostCenterRequest
{
    public Guid? CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CostCenterType Type { get; set; }
    public Guid? ParentId { get; set; }
    public decimal? BudgetAmount { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Sku { get; set; }
    public string? Location { get; set; }
    public string? ActivityCategory { get; set; }
}

public sealed class CostCenterBudgetStatus
{
    public Guid CostCenterId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal? BudgetAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal RemainingAmount => (BudgetAmount ?? 0) - SpentAmount;
    public decimal UtilizationPercent => BudgetAmount is { } b && b > 0 ? (SpentAmount / b) * 100 : 0;
}

public interface ICostCenterService
{
    Task<CostCenterResult<CostCenter>> CreateAsync(Guid tenantId, CreateCostCenterRequest req, CancellationToken ct);
    Task<CostCenterResult<CostCenter>> UpdateAsync(Guid tenantId, Guid id, CreateCostCenterRequest req, CancellationToken ct);
    Task<CostCenterResult<CostCenter>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<CostCenterResult<IReadOnlyList<CostCenter>>> ListAsync(Guid tenantId, Guid? companyId, CostCenterType? type, bool includeInactive, CancellationToken ct);
    Task<CostCenterResult<IReadOnlyList<CostCenter>>> GetChildrenAsync(Guid parentId, CancellationToken ct);
    Task<CostCenterResult<CostCenterBudgetStatus>> GetBudgetStatusAsync(Guid tenantId, Guid costCenterId, DateTime? asOf, CancellationToken ct);
    Task<CostCenterResult<bool>> DeactivateAsync(Guid tenantId, Guid id, CancellationToken ct);
}

public sealed class CostCenterService : ICostCenterService
{
    private readonly ICostCenterRepository _repo;
    public CostCenterService(ICostCenterRepository r) => _repo = r;
    public async Task<CostCenterResult<CostCenter>> CreateAsync(Guid tenantId, CreateCostCenterRequest req, CancellationToken ct)
    {
        if (await _repo.GetByCodeAsync(tenantId, req.Code, ct) != null) return CostCenterResult<CostCenter>.Fail("كود مستخدم.", CostCenterErrorCode.AlreadyExists);
        var now = DateTime.UtcNow;
        var cc = new CostCenter { Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = req.CompanyId, Code = req.Code.Trim(), Name = req.Name.Trim(), Type = req.Type, ParentId = req.ParentId, BudgetAmount = req.BudgetAmount, StartDate = req.StartDate, EndDate = req.EndDate, Sku = req.Sku, Location = req.Location, ActivityCategory = req.ActivityCategory, IsActive = true, CreatedAt = now, UpdatedAt = now };
        await _repo.InsertAsync(cc, ct);
        return CostCenterResult<CostCenter>.Ok(cc);
    }
    public async Task<CostCenterResult<CostCenter>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var cc = await _repo.GetByIdAsync(id, ct);
        if (cc == null || cc.TenantId != tenantId) return CostCenterResult<CostCenter>.Fail("غير موجود.", CostCenterErrorCode.NotFound);
        return CostCenterResult<CostCenter>.Ok(cc);
    }
    public async Task<CostCenterResult<IReadOnlyList<CostCenter>>> ListAsync(Guid tenantId, Guid? companyId, CostCenterType? type, bool includeInactive, CancellationToken ct) =>
        CostCenterResult<IReadOnlyList<CostCenter>>.Ok(await _repo.ListAsync(tenantId, companyId, type, includeInactive, ct));
    public async Task<CostCenterResult<IReadOnlyList<CostCenter>>> GetChildrenAsync(Guid parentId, CancellationToken ct) =>
        CostCenterResult<IReadOnlyList<CostCenter>>.Ok(await _repo.ListChildrenAsync(parentId, ct));
    public async Task<CostCenterResult<CostCenterBudgetStatus>> GetBudgetStatusAsync(Guid tenantId, Guid costCenterId, DateTime? asOf, CancellationToken ct)
    {
        var cc = await _repo.GetByIdAsync(costCenterId, ct);
        if (cc == null || cc.TenantId != tenantId) return CostCenterResult<CostCenterBudgetStatus>.Fail("غير موجود.", CostCenterErrorCode.NotFound);
        return CostCenterResult<CostCenterBudgetStatus>.Ok(new CostCenterBudgetStatus { CostCenterId = cc.Id, Code = cc.Code, Name = cc.Name, BudgetAmount = cc.BudgetAmount, SpentAmount = 0 });
    }
    public async Task<CostCenterResult<bool>> DeactivateAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var cc = await _repo.GetByIdAsync(id, ct);
        if (cc == null || cc.TenantId != tenantId) return CostCenterResult<bool>.Fail("غير موجود.", CostCenterErrorCode.NotFound);
        cc.IsActive = false; cc.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(cc, ct);
        return CostCenterResult<bool>.Ok(true);
    }

    public async Task<CostCenterResult<CostCenter>> UpdateAsync(Guid tenantId, Guid id, CreateCostCenterRequest req, CancellationToken ct)
    {
        var cc = await _repo.GetByIdAsync(id, ct);
        if (cc == null || cc.TenantId != tenantId) return CostCenterResult<CostCenter>.Fail("غير موجود.", CostCenterErrorCode.NotFound);
        // Code cannot be changed (FKs in journal_lines depend on it)
        if (!string.IsNullOrEmpty(req.Name)) cc.Name = req.Name.Trim();
        if (req.Type != default) cc.Type = req.Type;
        if (req.ParentId.HasValue) cc.ParentId = req.ParentId == Guid.Empty ? null : req.ParentId;
        if (req.BudgetAmount.HasValue) cc.BudgetAmount = req.BudgetAmount;
        if (req.StartDate.HasValue) cc.StartDate = req.StartDate;
        if (req.EndDate.HasValue) cc.EndDate = req.EndDate;
        if (req.Sku != null) cc.Sku = req.Sku;
        if (req.Location != null) cc.Location = req.Location;
        if (req.ActivityCategory != null) cc.ActivityCategory = req.ActivityCategory;
        cc.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(cc, ct);
        return CostCenterResult<CostCenter>.Ok(cc);
    }
}

public sealed class CostCenterResult<T> { public bool Succeeded { get; init; } public T? Value { get; init; } public string? Error { get; init; } public CostCenterErrorCode? ErrorCode { get; init; } public static CostCenterResult<T> Ok(T v) => new() { Succeeded = true, Value = v }; public static CostCenterResult<T> Fail(string e, CostCenterErrorCode c) => new() { Succeeded = false, Error = e, ErrorCode = c }; }
public enum CostCenterErrorCode { NotFound, AlreadyExists, ValidationError, Internal }
