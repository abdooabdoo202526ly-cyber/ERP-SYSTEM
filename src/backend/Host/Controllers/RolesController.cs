// v1.0.32: Roles Management Controller — CRUD + User-Role assignment + Permissions catalog

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ERPSystem.Modules.Identity.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

[ApiController]
[Route("api/identity")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IRoleManagementService _service;

    public RolesController(IRoleManagementService service) => _service = service;

    private Guid GetTenantId()
    {
        var t = User.FindFirstValue("tenantId") ?? User.FindFirstValue("TenantId");
        return Guid.TryParse(t, out var id) ? id : Guid.Empty;
    }

    /// <summary>قائمة الأدوار (Roles) داخل الـ tenant الحالي</summary>
    [HttpGet("roles")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();
        var roles = await _service.ListRolesAsync(tenantId, ct);
        return Ok(roles);
    }

    /// <summary>تفاصيل دور</summary>
    [HttpGet("roles/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var role = await _service.GetRoleAsync(id, ct);
        return Ok(role);
    }

    /// <summary>إنشاء دور جديد</summary>
    [HttpPost("roles")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest req, CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();
        var role = await _service.CreateRoleAsync(tenantId, req.Name, req.Description, ct);
        return Ok(role);
    }

    /// <summary>تعديل دور</summary>
    [HttpPut("roles/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleRequest req, CancellationToken ct)
    {
        var role = await _service.UpdateRoleAsync(id, req.Name, req.Description, ct);
        return Ok(role);
    }

    /// <summary>حذف دور (يفك ارتباط المستخدمين أولاً)</summary>
    [HttpDelete("roles/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteRoleAsync(id, ct);
        return NoContent();
    }

    /// <summary>أدوار مستخدم معين</summary>
    [HttpGet("users/{userId:guid}/roles")]
    public async Task<IActionResult> ListUserRoles(Guid userId, CancellationToken ct)
    {
        var roles = await _service.ListUserRolesAsync(userId, ct);
        return Ok(roles);
    }

    /// <summary>إسناد دور إلى مستخدم</summary>
    [HttpPost("users/{userId:guid}/roles/{roleId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignRole(Guid userId, Guid roleId, CancellationToken ct)
    {
        await _service.AssignRoleAsync(userId, roleId, ct);
        return NoContent();
    }

    /// <summary>إلغاء دور من مستخدم</summary>
    [HttpDelete("users/{userId:guid}/roles/{roleId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveRole(Guid userId, Guid roleId, CancellationToken ct)
    {
        await _service.RemoveRoleAsync(userId, roleId, ct);
        return NoContent();
    }

    /// <summary>قائمة الـ permissions المتاحة في النظام (catalog)</summary>
    [HttpGet("permissions")]
    public async Task<IActionResult> ListPermissions(CancellationToken ct)
    {
        var perms = await _service.ListAvailablePermissionsAsync(ct);
        return Ok(perms);
    }

    /// <summary>قائمة المستخدمين داخل الـ tenant مع أدوارهم</summary>
    [HttpGet("users")]
    public async Task<IActionResult> ListUsers(CancellationToken ct)
    {
        var tenantId = GetTenantId();
        if (tenantId == Guid.Empty) return Unauthorized();
        var users = await _service.ListUsersAsync(tenantId, ct);
        return Ok(users);
    }
}

public record CreateRoleRequest(string Name, string Description);
public record UpdateRoleRequest(string Name, string Description);
