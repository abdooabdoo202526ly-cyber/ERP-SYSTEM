using ERPSystem.Modules.Inventory.Entities;

namespace ERPSystem.Shared.SeedData;

/// <summary>
/// Seed افتراضي للمخزون لكل tenant جديد:
/// - 6 وحدات قياس (UoM)
/// - 5 تصنيفات أصناف (Categories) — شجرية (Raw Materials تحت Materials)
/// يُستدعى من ITenantBootstrap.OnTenantCreatedAsync
/// </summary>
public static class DefaultInventorySeed
{
    public static readonly (string Code, string Name, string? Symbol)[] DefaultUoMs =
    {
        ("pcs", "قطعة", "pcs"),
        ("kg", "كيلوغرام", "kg"),
        ("m", "متر", "m"),
        ("m2", "متر مربع", "m²"),
        ("m3", "متر مكعب", "m³"),
        ("l", "لتر", "l"),
    };

    public static readonly (string Code, string Name, string? Description, string? ParentCode)[] DefaultCategories =
    {
        ("RM", "المواد الخام", "مواد خام تدخل في الإنتاج", null),
        ("FG", "المنتجات النهائية", "منتجات جاهزة للبيع", null),
        ("CON", "مواد استهلاكية", "مواد تُستهلك ولا تُنتج", null),
        ("SVC", "خدمات", "خدمات (لا مخزون فعلي)", null),
        ("OFF", "لوازم مكتبية", "قرطاسية ولوازم إدارية", null),
    };
}
