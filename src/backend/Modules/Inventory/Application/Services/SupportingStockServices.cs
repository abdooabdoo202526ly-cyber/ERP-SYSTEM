using ERPSystem.Modules.Inventory.Application;
using ERPSystem.Modules.Inventory.Entities;
using ERPSystem.Modules.Inventory.Infrastructure;

namespace ERPSystem.Modules.Inventory.Application.Services;

public interface IStockLevelService
{
    Task<StockMovementResult<StockLevelResponse>> GetAsync(Guid tenantId, Guid itemId, Guid warehouseId, CancellationToken ct);
    Task<StockMovementResult<IReadOnlyList<StockLevelResponse>>> GetByItemAsync(Guid tenantId, Guid itemId, CancellationToken ct);
    Task<StockMovementResult<IReadOnlyList<StockLevelResponse>>> GetByWarehouseAsync(Guid tenantId, Guid warehouseId, CancellationToken ct);
    Task<StockMovementResult<IReadOnlyList<StockLevelResponse>>> GetLowStockAsync(Guid tenantId, Guid companyId, CancellationToken ct);
}

public sealed class StockLevelService : IStockLevelService
{
    private readonly IStockLevelRepository _repo;
    public StockLevelService(IStockLevelRepository r) => _repo = r;
    public async Task<StockMovementResult<StockLevelResponse>> GetAsync(Guid tenantId, Guid itemId, Guid warehouseId, CancellationToken ct)
    {
        var l = await _repo.GetAsync(tenantId, itemId, warehouseId, ct);
        if (l == null) return StockMovementResult<StockLevelResponse>.Fail("غير موجود.", StockErrorCode.NotFound);
        return StockMovementResult<StockLevelResponse>.Ok(MapToResponse(l));
    }
    public async Task<StockMovementResult<IReadOnlyList<StockLevelResponse>>> GetByItemAsync(Guid tenantId, Guid itemId, CancellationToken ct) =>
        StockMovementResult<IReadOnlyList<StockLevelResponse>>.Ok((await _repo.GetByItemAsync(tenantId, itemId, ct)).Select(MapToResponse).ToList());
    public async Task<StockMovementResult<IReadOnlyList<StockLevelResponse>>> GetByWarehouseAsync(Guid tenantId, Guid warehouseId, CancellationToken ct) =>
        StockMovementResult<IReadOnlyList<StockLevelResponse>>.Ok((await _repo.GetByWarehouseAsync(tenantId, warehouseId, ct)).Select(MapToResponse).ToList());
    public async Task<StockMovementResult<IReadOnlyList<StockLevelResponse>>> GetLowStockAsync(Guid tenantId, Guid companyId, CancellationToken ct) =>
        StockMovementResult<IReadOnlyList<StockLevelResponse>>.Ok((await _repo.GetLowStockAsync(tenantId, companyId, ct)).Select(MapToResponse).ToList());
    private static StockLevelResponse MapToResponse(StockLevel l) => new()
    {
        Id = l.Id, ItemId = l.ItemId, WarehouseId = l.WarehouseId,
        QuantityOnHand = l.QuantityOnHand, QuantityReserved = l.QuantityReserved,
        QuantityAvailable = l.QuantityAvailable, AverageCost = l.AverageCost,
        LastMovementAt = l.LastMovementAt, Version = l.Version
    };
}

public interface IStockReservationService
{
    Task<StockMovementResult<ReservationResponse>> CreateAsync(Guid tenantId, Guid userId, CreateReservationRequest req, CancellationToken ct);
    Task<StockMovementResult<bool>> ReleaseAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
    Task<StockMovementResult<IReadOnlyList<ReservationResponse>>> ListAsync(Guid tenantId, Guid? itemId, Guid? warehouseId, CancellationToken ct);
}

public sealed class StockReservationService : IStockReservationService
{
    private readonly IStockReservationRepository _repo;
    public StockReservationService(IStockReservationRepository r) => _repo = r;
    public async Task<StockMovementResult<ReservationResponse>> CreateAsync(Guid tenantId, Guid userId, CreateReservationRequest req, CancellationToken ct)
    {
        var r = new StockReservation
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ItemId = req.ItemId, WarehouseId = req.WarehouseId,
            Quantity = req.Quantity, ReferenceType = req.ReferenceType, ReferenceId = req.ReferenceId,
            ExpiresAt = req.ExpiresAt, CreatedAt = DateTime.UtcNow, CreatedBy = userId
        };
        await _repo.InsertAsync(r, ct);
        return StockMovementResult<ReservationResponse>.Ok(MapToResponse(r));
    }
    public async Task<StockMovementResult<bool>> ReleaseAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        var existing = await _repo.GetByIdAsync(id, ct);
        if (existing == null || existing.TenantId != tenantId) return StockMovementResult<bool>.Fail("غير موجود.", StockErrorCode.NotFound);
        await _repo.DeleteAsync(id, ct);
        return StockMovementResult<bool>.Ok(true);
    }
    public async Task<StockMovementResult<IReadOnlyList<ReservationResponse>>> ListAsync(Guid tenantId, Guid? itemId, Guid? warehouseId, CancellationToken ct) =>
        StockMovementResult<IReadOnlyList<ReservationResponse>>.Ok((await _repo.ListAsync(tenantId, itemId, warehouseId, ct)).Select(MapToResponse).ToList());
    private static ReservationResponse MapToResponse(StockReservation r) => new()
    {
        Id = r.Id, ItemId = r.ItemId, WarehouseId = r.WarehouseId, Quantity = r.Quantity,
        ReferenceType = r.ReferenceType, ReferenceId = r.ReferenceId, ExpiresAt = r.ExpiresAt, CreatedAt = r.CreatedAt
    };
}
