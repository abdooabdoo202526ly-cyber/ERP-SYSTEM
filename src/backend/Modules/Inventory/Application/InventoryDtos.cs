using System;
using ERPSystem.Modules.Inventory.Entities;

namespace ERPSystem.Modules.Inventory.Application;

public sealed class CreateItemRequest
{
    // v1.0.25: optional (single-company mode). If null/empty, server picks the first company.
    public Guid? CompanyId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    public ItemType ItemType { get; set; } = ItemType.RawMaterial;
    public CostingMethod CostingMethod { get; set; } = CostingMethod.Average;
    public decimal StandardCost { get; set; }
    public Guid? InventoryAccountId { get; set; }
    public Guid? CogsAccountId { get; set; }
    public Guid? SalesAccountId { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQuantity { get; set; }
}

public sealed class UpdateItemRequest
{
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    public CostingMethod CostingMethod { get; set; }
    public decimal StandardCost { get; set; }
    public Guid? InventoryAccountId { get; set; }
    public Guid? CogsAccountId { get; set; }
    public Guid? SalesAccountId { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQuantity { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ItemResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    public ItemType ItemType { get; set; }
    public CostingMethod CostingMethod { get; set; }
    public decimal AverageCost { get; set; }
    public decimal StandardCost { get; set; }
    public Guid? InventoryAccountId { get; set; }
    public Guid? CogsAccountId { get; set; }
    public Guid? SalesAccountId { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQuantity { get; set; }
    public bool IsActive { get; set; }
}

// ===== Warehouses =====

public sealed class CreateWarehouseRequest
{
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public Guid? ManagerUserId { get; set; }
}

public sealed class UpdateWarehouseRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public Guid? ManagerUserId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class WarehouseResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public Guid? ManagerUserId { get; set; }
    public bool IsActive { get; set; }
}

// ===== UoM =====

public sealed class CreateUnitOfMeasureRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Symbol { get; set; }
}

public sealed class UnitOfMeasureResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Symbol { get; set; }
    public bool IsActive { get; set; }
}

// ===== Categories =====

public sealed class CreateItemCategoryRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
}

public sealed class UpdateItemCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ItemCategoryResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; }
}
