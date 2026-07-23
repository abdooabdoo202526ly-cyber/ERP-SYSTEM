using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Application.Services;
using ERPSystem.Shared.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

/// <summary>
/// Finance Reports API — بديل للـ Reports module القديم.
/// - General Ledger per-account
/// - Balance Sheet
/// - Cash Flow (Indirect)
/// - AP Aging
/// كل الـ endpoints تحت /api/finance/* و [Authorize].
/// </summary>
[ApiController]
[Route("api/finance")]
[Authorize]
public class FinanceReportsController : ControllerBase
{
    private readonly IGeneralLedgerReportService _gl;
    private readonly IBalanceSheetService _bs;
    private readonly ICashFlowService _cf;
    private readonly IAPAgingService _ap;
    private readonly ITenantContext _tenant;

    public FinanceReportsController(
        IGeneralLedgerReportService gl, IBalanceSheetService bs, ICashFlowService cf, IAPAgingService ap,
        ITenantContext tenant)
    {
        _gl = gl; _bs = bs; _cf = cf; _ap = ap; _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();

    [HttpGet("general-ledger")]
    public async Task<IActionResult> GeneralLedger(
        [FromQuery] Guid accountId,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        if (accountId == Guid.Empty) return BadRequest("accountId مطلوب.");
        var r = await _gl.GetAccountLedgerAsync(TenantId, accountId, from, to, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpGet("balance-sheet")]
    public async Task<IActionResult> BalanceSheet([FromQuery] DateTime asOf, CancellationToken ct)
    {
        var r = await _bs.GetAsync(TenantId, asOf, ct);
        return Ok(r);
    }

    [HttpGet("cash-flow")]
    public async Task<IActionResult> CashFlow(
        [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        if (to < from) return BadRequest("to يجب أن يكون >= from.");
        var r = await _cf.GetAsync(TenantId, from, to, ct);
        return Ok(r);
    }

    [HttpGet("aging/ap")]
    public async Task<IActionResult> APAging([FromQuery] DateTime asOf, CancellationToken ct)
    {
        var r = await _ap.GetAsync(TenantId, asOf, ct);
        return Ok(r);
    }
}
