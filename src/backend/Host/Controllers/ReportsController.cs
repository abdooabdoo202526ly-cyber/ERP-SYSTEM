using ERPSystem.Modules.Reports.Application.Services;
using ERPSystem.Shared.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IProjectReportService _projects;
    private readonly IInventoryReportService _inventory;
    private readonly IFinanceReportService _finance;
    private readonly ITenantContext _tenant;
    public ReportsController(IProjectReportService p, IInventoryReportService i, IFinanceReportService f, ITenantContext t)
    { _projects = p; _inventory = i; _finance = f; _tenant = t; }
    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();

    // ===== Project Reports =====
    [HttpGet("projects/{id:guid}/pnl")]
    public async Task<IActionResult> ProjectPnL(Guid id, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var r = await _projects.GetProjectPnLAsync(TenantId, id, from, to, ct);
        return Ok(r);
    }
    [HttpGet("projects/{id:guid}/budget-vs-actual")]
    public async Task<IActionResult> BudgetVsActual(Guid id, CancellationToken ct)
    {
        var r = await _projects.GetBudgetVsActualAsync(TenantId, id, ct);
        return Ok(r);
    }
    [HttpGet("projects/summary")]
    public async Task<IActionResult> ProjectsSummary([FromQuery] Guid? companyId, CancellationToken ct)
    {
        var r = await _projects.GetProjectsSummaryAsync(TenantId, companyId, ct);
        return Ok(new { count = r.Count, items = r });
    }

    // ===== Inventory Reports =====
    [HttpGet("inventory/valuation")]
    public async Task<IActionResult> InventoryValuation([FromQuery] Guid? companyId, [FromQuery] Guid? warehouseId, CancellationToken ct)
    {
        var r = await _inventory.GetStockValuationAsync(TenantId, companyId, warehouseId, ct);
        return Ok(new { count = r.Count, totalValue = r.Sum(x => x.TotalValue), items = r });
    }
    [HttpGet("inventory/movements")]
    public async Task<IActionResult> MovementHistory(
        [FromQuery] Guid? itemId, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var r = await _inventory.GetMovementHistoryAsync(TenantId, itemId, from, to, skip, take, ct);
        return Ok(new { count = r.Count, items = r });
    }
    [HttpGet("inventory/low-stock")]
    public async Task<IActionResult> LowStock([FromQuery] Guid? companyId, CancellationToken ct)
    {
        var r = await _inventory.GetLowStockAsync(TenantId, companyId, ct);
        return Ok(new { count = r.Count, items = r });
    }
    [HttpGet("inventory/aging")]
    public async Task<IActionResult> StockAging([FromQuery] Guid? companyId, CancellationToken ct)
    {
        var r = await _inventory.GetStockAgingAsync(TenantId, companyId, ct);
        return Ok(new { count = r.Count, items = r });
    }

    // ===== Finance Reports =====
    [HttpGet("finance/trial-balance")]
    public async Task<IActionResult> TrialBalance([FromQuery] Guid? companyId, [FromQuery] DateTime asOf, CancellationToken ct)
    {
        var r = await _finance.GetTrialBalanceAsync(TenantId, companyId, asOf, ct);
        return Ok(r);
    }
    [HttpGet("finance/income-statement")]
    public async Task<IActionResult> IncomeStatement([FromQuery] Guid? companyId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var r = await _finance.GetIncomeStatementAsync(TenantId, companyId, from, to, ct);
        return Ok(r);
    }
    [HttpGet("finance/balance-sheet")]
    public async Task<IActionResult> BalanceSheet([FromQuery] Guid? companyId, [FromQuery] DateTime asOf, CancellationToken ct)
    {
        var r = await _finance.GetBalanceSheetAsync(TenantId, companyId, asOf, ct);
        return Ok(r);
    }

    // ===== Combined Reports (convenience) =====
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] Guid? companyId, CancellationToken ct)
    {
        // Quick overview: project count, low stock count, pending notifications
        var projects = await _projects.GetProjectsSummaryAsync(TenantId, companyId, ct);
        var lowStock = await _inventory.GetLowStockAsync(TenantId, companyId, ct);
        var valuation = await _inventory.GetStockValuationAsync(TenantId, companyId, null, ct);
        return Ok(new
        {
            projects = new { count = projects.Count, totalBudget = projects.Sum(p => p.Budget), totalSpent = projects.Sum(p => p.Spent) },
            inventory = new { lowStockCount = lowStock.Count, totalStockValue = valuation.Sum(v => v.TotalValue) },
        });
    }
}
