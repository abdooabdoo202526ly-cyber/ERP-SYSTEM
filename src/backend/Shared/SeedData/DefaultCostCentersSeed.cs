using ERPSystem.Modules.Companies.Application.Services;
using ERPSystem.Modules.Companies.Entities;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Shared.SeedData;

/// <summary>
/// v1.0.31: seed cost centers افتراضية لكل tenant جديد.
/// بدل ما الـ user يشوف combobox فاضي، نضيف 6 مراكز افتراضية (Admin/HR/IT/Sales/Marketing/Operations).
/// </summary>
public static class DefaultCostCentersSeed
{
    public record SeedEntry(string Code, string Name, CostCenterType Type, string? ParentCode = null);

    public static readonly SeedEntry[] Defaults = new[]
    {
        new SeedEntry("CC-ADMIN", "الإدارة العامة", CostCenterType.Department),
        new SeedEntry("CC-HR", "الموارد البشرية", CostCenterType.Department),
        new SeedEntry("CC-IT", "تقنية المعلومات", CostCenterType.Department),
        new SeedEntry("CC-SALES", "المبيعات", CostCenterType.Department),
        new SeedEntry("CC-MKT", "التسويق", CostCenterType.Department),
        new SeedEntry("CC-OPS", "العمليات", CostCenterType.Department),
        new SeedEntry("CC-FIN", "المالية", CostCenterType.Department),
        new SeedEntry("PROJ-DEFAULT", "مشاريع عامة", CostCenterType.Project),
    };

    /// <summary>يضيف الـ cost centers الافتراضية إذا الـ tenant لا يحوي ولا واحدة. Idempotent.</summary>
    public static async Task<int> EnsureDefaultsForTenantAsync(
        Guid tenantId,
        Guid companyId,
        ICostCenterService service,
        ILogger logger,
        CancellationToken ct)
    {
        var existing = await service.ListAsync(tenantId, companyId, type: null, includeInactive: true, ct);
        if (existing.Succeeded && existing.Value != null && existing.Value.Count > 0)
        {
            logger.LogInformation("CostCentersSeed: tenant {TenantId} already has {Count} cost centers — skipping", tenantId, existing.Value.Count);
            return 0;
        }

        var idByCode = new Dictionary<string, Guid>();
        var added = 0;
        // parent first
        foreach (var e in Defaults.Where(x => x.ParentCode == null))
        {
            var r = await service.CreateAsync(tenantId, new CreateCostCenterRequest
            {
                CompanyId = companyId,
                Code = e.Code,
                Name = e.Name,
                Type = e.Type,
            }, ct);
            if (r.Succeeded && r.Value != null)
            {
                idByCode[e.Code] = r.Value.Id;
                added++;
                logger.LogInformation("CostCentersSeed: created {Code} for tenant {TenantId}", e.Code, tenantId);
            }
        }
        // children (none for now)
        foreach (var e in Defaults.Where(x => x.ParentCode != null))
        {
            // resolve parent
            if (e.ParentCode != null && idByCode.TryGetValue(e.ParentCode, out var parentId))
            {
                var r = await service.CreateAsync(tenantId, new CreateCostCenterRequest
                {
                    CompanyId = companyId,
                    Code = e.Code,
                    Name = e.Name,
                    Type = e.Type,
                    ParentId = parentId,
                }, ct);
                if (r.Succeeded && r.Value != null) { added++; }
            }
        }
        logger.LogInformation("CostCentersSeed: seeded {Count} cost centers for tenant {TenantId}", added, tenantId);
        return added;
    }
}
