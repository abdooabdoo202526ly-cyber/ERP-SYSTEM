using ERPSystem.Modules.Inventory.Entities;

namespace ERPSystem.Modules.Inventory.Infrastructure;

public interface IStockMovementRepository
{
    Task<StockMovement?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<StockMovement?> GetByReferenceAsync(Guid tenantId, string reference, CancellationToken ct);
    Task<IReadOnlyList<StockMovement>> ListAsync(Guid tenantId, Guid? companyId, StockMovementType? type, StockMovementStatus? status, int skip, int take, CancellationToken ct);
    Task InsertAsync(StockMovement movement, CancellationToken ct);
    Task UpdateAsync(StockMovement movement, CancellationToken ct);
}

public interface IStockLevelRepository
{
    Task<StockLevel?> GetAsync(Guid tenantId, Guid itemId, Guid warehouseId, CancellationToken ct);
    Task<IReadOnlyList<StockLevel>> GetByItemAsync(Guid tenantId, Guid itemId, CancellationToken ct);
    Task<IReadOnlyList<StockLevel>> GetByWarehouseAsync(Guid tenantId, Guid warehouseId, CancellationToken ct);
    Task<IReadOnlyList<StockLevel>> GetLowStockAsync(Guid tenantId, Guid companyId, CancellationToken ct);
    /// <summary>UPSERT مع optimistic concurrency check (Version)</summary>
    Task UpsertAsync(StockLevel level, int expectedVersion, CancellationToken ct);
    Task InsertAsync(StockLevel level, CancellationToken ct);
}

public interface IStockReservationRepository
{
    Task<StockReservation?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<StockReservation>> ListAsync(Guid tenantId, Guid? itemId, Guid? warehouseId, CancellationToken ct);
    Task<IReadOnlyList<StockReservation>> GetByReferenceAsync(Guid tenantId, string referenceType, Guid referenceId, CancellationToken ct);
    Task InsertAsync(StockReservation reservation, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
