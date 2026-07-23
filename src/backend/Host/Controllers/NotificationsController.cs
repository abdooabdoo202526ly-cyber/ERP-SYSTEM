using System.Security.Claims;
using ERPSystem.Modules.Notifications.Application.Services;
using ERPSystem.Shared.MultiTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

[ApiController]
[Route("api/inventory/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _service;
    private readonly ITenantContext _tenant;
    public NotificationsController(INotificationService s, ITenantContext t) { _service = s; _tenant = t; }
    private Guid TenantId => _tenant.TenantId ?? throw new UnauthorizedAccessException();
    private Guid UserId => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool unreadOnly = false, [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var list = await _service.ListAsync(TenantId, UserId, unreadOnly, skip, take, ct);
        return Ok(list);
    }
    [HttpGet("unread")]
    public async Task<IActionResult> Unread([FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var list = await _service.ListAsync(TenantId, UserId, true, skip, take, ct);
        return Ok(new { count = list.Count, items = list });
    }
    [HttpPost("{id:guid}/mark-read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await _service.MarkReadAsync(TenantId, UserId, id, ct);
        return NoContent();
    }
}
