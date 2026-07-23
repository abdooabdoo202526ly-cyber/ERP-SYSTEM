using ERPSystem.Shared.Events.Application.Services;
using ERPSystem.Shared.Events.Infrastructure;
using ERPSystem.Shared.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

[ApiController]
[Route("api/events")]
[Authorize]
public class EventsController : ControllerBase
{
    private readonly IOutboxRepository _outbox;
    private readonly ITenantContext _tenant;
    public EventsController(IOutboxRepository outbox, ITenantContext tenant) { _outbox = outbox; _tenant = tenant; }
    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();

    /// <summary>Admin: list pending (unprocessed) events for the tenant</summary>
    [HttpGet("outbox")]
    public async Task<IActionResult> ListPending([FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        if (take is < 1 or > 200) take = 50;
        var list = await _outbox.ListAllAsync(TenantId, unprocessedOnly: true, skip, take, ct);
        return Ok(new { count = list.Count, items = list });
    }

    /// <summary>Admin: list processed events (audit trail)</summary>
    [HttpGet("processed")]
    public async Task<IActionResult> ListProcessed([FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        if (take is < 1 or > 200) take = 50;
        var list = await _outbox.ListAllAsync(TenantId, unprocessedOnly: false, skip, take, ct);
        return Ok(new { count = list.Count, items = list });
    }

    /// <summary>Admin: count of unprocessed events for the tenant</summary>
    [HttpGet("pending-count")]
    public async Task<IActionResult> PendingCount(CancellationToken ct)
    {
        var c = await _outbox.CountPendingAsync(TenantId, ct);
        return Ok(new { count = c });
    }

    /// <summary>Admin: manual retry — resets retry_count so the processor picks it up again</summary>
    [HttpPost("retry/{id:guid}")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var evt = await _outbox.GetByIdAsync(id, ct);
        if (evt == null || evt.TenantId != TenantId) return NotFound();
        await _outbox.ResetForRetryAsync(id, ct);
        return NoContent();
    }
}
