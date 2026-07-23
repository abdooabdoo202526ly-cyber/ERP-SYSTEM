using ERPSystem.Modules.Companies.Entities;
using ERPSystem.Modules.Companies.Infrastructure;
using ERPSystem.Modules.Finance.Infrastructure;
using ERPSystem.Modules.Identity.Application.Auth;
using ERPSystem.Modules.Inventory.Application.Services;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Modules.Companies.Application.Services;

public interface ICompanyService
{
    Task<CompanyResult<Company>> CreateHoldingAsync(Guid tenantId, string code, string name, string legalName, string baseCurrency, CancellationToken ct);
    Task<CompanyResult<Company>> AddSubsidiaryAsync(Guid tenantId, Guid parentCompanyId, string code, string name, string? legalName, CancellationToken ct);
    Task<CompanyResult<Company>> UpdateAsync(Guid tenantId, Guid id, string? name, string? legalName, string? taxId, string? phone, string? email, string? address, bool? isActive, CancellationToken ct);
    Task<CompanyResult<Company>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<CompanyResult<IReadOnlyList<Company>>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct);
    Task<CompanyResult<IReadOnlyList<Company>>> GetSubsidiariesAsync(Guid parentCompanyId, CancellationToken ct);
    Task<CompanyResult<CompanyTreeNode>> GetTreeAsync(Guid tenantId, CancellationToken ct);
    Task<CompanyResult<bool>> DeactivateAsync(Guid tenantId, Guid id, CancellationToken ct);
    // v1.0.22: ensure a default holding company exists for the tenant
    Task<Guid> EnsureDefaultHoldingAsync(Guid tenantId, string tenantName, string baseCurrency, CancellationToken ct);
}

public sealed class CompanyTreeNode { public Company Company { get; set; } = null!; public List<CompanyTreeNode> Children { get; set; } = new(); }
public sealed class CompanyResult<T> { public bool Succeeded { get; init; } public T? Value { get; init; } public string? Error { get; init; } public CompanyErrorCode? ErrorCode { get; init; } public static CompanyResult<T> Ok(T v) => new() { Succeeded = true, Value = v }; public static CompanyResult<T> Fail(string e, CompanyErrorCode c) => new() { Succeeded = false, Error = e, ErrorCode = c }; }
public enum CompanyErrorCode { NotFound, AlreadyExists, ValidationError, InUse, Internal }

public sealed class CompanyService : ICompanyService, ITenantBootstrap
{
    private readonly ICompanyRepository _companies;
    private readonly IAccountRepository _accounts;
    private readonly ILogger<CompanyService> _logger;
    private readonly IInventoryBootstrapper _inventoryBootstrap;
    public CompanyService(ICompanyRepository c, IAccountRepository a, IInventoryBootstrapper ib, ILogger<CompanyService> l) { _companies = c; _accounts = a; _inventoryBootstrap = ib; _logger = l; }

    public async Task<Guid> OnTenantCreatedAsync(Guid tenantId, string tenantName, string baseCurrency, CancellationToken ct)
    {
        return await EnsureDefaultHoldingAsync(tenantId, tenantName, baseCurrency, ct);
    }

    // v1.0.22: ensure a default holding company exists for the tenant
    public async Task<Guid> EnsureDefaultHoldingAsync(Guid tenantId, string tenantName, string baseCurrency, CancellationToken ct)
    {
        var existing = (await _companies.ListAsync(tenantId, true, ct)).FirstOrDefault(c => c.IsGroup);
        if (existing != null) return existing.Id;
        existing = await _companies.GetByCodeAsync(tenantId, "000", ct);
        if (existing != null) return existing.Id;
        var r = await CreateHoldingAsync(tenantId, "000", string.IsNullOrEmpty(tenantName) ? "Holding" : $"{tenantName} (Holding)", tenantName, baseCurrency, ct);
        if (r.Succeeded)
        {
            // Phase 2.2: seed UoMs and Categories for new tenant
            try { await _inventoryBootstrap.EnsureDefaultUoMsAndCategoriesAsync(tenantId, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "فشل زرع UoMs/Categories للمستأجر {TenantId}", tenantId); }
        }
        return r.Succeeded ? r.Value!.Id : Guid.Empty;
    }

    public async Task<CompanyResult<Company>> CreateHoldingAsync(Guid tenantId, string code, string name, string legalName, string baseCurrency, CancellationToken ct)
    {
        if (await _companies.GetByCodeAsync(tenantId, code, ct) != null) return CompanyResult<Company>.Fail("كود الشركة مستخدم.", CompanyErrorCode.AlreadyExists);
        var now = DateTime.UtcNow;
        var c = new Company { Id = Guid.NewGuid(), TenantId = tenantId, Code = code.Trim(), Name = name.Trim(), LegalName = legalName, IsGroup = true, BaseCurrency = baseCurrency.ToUpperInvariant(), IsActive = true, CreatedAt = now, UpdatedAt = now };
        await _companies.InsertAsync(c, ct);
        await _accounts.EnsureDefaultCoAAsync(tenantId, c.Id, ct);
        return CompanyResult<Company>.Ok(c);
    }

    public async Task<CompanyResult<Company>> AddSubsidiaryAsync(Guid tenantId, Guid parentCompanyId, string code, string name, string? legalName, CancellationToken ct)
    {
        var parent = await _companies.GetByIdAsync(parentCompanyId, ct);
        if (parent == null || parent.TenantId != tenantId) return CompanyResult<Company>.Fail("الشركة الأم غير موجودة.", CompanyErrorCode.NotFound);
        if (!parent.IsGroup) return CompanyResult<Company>.Fail("ليست Holding.", CompanyErrorCode.ValidationError);
        if (await _companies.GetByCodeAsync(tenantId, code, ct) != null) return CompanyResult<Company>.Fail("كود مستخدم.", CompanyErrorCode.AlreadyExists);
        var now = DateTime.UtcNow;
        var sub = new Company { Id = Guid.NewGuid(), TenantId = tenantId, Code = code.Trim(), Name = name.Trim(), LegalName = legalName, ParentCompanyId = parent.Id, IsGroup = false, BaseCurrency = parent.BaseCurrency, IsActive = true, CreatedAt = now, UpdatedAt = now };
        await _companies.InsertAsync(sub, ct);
        await _accounts.CloneCoAFromCompanyAsync(sub.Id, parent.Id, ct);
        return CompanyResult<Company>.Ok(sub);
    }

    public async Task<CompanyResult<Company>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var c = await _companies.GetByIdAsync(id, ct);
        if (c == null || c.TenantId != tenantId) return CompanyResult<Company>.Fail("غير موجودة.", CompanyErrorCode.NotFound);
        return CompanyResult<Company>.Ok(c);
    }
    public async Task<CompanyResult<IReadOnlyList<Company>>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct) =>
        CompanyResult<IReadOnlyList<Company>>.Ok(await _companies.ListAsync(tenantId, includeInactive, ct));
    public async Task<CompanyResult<IReadOnlyList<Company>>> GetSubsidiariesAsync(Guid parentCompanyId, CancellationToken ct) =>
        CompanyResult<IReadOnlyList<Company>>.Ok(await _companies.ListSubsidiariesAsync(parentCompanyId, ct));
    public async Task<CompanyResult<CompanyTreeNode>> GetTreeAsync(Guid tenantId, CancellationToken ct)
    {
        var all = await _companies.ListAsync(tenantId, true, ct);
        var tree = all.Where(c => c.ParentCompanyId == null).Select(r => BuildTree(r, all)).ToList();
        return CompanyResult<CompanyTreeNode>.Ok(new CompanyTreeNode { Children = tree });
    }
    public async Task<CompanyResult<bool>> DeactivateAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var c = await _companies.GetByIdAsync(id, ct);
        if (c == null || c.TenantId != tenantId) return CompanyResult<bool>.Fail("غير موجودة.", CompanyErrorCode.NotFound);
        c.IsActive = false; c.UpdatedAt = DateTime.UtcNow;
        await _companies.UpdateAsync(c, ct);
        return CompanyResult<bool>.Ok(true);
    }

    public async Task<CompanyResult<Company>> UpdateAsync(Guid tenantId, Guid id, string? name, string? legalName, string? taxId, string? phone, string? email, string? address, bool? isActive, CancellationToken ct)
    {
        var c = await _companies.GetByIdAsync(id, ct);
        if (c == null || c.TenantId != tenantId) return CompanyResult<Company>.Fail("غير موجودة.", CompanyErrorCode.NotFound);
        if (name != null) c.Name = name.Trim();
        if (legalName != null) c.LegalName = legalName.Trim();
        if (taxId != null) c.TaxId = taxId.Trim();
        if (phone != null) c.Phone = phone.Trim();
        if (email != null) c.Email = email.Trim();
        if (address != null) c.Address = address.Trim();
        if (isActive.HasValue) c.IsActive = isActive.Value;
        c.UpdatedAt = DateTime.UtcNow;
        await _companies.UpdateAsync(c, ct);
        return CompanyResult<Company>.Ok(c);
    }
    private static CompanyTreeNode BuildTree(Company n, IReadOnlyList<Company> all) => new() { Company = n, Children = all.Where(c => c.ParentCompanyId == n.Id).Select(c => BuildTree(c, all)).ToList() };
}
