using ERPSystem.Modules.Inventory.Entities;
using ERPSystem.Modules.Inventory.Infrastructure;
using ERPSystem.Shared.SeedData;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Modules.Inventory.Application.Services;

/// <summary>
/// يُستدعى من ITenantBootstrap.OnTenantCreatedAsync
/// لزرع UoMs و Categories الافتراضية لكل tenant جديد.
/// </summary>
public interface IInventoryBootstrapper
{
    Task EnsureDefaultUoMsAndCategoriesAsync(Guid tenantId, CancellationToken ct);
}

public sealed class InventoryBootstrapper : IInventoryBootstrapper
{
    private readonly IUnitOfMeasureRepository _uoms;
    private readonly IItemCategoryRepository _categories;
    private readonly ILogger<InventoryBootstrapper> _logger;

    public InventoryBootstrapper(IUnitOfMeasureRepository uoms, IItemCategoryRepository categories, ILogger<InventoryBootstrapper> logger)
    {
        _uoms = uoms; _categories = categories; _logger = logger;
    }

    public async Task EnsureDefaultUoMsAndCategoriesAsync(Guid tenantId, CancellationToken ct)
    {
        // UoMs
        if (await _uoms.GetByCodeAsync(tenantId, "pcs", ct) == null)
        {
            foreach (var (code, name, symbol) in DefaultInventorySeed.DefaultUoMs)
            {
                if (await _uoms.GetByCodeAsync(tenantId, code, ct) == null)
                {
                    await _uoms.InsertAsync(new UnitOfMeasure
                    {
                        Id = Guid.NewGuid(), TenantId = tenantId, Code = code, Name = name, Symbol = symbol,
                        IsActive = true, CreatedAt = DateTime.UtcNow
                    }, ct);
                }
            }
            _logger.LogInformation("تم زرع 6 UoMs افتراضية للمستأجر {TenantId}", tenantId);
        }

        // Categories
        if (await _categories.GetByCodeAsync(tenantId, "RM", ct) == null)
        {
            // Phase 1: roots
            var idByCode = new Dictionary<string, Guid>();
            foreach (var (code, name, _, parentCode) in DefaultInventorySeed.DefaultCategories.Where(c => c.ParentCode == null))
            {
                var id = Guid.NewGuid();
                await _categories.InsertAsync(new ItemCategory
                {
                    Id = id, TenantId = tenantId, Code = code, Name = name, ParentId = null,
                    IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                }, ct);
                idByCode[code] = id;
            }
            _logger.LogInformation("تم زرع 5 تصنيفات افتراضية للمستأجر {TenantId}", tenantId);
        }
    }
}
