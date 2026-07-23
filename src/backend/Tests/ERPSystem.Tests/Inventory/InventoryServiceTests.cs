using ERPSystem.Modules.Inventory.Application;
using ERPSystem.Modules.Inventory.Application.Services;
using ERPSystem.Modules.Inventory.Entities;
using ERPSystem.Modules.Inventory.Infrastructure;
using ERPSystem.Tests.Fakes;
using FluentAssertions;
using TaskStatus = System.Threading.Tasks.TaskStatus;

namespace ERPSystem.Tests.Inventory;

public class ItemServiceTests
{
    private static (ItemService svc, FakeItemRepository repo) Build() => (new ItemService(new FakeItemRepository(), new FakeCompanyService()), new FakeItemRepository());

    [Fact]
    public async Task Create_DefaultsToAverageCostMethod_ZeroStandard()
    {
        var (svc, _) = Build();
        var r = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateItemRequest
        {
            CompanyId = Guid.NewGuid(), Sku = "SKU-001", Name = "صنف 1"
        }, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.CostingMethod.Should().Be(CostingMethod.Average, "default = Average");
        r.Value!.AverageCost.Should().Be(0);
        r.Value!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_DuplicateSku_Fails()
    {
        var (svc, _) = Build();
        var t = Guid.NewGuid();
        await svc.CreateAsync(t, Guid.NewGuid(), new CreateItemRequest { CompanyId = Guid.NewGuid(), Sku = "DUP", Name = "A" }, CancellationToken.None);
        var r = await svc.CreateAsync(t, Guid.NewGuid(), new CreateItemRequest { CompanyId = Guid.NewGuid(), Sku = "DUP", Name = "B" }, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(InventoryErrorCode.AlreadyExists);
    }

    [Fact]
    public async Task Create_DuplicateBarcode_Fails()
    {
        var (svc, _) = Build();
        var t = Guid.NewGuid();
        await svc.CreateAsync(t, Guid.NewGuid(), new CreateItemRequest { CompanyId = Guid.NewGuid(), Sku = "A", Barcode = "BC-1", Name = "X" }, CancellationToken.None);
        var r = await svc.CreateAsync(t, Guid.NewGuid(), new CreateItemRequest { CompanyId = Guid.NewGuid(), Sku = "B", Barcode = "BC-1", Name = "Y" }, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(InventoryErrorCode.AlreadyExists);
    }

    [Fact]
    public async Task Deactivate_SetsIsActiveFalse()
    {
        var (svc, _) = Build();
        var t = Guid.NewGuid();
        var c = await svc.CreateAsync(t, Guid.NewGuid(), new CreateItemRequest { CompanyId = Guid.NewGuid(), Sku = "X", Name = "Y" }, CancellationToken.None);
        var r = await svc.DeactivateAsync(t, Guid.NewGuid(), c.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        var fetched = await svc.GetByIdAsync(t, c.Value!.Id, CancellationToken.None);
        fetched.Value!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_WrongTenant_Fails()
    {
        var (svc, _) = Build();
        var c = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateItemRequest { CompanyId = Guid.NewGuid(), Sku = "X", Name = "Y" }, CancellationToken.None);
        var r = await svc.GetByIdAsync(Guid.NewGuid(), c.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
    }
}

public class WarehouseServiceTests
{
    [Fact]
    public async Task Create_DefaultsActive_WithCompany()
    {
        var svc = new WarehouseService(new FakeWarehouseRepository());
        var r = await svc.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), new CreateWarehouseRequest
        {
            CompanyId = Guid.NewGuid(), Code = "WH-1", Name = "المخزن الرئيسي", Location = "طرابلس"
        }, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.IsActive.Should().BeTrue();
        r.Value!.Location.Should().Be("طرابلس");
    }

    [Fact]
    public async Task Create_DuplicateCode_Fails()
    {
        var repo = new FakeWarehouseRepository();
        var svc = new WarehouseService(repo);
        var t = Guid.NewGuid();
        await svc.CreateAsync(t, Guid.NewGuid(), new CreateWarehouseRequest { CompanyId = Guid.NewGuid(), Code = "WH", Name = "A" }, CancellationToken.None);
        var r = await svc.CreateAsync(t, Guid.NewGuid(), new CreateWarehouseRequest { CompanyId = Guid.NewGuid(), Code = "WH", Name = "B" }, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
    }
}

public class UoMServiceTests
{
    [Fact]
    public async Task Create_DefaultsActive()
    {
        var svc = new UnitOfMeasureService(new FakeUnitOfMeasureRepository());
        var r = await svc.CreateAsync(Guid.NewGuid(), new CreateUnitOfMeasureRequest { Code = "pcs", Name = "قطعة" }, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_DuplicateCode_Fails()
    {
        var repo = new FakeUnitOfMeasureRepository();
        var svc = new UnitOfMeasureService(repo);
        var t = Guid.NewGuid();
        await svc.CreateAsync(t, new CreateUnitOfMeasureRequest { Code = "kg", Name = "كجم" }, CancellationToken.None);
        var r = await svc.CreateAsync(t, new CreateUnitOfMeasureRequest { Code = "kg", Name = "كيلو" }, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task List_ReturnsAllActiveUoMs()
    {
        var repo = new FakeUnitOfMeasureRepository();
        var svc = new UnitOfMeasureService(repo);
        var t = Guid.NewGuid();
        await svc.CreateAsync(t, new CreateUnitOfMeasureRequest { Code = "pcs", Name = "قطعة" }, CancellationToken.None);
        await svc.CreateAsync(t, new CreateUnitOfMeasureRequest { Code = "kg", Name = "كجم" }, CancellationToken.None);
        await svc.CreateAsync(t, new CreateUnitOfMeasureRequest { Code = "m", Name = "متر" }, CancellationToken.None);
        var r = await svc.ListAsync(t, false, CancellationToken.None);
        r.Value!.Count.Should().Be(3);
    }
}

public class ItemCategoryServiceTests
{
    [Fact]
    public async Task Create_RootCategory_Succeeds()
    {
        var svc = new ItemCategoryService(new FakeItemCategoryRepository());
        var r = await svc.CreateAsync(Guid.NewGuid(), new CreateItemCategoryRequest { Code = "RM", Name = "مواد خام" }, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.ParentId.Should().BeNull();
    }

    [Fact]
    public async Task Create_ChildCategory_WithValidParent_Succeeds()
    {
        var repo = new FakeItemCategoryRepository();
        var svc = new ItemCategoryService(repo);
        var t = Guid.NewGuid();
        var parent = await svc.CreateAsync(t, new CreateItemCategoryRequest { Code = "RM", Name = "مواد" }, CancellationToken.None);
        var child = await svc.CreateAsync(t, new CreateItemCategoryRequest
        {
            Code = "RM-METAL", Name = "معادن", ParentId = parent.Value!.Id
        }, CancellationToken.None);
        child.Succeeded.Should().BeTrue();
        child.Value!.ParentId.Should().Be(parent.Value.Id);
    }

    [Fact]
    public async Task Create_ChildCategory_WithInvalidParent_Fails()
    {
        var svc = new ItemCategoryService(new FakeItemCategoryRepository());
        var r = await svc.CreateAsync(Guid.NewGuid(), new CreateItemCategoryRequest
        {
            Code = "X", Name = "X", ParentId = Guid.NewGuid()
        }, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(InventoryErrorCode.NotFound);
    }

    [Fact]
    public async Task GetChildren_ReturnsDirectChildren_NotGrandchildren()
    {
        var repo = new FakeItemCategoryRepository();
        var svc = new ItemCategoryService(repo);
        var t = Guid.NewGuid();
        var parent = await svc.CreateAsync(t, new CreateItemCategoryRequest { Code = "P", Name = "P" }, CancellationToken.None);
        await svc.CreateAsync(t, new CreateItemCategoryRequest { Code = "C1", Name = "C1", ParentId = parent.Value!.Id }, CancellationToken.None);
        await svc.CreateAsync(t, new CreateItemCategoryRequest { Code = "C2", Name = "C2", ParentId = parent.Value.Id }, CancellationToken.None);
        var r = await svc.GetChildrenAsync(parent.Value.Id, CancellationToken.None);
        r.Value!.Count.Should().Be(2);
    }
}

public class InventoryBootstrapperTests
{
    [Fact]
    public async Task EnsureDefaultUoMsAndCategoriesAsync_Seeds6UoMs_5Categories()
    {
        var uomRepo = new FakeUnitOfMeasureRepository();
        var catRepo = new FakeItemCategoryRepository();
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<InventoryBootstrapper>();
        var boot = new InventoryBootstrapper(uomRepo, catRepo, logger);
        var t = Guid.NewGuid();
        await boot.EnsureDefaultUoMsAndCategoriesAsync(t, CancellationToken.None);
        var uoms = await uomRepo.ListAsync(t, true, CancellationToken.None);
        var cats = await catRepo.ListAsync(t, true, CancellationToken.None);
        uoms.Count.Should().Be(6, "pcs, kg, m, m², m³, liter");
        cats.Count.Should().Be(5, "RM, FG, CON, SVC, OFF");
        uoms.Should().Contain(u => u.Code == "m2" && u.Symbol == "m²");
        cats.Should().Contain(c => c.Code == "RM" && c.Name == "المواد الخام");
    }

    [Fact]
    public async Task EnsureDefaultUoMsAndCategoriesAsync_IsIdempotent()
    {
        var uomRepo = new FakeUnitOfMeasureRepository();
        var catRepo = new FakeItemCategoryRepository();
        var boot = new InventoryBootstrapper(uomRepo, catRepo, new Microsoft.Extensions.Logging.Abstractions.NullLogger<InventoryBootstrapper>());
        var t = Guid.NewGuid();
        await boot.EnsureDefaultUoMsAndCategoriesAsync(t, CancellationToken.None);
        await boot.EnsureDefaultUoMsAndCategoriesAsync(t, CancellationToken.None);
        var uoms = await uomRepo.ListAsync(t, true, CancellationToken.None);
        uoms.Count.Should().Be(6, "لا يجب أن يضيف مكررات");
    }
}

// ============== Fakes ==============

internal class FakeItemRepository : IItemRepository
{
    private readonly Dictionary<Guid, Item> _items = new();
    public Task<Item?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var i) ? i : null);
    public Task<Item?> GetBySkuAsync(Guid tenantId, string sku, CancellationToken ct) =>
        Task.FromResult(_items.Values.FirstOrDefault(i => i.TenantId == tenantId && i.Sku == sku));
    public Task<Item?> GetByBarcodeAsync(Guid tenantId, string barcode, CancellationToken ct) =>
        Task.FromResult(_items.Values.FirstOrDefault(i => i.TenantId == tenantId && i.Barcode == barcode));
    public Task<IReadOnlyList<Item>> ListAsync(Guid tenantId, Guid? companyId, Guid? categoryId, bool includeInactive, int skip, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Item>>(_items.Values
            .Where(i => i.TenantId == tenantId && (includeInactive || i.IsActive)
                && (companyId == null || i.CompanyId == companyId)
                && (categoryId == null || i.CategoryId == categoryId))
            .ToList());
    public Task InsertAsync(Item item, CancellationToken ct) { _items[item.Id] = item; return Task.CompletedTask; }
    public Task UpdateAsync(Item item, CancellationToken ct) { _items[item.Id] = item; return Task.CompletedTask; }
}

internal class FakeWarehouseRepository : IWarehouseRepository
{
    private readonly Dictionary<Guid, Warehouse> _items = new();
    public Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var w) ? w : null);
    public Task<Warehouse?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct) =>
        Task.FromResult(_items.Values.FirstOrDefault(w => w.TenantId == tenantId && w.Code == code));
    public Task<IReadOnlyList<Warehouse>> ListAsync(Guid tenantId, Guid? companyId, bool includeInactive, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Warehouse>>(_items.Values
            .Where(w => w.TenantId == tenantId && (includeInactive || w.IsActive) && (companyId == null || w.CompanyId == companyId)).ToList());
    public Task<IReadOnlyList<Warehouse>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct) // DEC-031
    {
        var idSet = ids.ToHashSet();
        return Task.FromResult<IReadOnlyList<Warehouse>>(_items.Values.Where(w => idSet.Contains(w.Id)).ToList());
    }
    public Task InsertAsync(Warehouse w, CancellationToken ct) { _items[w.Id] = w; return Task.CompletedTask; }
    public Task UpdateAsync(Warehouse w, CancellationToken ct) { _items[w.Id] = w; return Task.CompletedTask; }
}

internal class FakeUnitOfMeasureRepository : IUnitOfMeasureRepository
{
    private readonly Dictionary<Guid, UnitOfMeasure> _items = new();
    public Task<UnitOfMeasure?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var u) ? u : null);
    public Task<UnitOfMeasure?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct) =>
        Task.FromResult(_items.Values.FirstOrDefault(u => u.TenantId == tenantId && u.Code == code));
    public Task<IReadOnlyList<UnitOfMeasure>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<UnitOfMeasure>>(_items.Values
            .Where(u => u.TenantId == tenantId && (includeInactive || u.IsActive)).ToList());
    public Task InsertAsync(UnitOfMeasure u, CancellationToken ct) { _items[u.Id] = u; return Task.CompletedTask; }
    public Task UpdateAsync(UnitOfMeasure u, CancellationToken ct) { _items[u.Id] = u; return Task.CompletedTask; }
}

internal class FakeItemCategoryRepository : IItemCategoryRepository
{
    private readonly Dictionary<Guid, ItemCategory> _items = new();
    public Task<ItemCategory?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var c) ? c : null);
    public Task<ItemCategory?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct) =>
        Task.FromResult(_items.Values.FirstOrDefault(c => c.TenantId == tenantId && c.Code == code));
    public Task<IReadOnlyList<ItemCategory>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ItemCategory>>(_items.Values
            .Where(c => c.TenantId == tenantId && (includeInactive || c.IsActive)).ToList());
    public Task<IReadOnlyList<ItemCategory>> ListChildrenAsync(Guid parentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ItemCategory>>(_items.Values.Where(c => c.ParentId == parentId).ToList());
    public Task InsertAsync(ItemCategory c, CancellationToken ct) { _items[c.Id] = c; return Task.CompletedTask; }
    public Task UpdateAsync(ItemCategory c, CancellationToken ct) { _items[c.Id] = c; return Task.CompletedTask; }
}
