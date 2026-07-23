using Microsoft.AspNetCore.Authorization;
using ERPSystem.Shared.SeedData;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

/// <summary>
/// Debug endpoints — exposes internal state for diagnostics (DEC-069/DEC-071).
/// Admin-only.
/// </summary>
[ApiController]
[Route("api/debug")]
[Authorize(Roles = "Admin")]
public class DebugController : ControllerBase
{
    [HttpGet("seed-status")]
    public IActionResult GetSeedStatus()
    {
        return Ok(new
        {
            seed_debug = new
            {
                service_constructed = SeedDebugState.ServiceConstructed,
                execute_async_called = SeedDebugState.ExecuteAsyncCalled,
                seed_enabled = SeedDebugState.SeedEnabled,
                connectivity_check_passed = SeedDebugState.ConnectivityCheckPassed,
                tenant_id = SeedDebugState.TenantId,
                current_step = SeedDebugState.CurrentStep,
                started_at = SeedDebugState.StartedAt,
                completed_at = SeedDebugState.CompletedAt,
                // DEC-071: Per-step tracking (the actual SQL errors live here)
                step_record_counts = SeedDebugState.StepRecordCounts,
                step_errors = SeedDebugState.StepErrors,
                step_durations_seconds = SeedDebugState.StepDurationsSeconds,
                // DEC-069: Legacy single-error field (last failure)
                last_error = SeedDebugState.LastError
            }
        });
    }
}
