using ERPSystem.Modules.Inventory.Application;
using ERPSystem.Modules.Inventory.Entities;
using ERPSystem.Modules.Inventory.Infrastructure;
using ERPSystem.Modules.Notifications.Application.Services;
using ERPSystem.Shared.Events;
using ERPSystem.Shared.Events.Application.Services;
using Microsoft.Extensions.Logging;
using TaskStatus = ERPSystem.Modules.Inventory.Entities.StockMovementStatus;

namespace ERPSystem.Modules.Inventory.Application.Services;

public sealed class StockMovementResult<T>
{
    public bool Succeeded { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public StockErrorCode? ErrorCode { get; init; }
    public static StockMovementResult<T> Ok(T v) => new() { Succeeded = true, Value = v };
    public static StockMovementResult<T> Fail(string e, StockErrorCode c) => new() { Succeeded = false, Error = e, ErrorCode = c };
}

public enum StockErrorCode
{
    NotFound, AlreadyExists, ValidationError, InsufficientStock, PostFailed, Internal
}

public interface IStockMovementService
{
    Task<StockMovementResult<StockMovementResponse>> CreateReceiveAsync(Guid tenantId, Guid userId, ReceiveStockRequest req, CancellationToken ct);
    Task<StockMovementResult<StockMovementResponse>> CreateIssueAsync(Guid tenantId, Guid userId, IssueStockRequest req, CancellationToken ct);
    Task<StockMovementResult<StockMovementResponse>> CreateTransferAsync(Guid tenantId, Guid userId, TransferStockRequest req, CancellationToken ct);
    Task<StockMovementResult<StockMovementResponse>> CreateAdjustAsync(Guid tenantId, Guid userId, AdjustStockRequest req, CancellationToken ct);
    Task<StockMovementResult<StockMovementResponse>> PostAsync(Guid tenantId, Guid userId, Guid movementId, CancellationToken ct);
    Task<StockMovementResult<StockMovementResponse>> ReverseAsync(Guid tenantId, Guid userId, Guid movementId, string? reason, CancellationToken ct);
    Task<StockMovementResult<StockMovementResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<StockMovementResult<IReadOnlyList<StockMovementResponse>>> ListAsync(Guid tenantId, Guid? companyId, StockMovementType? type, StockMovementStatus? status, int skip, int take, CancellationToken ct);
}

public sealed class StockMovementService : IStockMovementService
{
    private readonly IStockMovementRepository _movements;
    private readonly IStockLevelRepository _levels;
    private readonly IItemRepository _items;
    private readonly IStockReservationRepository _reservations;
    private readonly INotificationService _notifications;
    private readonly IEventBus _eventBus;
    private readonly ILogger<StockMovementService> _logger;

    public StockMovementService(
        IStockMovementRepository movements,
        IStockLevelRepository levels,
        IItemRepository items,
        IStockReservationRepository reservations,
        INotificationService notifications,
        IEventBus eventBus,
        ILogger<StockMovementService> logger)
    {
        _movements = movements; _levels = levels; _items = items; _reservations = reservations;
        _notifications = notifications; _eventBus = eventBus; _logger = logger;
    }

    public async Task<StockMovementResult<StockMovementResponse>> CreateReceiveAsync(Guid tenantId, Guid userId, ReceiveStockRequest req, CancellationToken ct)
    {
        if (await _movements.GetByReferenceAsync(tenantId, req.Reference, ct) != null)
            return StockMovementResult<StockMovementResponse>.Fail("المرجع مستخدم.", StockErrorCode.AlreadyExists);
        var item = await _items.GetByIdAsync(req.ItemId, ct);
        if (item == null || item.TenantId != tenantId) return StockMovementResult<StockMovementResponse>.Fail("الصنف غير موجود.", StockErrorCode.NotFound);
        var movement = new StockMovement
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = req.CompanyId,
            Reference = req.Reference, Type = StockMovementType.Receive, MovementDate = req.MovementDate,
            ItemId = req.ItemId, WarehouseId = req.WarehouseId, Quantity = req.Quantity, UnitCost = req.UnitCost,
            ProjectId = req.ProjectId, CostCenterId = req.CostCenterId,
            SourceType = req.SourceType, SourceId = req.SourceId, Notes = req.Notes,
            Status = StockMovementStatus.Draft, CreatedAt = DateTime.UtcNow, CreatedBy = userId
        };
        await _movements.InsertAsync(movement, ct);
        return StockMovementResult<StockMovementResponse>.Ok(MapToResponse(movement));
    }

    public async Task<StockMovementResult<StockMovementResponse>> CreateIssueAsync(Guid tenantId, Guid userId, IssueStockRequest req, CancellationToken ct)
    {
        // Pre-check: sufficient stock? (post-validation is the real check, but pre-check is friendly)
        var level = await _levels.GetAsync(tenantId, req.ItemId, req.WarehouseId, ct);
        if (level != null && level.QuantityAvailable < req.Quantity)
            return StockMovementResult<StockMovementResponse>.Fail(
                $"المخزون غير كافٍ. المتاح: {level.QuantityAvailable}, المطلوب: {req.Quantity}", StockErrorCode.InsufficientStock);

        if (await _movements.GetByReferenceAsync(tenantId, req.Reference, ct) != null)
            return StockMovementResult<StockMovementResponse>.Fail("المرجع مستخدم.", StockErrorCode.AlreadyExists);

        var movement = new StockMovement
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = req.CompanyId,
            Reference = req.Reference, Type = StockMovementType.Issue, MovementDate = req.MovementDate,
            ItemId = req.ItemId, WarehouseId = req.WarehouseId, Quantity = -req.Quantity, UnitCost = 0,
            ProjectId = req.ProjectId, CostCenterId = req.CostCenterId,
            SourceType = req.SourceType, SourceId = req.SourceId, Notes = req.Notes,
            Status = StockMovementStatus.Draft, CreatedAt = DateTime.UtcNow, CreatedBy = userId
        };
        await _movements.InsertAsync(movement, ct);
        return StockMovementResult<StockMovementResponse>.Ok(MapToResponse(movement));
    }

    public async Task<StockMovementResult<StockMovementResponse>> CreateTransferAsync(Guid tenantId, Guid userId, TransferStockRequest req, CancellationToken ct)
    {
        var level = await _levels.GetAsync(tenantId, req.ItemId, req.SourceWarehouseId, ct);
        if (level != null && level.QuantityAvailable < req.Quantity)
            return StockMovementResult<StockMovementResponse>.Fail(
                $"المخزون غير كافٍ في المصدر. المتاح: {level.QuantityAvailable}, المطلوب: {req.Quantity}", StockErrorCode.InsufficientStock);
        if (await _movements.GetByReferenceAsync(tenantId, req.Reference, ct) != null)
            return StockMovementResult<StockMovementResponse>.Fail("المرجع مستخدم.", StockErrorCode.AlreadyExists);
        var movement = new StockMovement
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = req.CompanyId,
            Reference = req.Reference, Type = StockMovementType.Transfer, MovementDate = req.MovementDate,
            ItemId = req.ItemId, WarehouseId = req.SourceWarehouseId,
            DestinationWarehouseId = req.DestinationWarehouseId,
            Quantity = -req.Quantity, UnitCost = req.UnitCost,  // out
            ProjectId = req.ProjectId, CostCenterId = req.CostCenterId, Notes = req.Notes,
            Status = StockMovementStatus.Draft, CreatedAt = DateTime.UtcNow, CreatedBy = userId
        };
        await _movements.InsertAsync(movement, ct);
        return StockMovementResult<StockMovementResponse>.Ok(MapToResponse(movement));
    }

    public async Task<StockMovementResult<StockMovementResponse>> CreateAdjustAsync(Guid tenantId, Guid userId, AdjustStockRequest req, CancellationToken ct)
    {
        if (await _movements.GetByReferenceAsync(tenantId, req.Reference, ct) != null)
            return StockMovementResult<StockMovementResponse>.Fail("المرجع مستخدم.", StockErrorCode.AlreadyExists);
        var movement = new StockMovement
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = req.CompanyId,
            Reference = req.Reference, Type = StockMovementType.Adjust, MovementDate = req.MovementDate,
            ItemId = req.ItemId, WarehouseId = req.WarehouseId,
            Quantity = req.Quantity, UnitCost = req.UnitCost, Notes = req.Notes,
            Status = StockMovementStatus.Draft, CreatedAt = DateTime.UtcNow, CreatedBy = userId
        };
        await _movements.InsertAsync(movement, ct);
        return StockMovementResult<StockMovementResponse>.Ok(MapToResponse(movement));
    }

    public async Task<StockMovementResult<StockMovementResponse>> PostAsync(Guid tenantId, Guid userId, Guid movementId, CancellationToken ct)
    {
        var movement = await _movements.GetByIdAsync(movementId, ct);
        if (movement == null || movement.TenantId != tenantId) return StockMovementResult<StockMovementResponse>.Fail("غير موجود.", StockErrorCode.NotFound);
        if (movement.Status != StockMovementStatus.Draft)
            return StockMovementResult<StockMovementResponse>.Fail("القيد ليس في حالة Draft.", StockErrorCode.ValidationError);

        // Apply to stock_levels
        try
        {
            await ApplyToStockLevelAsync(movement, ct);

            // Transfer updates TWO levels (out + in)
            if (movement.Type == StockMovementType.Transfer && movement.DestinationWarehouseId.HasValue)
            {
                var inbound = new StockMovement
                {
                    // virtual inbound for level update — quantity positive, source=destination
                    Id = Guid.NewGuid(), TenantId = movement.TenantId, CompanyId = movement.CompanyId,
                    Reference = movement.Reference + "-IN", Type = StockMovementType.Transfer,
                    MovementDate = movement.MovementDate,
                    ItemId = movement.ItemId, WarehouseId = movement.DestinationWarehouseId.Value,
                    Quantity = Math.Abs(movement.Quantity), UnitCost = movement.UnitCost,
                    ProjectId = movement.ProjectId, CostCenterId = movement.CostCenterId,
                    Status = StockMovementStatus.Posted,
                    CreatedAt = DateTime.UtcNow, CreatedBy = userId
                };
                await ApplyToStockLevelAsync(inbound, ct);
            }
        }
        catch (InvalidOperationException ex)
        {
            return StockMovementResult<StockMovementResponse>.Fail(ex.Message, StockErrorCode.InsufficientStock);
        }

        // Update movement status
        movement.Status = StockMovementStatus.Posted;
        movement.PostedAt = DateTime.UtcNow;
        await _movements.UpdateAsync(movement, ct);

        // LowStock notification check (PR #6 deliverable)
        var item = await _items.GetByIdAsync(movement.ItemId, ct);
        if (item != null && item.ReorderLevel > 0)
        {
            var level = await _levels.GetAsync(tenantId, movement.ItemId, movement.WarehouseId, ct);
            if (level != null && level.QuantityAvailable < item.ReorderLevel)
            {
                // target user = creator (in real life: all admins in tenant — but here we just use the actor)
                await _notifications.CreateAsync(tenantId, userId, "LowStock",
                    "تنبيه نقص المخزون",
                    $"{item.Name} وصل إلى {level.QuantityAvailable} (الحد الأدنى: {item.ReorderLevel})",
                    "Item", item.Id);
            }
        }

        // ⭐ Publish integration event (Outbox pattern) — Finance consumes async
        if (movement.Type == StockMovementType.Receive)
        {
            var receiveEvt = new StockReceivedEvent(
                EventId: Guid.NewGuid(), TenantId: tenantId, StockMovementId: movement.Id,
                ItemId: movement.ItemId, WarehouseId: movement.WarehouseId,
                Quantity: Math.Abs(movement.Quantity), UnitCost: movement.UnitCost,
                PurchaseOrderRef: movement.SourceId?.ToString(), OccurredAt: DateTime.UtcNow);
            await _eventBus.PublishAsync(receiveEvt, ct);
        }
        else if (movement.Type == StockMovementType.Issue)
        {
            var issueEvt = new StockIssuedEvent(
                EventId: Guid.NewGuid(), TenantId: tenantId, StockMovementId: movement.Id,
                ItemId: movement.ItemId, WarehouseId: movement.WarehouseId,
                Quantity: Math.Abs(movement.Quantity),
                ReferenceType: movement.SourceType, ReferenceId: movement.SourceId,
                OccurredAt: DateTime.UtcNow);
            await _eventBus.PublishAsync(issueEvt, ct);
        }
        else if (movement.Type == StockMovementType.Transfer && movement.DestinationWarehouseId.HasValue)
        {
            var transferEvt = new StockTransferredEvent(
                EventId: Guid.NewGuid(), TenantId: tenantId, StockMovementId: movement.Id,
                ItemId: movement.ItemId, FromWarehouseId: movement.WarehouseId,
                ToWarehouseId: movement.DestinationWarehouseId.Value,
                Quantity: Math.Abs(movement.Quantity), UnitCost: movement.UnitCost,
                OccurredAt: DateTime.UtcNow);
            await _eventBus.PublishAsync(transferEvt, ct);
        }
        else if (movement.Type == StockMovementType.Adjust)
        {
            var adjEvt = new StockAdjustedEvent(
                EventId: Guid.NewGuid(), TenantId: tenantId, StockMovementId: movement.Id,
                ItemId: movement.ItemId, WarehouseId: movement.WarehouseId,
                QuantityDelta: movement.Quantity, UnitCost: movement.UnitCost,
                Reason: movement.Notes, OccurredAt: DateTime.UtcNow);
            await _eventBus.PublishAsync(adjEvt, ct);
        }

        return StockMovementResult<StockMovementResponse>.Ok(MapToResponse(movement));
    }

    public async Task<StockMovementResult<StockMovementResponse>> ReverseAsync(Guid tenantId, Guid userId, Guid movementId, string? reason, CancellationToken ct)
    {
        var movement = await _movements.GetByIdAsync(movementId, ct);
        if (movement == null || movement.TenantId != tenantId) return StockMovementResult<StockMovementResponse>.Fail("غير موجود.", StockErrorCode.NotFound);
        if (movement.Status != StockMovementStatus.Posted)
            return StockMovementResult<StockMovementResponse>.Fail("لا يمكن عكس قيد غير مُرحّل.", StockErrorCode.ValidationError);

        // Create opposite movement
        var opposite = new StockMovement
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = movement.CompanyId,
            Reference = movement.Reference + "-REV", Type = movement.Type, MovementDate = DateTime.UtcNow,
            ItemId = movement.ItemId, WarehouseId = movement.WarehouseId,
            DestinationWarehouseId = movement.DestinationWarehouseId,
            Quantity = -movement.Quantity, UnitCost = movement.UnitCost,
            ProjectId = movement.ProjectId, CostCenterId = movement.CostCenterId,
            SourceType = movement.SourceType, SourceId = movement.SourceId,
            Notes = $"عكس: {reason ?? "بدون سبب"}",
            Status = StockMovementStatus.Posted, CreatedAt = DateTime.UtcNow, CreatedBy = userId, PostedAt = DateTime.UtcNow,
            ReversedByMovementId = movement.Id
        };
        await _movements.InsertAsync(opposite, ct);
        await ApplyToStockLevelAsync(opposite, ct);

        // Mark original as Reversed
        movement.Status = StockMovementStatus.Reversed;
        await _movements.UpdateAsync(movement, ct);

        return StockMovementResult<StockMovementResponse>.Ok(MapToResponse(opposite));
    }

    public async Task<StockMovementResult<StockMovementResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var m = await _movements.GetByIdAsync(id, ct);
        if (m == null || m.TenantId != tenantId) return StockMovementResult<StockMovementResponse>.Fail("غير موجود.", StockErrorCode.NotFound);
        return StockMovementResult<StockMovementResponse>.Ok(MapToResponse(m));
    }

    public async Task<StockMovementResult<IReadOnlyList<StockMovementResponse>>> ListAsync(Guid tenantId, Guid? companyId, StockMovementType? type, StockMovementStatus? status, int skip, int take, CancellationToken ct)
    {
        if (take is < 1 or > 200) take = 50;
        var list = await _movements.ListAsync(tenantId, companyId, type, status, skip, take, ct);
        return StockMovementResult<IReadOnlyList<StockMovementResponse>>.Ok(list.Select(MapToResponse).ToList());
    }

    /// <summary>
    /// Apply a movement to its stock_level (UPSERT with weighted moving average for Receive).
    /// Throws InvalidOperationException for InsufficientStock on issue/transfer-out.
    /// </summary>
    private async Task ApplyToStockLevelAsync(StockMovement m, CancellationToken ct)
    {
        var existing = await _levels.GetAsync(m.TenantId, m.ItemId, m.WarehouseId, ct);
        var oldQty = existing?.QuantityOnHand ?? 0;
        var oldAvg = existing?.AverageCost ?? 0;
        var newQty = oldQty + m.Quantity;

        // moving weighted average: only for Receive with positive qty
        decimal newAvg = oldAvg;
        if (m.Type == StockMovementType.Receive && m.Quantity > 0)
        {
            if (newQty > 0)
                newAvg = ((oldQty * oldAvg) + (m.Quantity * m.UnitCost)) / newQty;
            else
                newAvg = m.UnitCost;
        }

        if (newQty < 0 && (m.Type == StockMovementType.Issue || m.Type == StockMovementType.Transfer || m.Type == StockMovementType.Adjust))
            throw new InvalidOperationException($"المخزون غير كافٍ لـ {m.Reference}: رصيد {oldQty}، الحركة {m.Quantity}");

        var level = new StockLevel
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            TenantId = m.TenantId, CompanyId = m.CompanyId,
            ItemId = m.ItemId, WarehouseId = m.WarehouseId,
            QuantityOnHand = newQty,
            QuantityReserved = existing?.QuantityReserved ?? 0,
            AverageCost = newAvg,
            LastMovementAt = DateTime.UtcNow,
            Version = (existing?.Version ?? 0) + 1
        };
        if (existing == null)
            await _levels.InsertAsync(level, ct);
        else
            await _levels.UpsertAsync(level, existing.Version, ct);
    }

    private static StockMovementResponse MapToResponse(StockMovement m) => new()
    {
        Id = m.Id, Reference = m.Reference, Type = m.Type, Status = m.Status,
        MovementDate = m.MovementDate, ItemId = m.ItemId, WarehouseId = m.WarehouseId,
        DestinationWarehouseId = m.DestinationWarehouseId,
        Quantity = m.Quantity, UnitCost = m.UnitCost, TotalCost = m.TotalCost,
        ProjectId = m.ProjectId, CostCenterId = m.CostCenterId, Notes = m.Notes,
        CreatedAt = m.CreatedAt, PostedAt = m.PostedAt
    };
}
