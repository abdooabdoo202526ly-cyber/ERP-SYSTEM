using System.Collections.Concurrent;

namespace ERPSystem.Shared.SeedData;

/// <summary>
/// Static state tracker for RealisticSeedHostedService (DEC-069).
/// Exposes progress via /api/debug/seed-status endpoint.
///
/// DEC-071: Added per-step error + count tracking so we can SEE
/// which step is failing silently (DEC-069 caught exceptions silently).
/// </summary>
public static class SeedDebugState
{
    public static bool ServiceConstructed { get; set; }
    public static bool ExecuteAsyncCalled { get; set; }
    public static bool SeedEnabled { get; set; }
    public static bool ConnectivityCheckPassed { get; set; }
    public static Guid? TenantId { get; set; }
    public static string CurrentStep { get; set; } = "(none)";
    public static DateTime? StartedAt { get; set; }
    public static DateTime? CompletedAt { get; set; }
    public static string? LastError { get; set; }

    // DEC-071: Per-step tracking (concurrent for thread-safety)
    public static ConcurrentDictionary<string, int> StepRecordCounts { get; } = new();
    public static ConcurrentDictionary<string, string> StepErrors { get; } = new();
    public static ConcurrentDictionary<string, double> StepDurationsSeconds { get; } = new();

    // Reset all step tracking (called at start of ExecuteAsync)
    public static void ResetStepTracking()
    {
        StepRecordCounts.Clear();
        StepErrors.Clear();
        StepDurationsSeconds.Clear();
        LastError = null;
    }
}
