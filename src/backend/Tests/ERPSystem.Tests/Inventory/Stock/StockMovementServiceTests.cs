using ERPSystem.Modules.Inventory.Application;
using ERPSystem.Modules.Inventory.Application.Services;
using ERPSystem.Modules.Inventory.Entities;
using ERPSystem.Modules.Inventory.Infrastructure;
using ERPSystem.Modules.Notifications.Application.Services;
using ERPSystem.Shared.Events;
using ERPSystem.Shared.Events.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERPSystem.Tests.Inventory.Stock;

public class StockMovementServiceTests
{
    // Single test fixture: tenant + item pre-wired for that tenant
    private sealed class Fixture
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public FakeItemRepo Items { get; } = new();
        public FakeStockMovementRepo Movements { get; } = new();
        public FakeStockLevelRepo Levels { get; } = new();
        public FakeStockReservationRepo Reservations { get; } = new();
        public FakeNotificationService Notifications { get; } = new();
        public StockMovementService Svc { get; }

        public Fixture()
        {
            Svc = new StockMovementService(Movements, Levels, Items, Reservations, Notifications, new FakeEventBus(), NullLogger<StockMovementService>.Instance);
        }

        public Item AddItem(string sku = "TEST", decimal reorder = 0, decimal reorderQty = 0)
        {
            var i = new Item
            {
                Id = Guid.NewGuid(), TenantId = TenantId, CompanyId = Guid.NewGuid(), Sku = sku, Name = "صنف اختبار",
                ReorderLevel = reorder, ReorderQuantity = reorderQty, IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = Guid.NewGuid()
            };
            Items.Items[i.Id] = i;
            return i;
        }
    }

    [Fact]
    public async Task CreateReceive_StoresAsDraft_WithPositiveQuantity()
    {
        var f = new Fixture();
        var item = f.AddItem();
        var r = await f.Svc.CreateReceiveAsync(f.TenantId, Guid.NewGuid(), new ReceiveStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "RCV-001", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = Guid.NewGuid(), Quantity = 100, UnitCost = 10
        }, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Status.Should().Be(StockMovementStatus.Draft);
        r.Value.Quantity.Should().Be(100);
    }

    [Fact]
    public async Task Post_Receive_UpdatesStockLevel_WithMovingAverage()
    {
        var f = new Fixture();
        var item = f.AddItem();
        var r = await f.Svc.CreateReceiveAsync(f.TenantId, Guid.NewGuid(), new ReceiveStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "RCV-A", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = Guid.NewGuid(), Quantity = 100, UnitCost = 10
        }, CancellationToken.None);
        var post = await f.Svc.PostAsync(f.TenantId, Guid.NewGuid(), r.Value!.Id, CancellationToken.None);
        post.Succeeded.Should().BeTrue();
        post.Value!.Status.Should().Be(StockMovementStatus.Posted);
        var level = f.Levels.Levels.Values.First();
        level.QuantityOnHand.Should().Be(100);
        level.AverageCost.Should().Be(10);
    }

    [Fact]
    public async Task Post_SecondReceive_RecomputesMovingAverage()
    {
        var f = new Fixture();
        var item = f.AddItem();
        var r1 = await f.Svc.CreateReceiveAsync(f.TenantId, Guid.NewGuid(), new ReceiveStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "RCV-1", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = Guid.NewGuid(), Quantity = 100, UnitCost = 10
        }, CancellationToken.None);
        await f.Svc.PostAsync(f.TenantId, Guid.NewGuid(), r1.Value!.Id, CancellationToken.None);
        var r2 = await f.Svc.CreateReceiveAsync(f.TenantId, Guid.NewGuid(), new ReceiveStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "RCV-2", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = r1.Value.WarehouseId, Quantity = 100, UnitCost = 12
        }, CancellationToken.None);
        await f.Svc.PostAsync(f.TenantId, Guid.NewGuid(), r2.Value!.Id, CancellationToken.None);
        var level = f.Levels.Levels.Values.First();
        level.QuantityOnHand.Should().Be(200);
        level.AverageCost.Should().Be(11, "(100*10 + 100*12) / 200");
    }

    [Fact]
    public async Task Post_Issue_DecreasesStockLevel()
    {
        var f = new Fixture();
        var item = f.AddItem();
        var r1 = await f.Svc.CreateReceiveAsync(f.TenantId, Guid.NewGuid(), new ReceiveStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "RCV", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = Guid.NewGuid(), Quantity = 100, UnitCost = 10
        }, CancellationToken.None);
        await f.Svc.PostAsync(f.TenantId, Guid.NewGuid(), r1.Value!.Id, CancellationToken.None);
        var r2 = await f.Svc.CreateIssueAsync(f.TenantId, Guid.NewGuid(), new IssueStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "ISS", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = r1.Value.WarehouseId, Quantity = 30
        }, CancellationToken.None);
        var post = await f.Svc.PostAsync(f.TenantId, Guid.NewGuid(), r2.Value!.Id, CancellationToken.None);
        post.Succeeded.Should().BeTrue();
        var level = f.Levels.Levels.Values.First();
        level.QuantityOnHand.Should().Be(70);
    }

    [Fact]
    public async Task PostIssue_InsufficientStock_FailsAtPost()
    {
        var f = new Fixture();
        var item = f.AddItem();
        // CreateIssue passes pre-check (no stock exists yet)
        var draft = await f.Svc.CreateIssueAsync(f.TenantId, Guid.NewGuid(), new IssueStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "ISS", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = Guid.NewGuid(), Quantity = 5
        }, CancellationToken.None);
        draft.Succeeded.Should().BeTrue();
        // PostAsync is where the actual stock check happens
        var post = await f.Svc.PostAsync(f.TenantId, Guid.NewGuid(), draft.Value!.Id, CancellationToken.None);
        post.Succeeded.Should().BeFalse();
        post.ErrorCode.Should().Be(StockErrorCode.InsufficientStock);
    }

    [Fact]
    public async Task Post_Transfer_UpdatesBothWarehousesAtomically()
    {
        var f = new Fixture();
        var item = f.AddItem();
        var src = Guid.NewGuid();
        var dst = Guid.NewGuid();
        var r1 = await f.Svc.CreateReceiveAsync(f.TenantId, Guid.NewGuid(), new ReceiveStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "RCV", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = src, Quantity = 100, UnitCost = 10
        }, CancellationToken.None);
        await f.Svc.PostAsync(f.TenantId, Guid.NewGuid(), r1.Value!.Id, CancellationToken.None);
        var r2 = await f.Svc.CreateTransferAsync(f.TenantId, Guid.NewGuid(), new TransferStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "TRF", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, SourceWarehouseId = src, DestinationWarehouseId = dst,
            Quantity = 30, UnitCost = 10
        }, CancellationToken.None);
        var post = await f.Svc.PostAsync(f.TenantId, Guid.NewGuid(), r2.Value!.Id, CancellationToken.None);
        post.Succeeded.Should().BeTrue();
        f.Levels.Levels.Values.First(l => l.WarehouseId == src).QuantityOnHand.Should().Be(70);
        f.Levels.Levels.Values.First(l => l.WarehouseId == dst).QuantityOnHand.Should().Be(30);
    }

    [Fact]
    public async Task Post_DuplicateReference_Fails()
    {
        var f = new Fixture();
        var item = f.AddItem();
        await f.Svc.CreateReceiveAsync(f.TenantId, Guid.NewGuid(), new ReceiveStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "DUPL", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = Guid.NewGuid(), Quantity = 10, UnitCost = 5
        }, CancellationToken.None);
        var r = await f.Svc.CreateReceiveAsync(f.TenantId, Guid.NewGuid(), new ReceiveStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "DUPL", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = Guid.NewGuid(), Quantity = 20, UnitCost = 6
        }, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(StockErrorCode.AlreadyExists);
    }

    [Fact]
    public async Task Post_AlreadyPosted_Fails()
    {
        var f = new Fixture();
        var item = f.AddItem();
        var r = await f.Svc.CreateReceiveAsync(f.TenantId, Guid.NewGuid(), new ReceiveStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "P", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = Guid.NewGuid(), Quantity = 10, UnitCost = 5
        }, CancellationToken.None);
        await f.Svc.PostAsync(f.TenantId, Guid.NewGuid(), r.Value!.Id, CancellationToken.None);
        var second = await f.Svc.PostAsync(f.TenantId, Guid.NewGuid(), r.Value!.Id, CancellationToken.None);
        second.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Reverse_CreatesOpposite_MarksOriginalAsReversed()
    {
        var f = new Fixture();
        var item = f.AddItem();
        var r1 = await f.Svc.CreateReceiveAsync(f.TenantId, Guid.NewGuid(), new ReceiveStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "REV", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = Guid.NewGuid(), Quantity = 50, UnitCost = 10
        }, CancellationToken.None);
        await f.Svc.PostAsync(f.TenantId, Guid.NewGuid(), r1.Value!.Id, CancellationToken.None);
        var reverse = await f.Svc.ReverseAsync(f.TenantId, Guid.NewGuid(), r1.Value!.Id, "تصحيح", CancellationToken.None);
        reverse.Succeeded.Should().BeTrue();
        reverse.Value!.Reference.Should().EndWith("-REV");
        reverse.Value.Quantity.Should().Be(-50);
        reverse.Value.Status.Should().Be(StockMovementStatus.Posted);
        f.Levels.Levels.Values.First().QuantityOnHand.Should().Be(0);
        var orig = await f.Svc.GetByIdAsync(f.TenantId, r1.Value!.Id, CancellationToken.None);
        orig.Value!.Status.Should().Be(StockMovementStatus.Reversed);
    }

    [Fact]
    public async Task Post_LowStock_CreatesNotification()
    {
        var f = new Fixture();
        var item = f.AddItem(sku: "REORDER", reorder: 50, reorderQty: 100);
        var r = await f.Svc.CreateReceiveAsync(f.TenantId, Guid.NewGuid(), new ReceiveStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "R", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = Guid.NewGuid(), Quantity = 10, UnitCost = 5
        }, CancellationToken.None);
        await f.Svc.PostAsync(f.TenantId, Guid.NewGuid(), r.Value!.Id, CancellationToken.None);
        f.Notifications.Created.Count.Should().Be(1);
        f.Notifications.Created[0].Type.Should().Be("LowStock");
        f.Notifications.Created[0].ReferenceId.Should().Be(item.Id);
    }

    [Fact]
    public async Task Post_AboveReorder_NoNotification()
    {
        var f = new Fixture();
        var item = f.AddItem(sku: "OK", reorder: 5);
        var r = await f.Svc.CreateReceiveAsync(f.TenantId, Guid.NewGuid(), new ReceiveStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "OK", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = Guid.NewGuid(), Quantity = 50, UnitCost = 5
        }, CancellationToken.None);
        await f.Svc.PostAsync(f.TenantId, Guid.NewGuid(), r.Value!.Id, CancellationToken.None);
        f.Notifications.Created.Count.Should().Be(0);
    }

    [Fact]
    public async Task Post_Adjust_Negative_AllowedWithinStock()
    {
        var f = new Fixture();
        var item = f.AddItem();
        var r1 = await f.Svc.CreateReceiveAsync(f.TenantId, Guid.NewGuid(), new ReceiveStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "A", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = Guid.NewGuid(), Quantity = 100, UnitCost = 10
        }, CancellationToken.None);
        await f.Svc.PostAsync(f.TenantId, Guid.NewGuid(), r1.Value!.Id, CancellationToken.None);
        var adj = await f.Svc.CreateAdjustAsync(f.TenantId, Guid.NewGuid(), new AdjustStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "ADJ-1", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = r1.Value.WarehouseId, Quantity = -5, UnitCost = 0
        }, CancellationToken.None);
        var post = await f.Svc.PostAsync(f.TenantId, Guid.NewGuid(), adj.Value!.Id, CancellationToken.None);
        post.Succeeded.Should().BeTrue();
        f.Levels.Levels.Values.First().QuantityOnHand.Should().Be(95);
    }

    [Fact]
    public async Task Post_Adjust_NegativeBeyondStock_Fails()
    {
        var f = new Fixture();
        var item = f.AddItem();
        var r1 = await f.Svc.CreateReceiveAsync(f.TenantId, Guid.NewGuid(), new ReceiveStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "B", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = Guid.NewGuid(), Quantity = 10, UnitCost = 5
        }, CancellationToken.None);
        await f.Svc.PostAsync(f.TenantId, Guid.NewGuid(), r1.Value!.Id, CancellationToken.None);
        var adj = await f.Svc.CreateAdjustAsync(f.TenantId, Guid.NewGuid(), new AdjustStockRequest
        {
            CompanyId = Guid.NewGuid(), Reference = "ADJ-X", MovementDate = DateTime.UtcNow,
            ItemId = item.Id, WarehouseId = r1.Value.WarehouseId, Quantity = -20, UnitCost = 0
        }, CancellationToken.None);
        var post = await f.Svc.PostAsync(f.TenantId, Guid.NewGuid(), adj.Value!.Id, CancellationToken.None);
        post.Succeeded.Should().BeFalse();
        post.ErrorCode.Should().Be(StockErrorCode.InsufficientStock);
    }
}

// ============== Fakes ==============

internal class FakeStockMovementRepo : IStockMovementRepository
{
    private readonly Dictionary<Guid, StockMovement> _items = new();
    public Task<StockMovement?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var m) ? m : null);
    public Task<StockMovement?> GetByReferenceAsync(Guid tenantId, string reference, CancellationToken ct) =>
        Task.FromResult(_items.Values.FirstOrDefault(m => m.TenantId == tenantId && m.Reference == reference));
    public Task<IReadOnlyList<StockMovement>> ListAsync(Guid tenantId, Guid? companyId, StockMovementType? type, StockMovementStatus? status, int skip, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StockMovement>>(_items.Values
            .Where(m => m.TenantId == tenantId && (companyId == null || m.CompanyId == companyId)
                && (type == null || m.Type == type) && (status == null || m.Status == status))
            .OrderByDescending(m => m.MovementDate).ToList());
    public Task InsertAsync(StockMovement m, CancellationToken ct) { _items[m.Id] = m; return Task.CompletedTask; }
    public Task UpdateAsync(StockMovement m, CancellationToken ct) { _items[m.Id] = m; return Task.CompletedTask; }
}

internal class FakeStockLevelRepo : IStockLevelRepository
{
    public Dictionary<Guid, StockLevel> Levels { get; } = new();
    public Task<StockLevel?> GetAsync(Guid tenantId, Guid itemId, Guid warehouseId, CancellationToken ct) =>
        Task.FromResult(Levels.Values.FirstOrDefault(l => l.TenantId == tenantId && l.ItemId == itemId && l.WarehouseId == warehouseId));
    public Task<IReadOnlyList<StockLevel>> GetByItemAsync(Guid tenantId, Guid itemId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StockLevel>>(Levels.Values.Where(l => l.TenantId == tenantId && l.ItemId == itemId).ToList());
    public Task<IReadOnlyList<StockLevel>> GetByWarehouseAsync(Guid tenantId, Guid warehouseId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StockLevel>>(Levels.Values.Where(l => l.TenantId == tenantId && l.WarehouseId == warehouseId).ToList());
    public Task<IReadOnlyList<StockLevel>> GetLowStockAsync(Guid tenantId, Guid companyId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StockLevel>>(Levels.Values.Where(l => l.TenantId == tenantId && l.CompanyId == companyId).ToList());
    public Task UpsertAsync(StockLevel level, int expectedVersion, CancellationToken ct) { Levels[level.Id] = level; return Task.CompletedTask; }
    public Task InsertAsync(StockLevel level, CancellationToken ct) { Levels[level.Id] = level; return Task.CompletedTask; }
}

internal class FakeItemRepo : IItemRepository
{
    public Dictionary<Guid, Item> Items { get; } = new();
    public Task<Item?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Items.TryGetValue(id, out var i) ? i : null);
    public Task<Item?> GetBySkuAsync(Guid tenantId, string sku, CancellationToken ct) =>
        Task.FromResult(Items.Values.FirstOrDefault(i => i.TenantId == tenantId && i.Sku == sku));
    public Task<Item?> GetByBarcodeAsync(Guid tenantId, string barcode, CancellationToken ct) =>
        Task.FromResult(barcode == null ? null : Items.Values.FirstOrDefault(i => i.TenantId == tenantId && i.Barcode == barcode));
    public Task<IReadOnlyList<Item>> ListAsync(Guid tenantId, Guid? companyId, Guid? categoryId, bool includeInactive, int skip, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Item>>(Items.Values.Where(i => i.TenantId == tenantId).ToList());
    public Task InsertAsync(Item item, CancellationToken ct) { Items[item.Id] = item; return Task.CompletedTask; }
    public Task UpdateAsync(Item item, CancellationToken ct) { Items[item.Id] = item; return Task.CompletedTask; }
}

internal class FakeStockReservationRepo : IStockReservationRepository
{
    private readonly Dictionary<Guid, StockReservation> _items = new();
    public Task<StockReservation?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var r) ? r : null);
    public Task<IReadOnlyList<StockReservation>> ListAsync(Guid tenantId, Guid? itemId, Guid? warehouseId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StockReservation>>(_items.Values
            .Where(r => r.TenantId == tenantId && (itemId == null || r.ItemId == itemId) && (warehouseId == null || r.WarehouseId == warehouseId))
            .ToList());
    public Task<IReadOnlyList<StockReservation>> GetByReferenceAsync(Guid tenantId, string referenceType, Guid referenceId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StockReservation>>(_items.Values
            .Where(r => r.TenantId == tenantId && r.ReferenceType == referenceType && r.ReferenceId == referenceId).ToList());
    public Task InsertAsync(StockReservation r, CancellationToken ct) { _items[r.Id] = r; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct) { _items.Remove(id); return Task.CompletedTask; }
}

internal class FakeNotificationService : INotificationService
{
    public List<ERPSystem.Modules.Notifications.Entities.Notification> Created { get; } = new();
    public Task CreateAsync(Guid tenantId, Guid userId, string type, string title, string message, string? referenceType = null, Guid? referenceId = null)
    {
        Created.Add(new ERPSystem.Modules.Notifications.Entities.Notification
        {
            Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId, Type = type, Title = title, Message = message,
            ReferenceType = referenceType, ReferenceId = referenceId, CreatedAt = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<ERPSystem.Modules.Notifications.Entities.Notification>> ListAsync(Guid tenantId, Guid userId, bool unreadOnly, int skip, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ERPSystem.Modules.Notifications.Entities.Notification>>(Created);
    public Task<int> CountUnreadAsync(Guid tenantId, Guid userId, CancellationToken ct) => Task.FromResult(Created.Count(n => !n.IsRead));
    public Task MarkReadAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct) { return Task.CompletedTask; }
}

internal class FakeEventBus : IEventBus
{
    public List<StockReceivedEvent> Received { get; } = new();
    public List<StockIssuedEvent> Issued { get; } = new();
    public Task PublishAsync<T>(T @event, CancellationToken ct = default) where T : IIntegrationEvent
    {
        if (@event is StockReceivedEvent r) Received.Add(r);
        else if (@event is StockIssuedEvent i) Issued.Add(i);
        return Task.CompletedTask;
    }
}
