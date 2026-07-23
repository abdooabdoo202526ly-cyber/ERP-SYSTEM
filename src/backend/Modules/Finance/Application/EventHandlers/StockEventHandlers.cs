using ERPSystem.Modules.Finance.Application.Services;
using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Inventory.Entities;
using ERPSystem.Modules.Inventory.Infrastructure;
using ERPSystem.Shared.Events;
using ERPSystem.Shared.Events.Application.Services;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Modules.Finance.Application.EventHandlers;

/// <summary>
/// عند StockReceived (Receive, Return, Adjust-up):
/// PostingRule "StockReceived" ينشئ Journal Entry: Dr Inventory (1300) / Cr A/P (2100) أو Cash حسب الـ rule
/// الـ amount = Quantity * UnitCost
/// </summary>
public sealed class StockReceivedEventHandler : IIntegrationEventHandler<StockReceivedEvent>
{
    private readonly IPostingRulesService _rules;
    private readonly IItemRepository _items;
    private readonly ILogger<StockReceivedEventHandler> _logger;

    public StockReceivedEventHandler(IPostingRulesService rules, IItemRepository items, ILogger<StockReceivedEventHandler> logger)
    {
        _rules = rules; _items = items; _logger = logger;
    }

    public async Task HandleAsync(StockReceivedEvent @event, CancellationToken ct)
    {
        var item = await _items.GetByIdAsync(@event.ItemId, ct);
        if (item == null || item.TenantId != @event.TenantId)
        {
            _logger.LogWarning("StockReceived event {EventId} skipped: item {ItemId} not found in tenant {TenantId}",
                @event.EventId, @event.ItemId, @event.TenantId);
            return;
        }
        if (item.InventoryAccountId == null)
        {
            _logger.LogWarning("StockReceived event {EventId} skipped: item {Sku} has no InventoryAccountId",
                @event.EventId, item.Sku);
            return;
        }

        // amount = qty * unitCost (positive for receive)
        var amount = @event.Quantity * @event.UnitCost;
        if (amount <= 0) return;

        var userId = Guid.Empty; // system-generated
        var payload = new EventPayload { Amount = amount, Description = $"استلام بضاعة {item.Sku} × {@event.Quantity}", Reference = @event.PurchaseOrderRef };
        var count = await _rules.ApplyRulesAsync(@event.TenantId, userId, TriggeringEvent.StockReceived, payload, ct);
        _logger.LogInformation("StockReceived {EventId}: applied {Count} posting rule(s), amount={Amount}",
            @event.EventId, count, amount);
    }
}

/// <summary>
/// عند StockIssued (Issue, Transfer-out, Adjust-down):
/// PostingRule "StockIssued" ينشئ: Dr COGS (5100) / Cr Inventory (1300)
/// الـ amount = |Quantity| * AverageCost (moving average)
/// </summary>
public sealed class StockIssuedEventHandler : IIntegrationEventHandler<StockIssuedEvent>
{
    private readonly IPostingRulesService _rules;
    private readonly IItemRepository _items;
    private readonly IStockLevelRepository _levels;
    private readonly ILogger<StockIssuedEventHandler> _logger;

    public StockIssuedEventHandler(IPostingRulesService rules, IItemRepository items,
        IStockLevelRepository levels, ILogger<StockIssuedEventHandler> logger)
    {
        _rules = rules; _items = items; _levels = levels; _logger = logger;
    }

    public async Task HandleAsync(StockIssuedEvent @event, CancellationToken ct)
    {
        var item = await _items.GetByIdAsync(@event.ItemId, ct);
        if (item == null || item.TenantId != @event.TenantId) return;
        if (item.CogsAccountId == null || item.InventoryAccountId == null) return;

        var level = await _levels.GetAsync(@event.TenantId, @event.ItemId, @event.WarehouseId, ct);
        var unitCost = level?.AverageCost ?? 0;
        var amount = Math.Abs(@event.Quantity) * unitCost;
        if (amount <= 0) return;

        var userId = Guid.Empty;
        var payload = new EventPayload { Amount = amount, Description = $"صرف بضاعة {item.Sku} × {Math.Abs(@event.Quantity)}", Reference = @event.ReferenceType };
        var count = await _rules.ApplyRulesAsync(@event.TenantId, userId, TriggeringEvent.StockIssued, payload, ct);
        _logger.LogInformation("StockIssued {EventId}: applied {Count} rule(s), amount={Amount}", @event.EventId, count, amount);
    }
}

/// <summary>
/// Transfer بين مخازن — لا P&L effect (لا posting rule).
/// الحين: فقط logging للأغراض الـ audit.
/// مستقبلياً: يمكن tracking inter-warehouse costing variance.
/// </summary>
public sealed class StockTransferredEventHandler : IIntegrationEventHandler<StockTransferredEvent>
{
    private readonly ILogger<StockTransferredEventHandler> _logger;
    public StockTransferredEventHandler(ILogger<StockTransferredEventHandler> logger) { _logger = logger; }
    public Task HandleAsync(StockTransferredEvent @event, CancellationToken ct)
    {
        _logger.LogInformation("StockTransferred: {Item} × {Qty} from {FromWh} → {ToWh}",
            @event.ItemId, @event.Quantity, @event.FromWarehouseId, @event.ToWarehouseId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Adjust (manual correction) — لا posting rule افتراضي.
/// في Phase #8 يمكن إضافة PostingRule "StockAdjusted" لكن MVP يكتفى بـ logging.
/// </summary>
public sealed class StockAdjustedEventHandler : IIntegrationEventHandler<StockAdjustedEvent>
{
    private readonly ILogger<StockAdjustedEventHandler> _logger;
    public StockAdjustedEventHandler(ILogger<StockAdjustedEventHandler> logger) { _logger = logger; }
    public Task HandleAsync(StockAdjustedEvent @event, CancellationToken ct)
    {
        _logger.LogInformation("StockAdjusted: {Item} {Delta} in {Wh} (reason: {Reason})",
            @event.ItemId, @event.QuantityDelta, @event.WarehouseId, @event.Reason ?? "—");
        return Task.CompletedTask;
    }
}
