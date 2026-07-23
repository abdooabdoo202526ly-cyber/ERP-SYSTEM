using ERPSystem.Modules.Inventory.Entities;

namespace ERPSystem.Modules.Inventory.Infrastructure;

public interface IItemRepository
{
    Task<Item?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Item?> GetBySkuAsync(Guid tenantId, string sku, CancellationToken ct);
    Task<Item?> GetByBarcodeAsync(Guid tenantId, string barcode, CancellationToken ct);
    Task<IReadOnlyList<Item>> ListAsync(Guid tenantId, Guid? companyId, Guid? categoryId, bool includeInactive, int skip, int take, CancellationToken ct);
    Task InsertAsync(Item item, CancellationToken ct);
    Task UpdateAsync(Item item, CancellationToken ct);
}

public interface IWarehouseRepository
{
    Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Warehouse?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct);
    Task<IReadOnlyList<Warehouse>> ListAsync(Guid tenantId, Guid? companyId, bool includeInactive, CancellationToken ct);
    Task<IReadOnlyList<Warehouse>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct); // DEC-031
    Task InsertAsync(Warehouse warehouse, CancellationToken ct);
    Task UpdateAsync(Warehouse warehouse, CancellationToken ct);
}

public interface IUnitOfMeasureRepository
{
    Task<UnitOfMeasure?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<UnitOfMeasure?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct);
    Task<IReadOnlyList<UnitOfMeasure>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct);
    Task InsertAsync(UnitOfMeasure uom, CancellationToken ct);
    Task UpdateAsync(UnitOfMeasure uom, CancellationToken ct);
}

public interface IItemCategoryRepository
{
    Task<ItemCategory?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<ItemCategory?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct);
    Task<IReadOnlyList<ItemCategory>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct);
    Task<IReadOnlyList<ItemCategory>> ListChildrenAsync(Guid parentId, CancellationToken ct);
    Task InsertAsync(ItemCategory category, CancellationToken ct);
    Task UpdateAsync(ItemCategory category, CancellationToken ct);
}
