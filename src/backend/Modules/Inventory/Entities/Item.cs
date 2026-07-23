using System;

namespace ERPSystem.Modules.Inventory.Entities;

public enum ItemType
{
    RawMaterial = 1,
    FinishedGood = 2,
    Consumable = 3,
    Service = 4
}

public enum CostingMethod
{
    FIFO = 1,
    LIFO = 2,
    Average = 3,    // default — moving weighted average
    Standard = 4
}

/// <summary>
/// صنف في المخزون — مملوك لشركة (multi-company) ومرتبط بحسابات Finance.
/// عند Post stock movement، AverageCost يُحدّث بـ moving weighted average.
/// </summary>
public class Item
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }

    public string Sku { get; set; } = string.Empty;        // "SKU-001" — unique per tenant
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid? CategoryId { get; set; }
    public Guid? UnitOfMeasureId { get; set; }

    public ItemType ItemType { get; set; } = ItemType.RawMaterial;
    public CostingMethod CostingMethod { get; set; } = CostingMethod.Average;

    public decimal AverageCost { get; set; }    // moving weighted average (يُحدّث على stock receipt)
    public decimal StandardCost { get; set; }  // للـ CostingMethod.Standard

    /// <summary>حساب المخزون (1300 افتراضياً) — يُستخدم في Journal Entry عند stock receipt</summary>
    public Guid? InventoryAccountId { get; set; }

    /// <summary>حساب COGS (5100 افتراضياً) — يُستخدم عند stock issue</summary>
    public Guid? CogsAccountId { get; set; }

    /// <summary>حساب الإيرادات (4100 افتراضياً) — يُستخدم عند sale</summary>
    public Guid? SalesAccountId { get; set; }

    public decimal ReorderLevel { get; set; }     // 0 = disabled
    public decimal ReorderQuantity { get; set; } // 0 = disabled

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
