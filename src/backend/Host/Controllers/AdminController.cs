using System.Collections.Concurrent;
using Dapper;
using ERPSystem.Modules.Finance.Application;
using ERPSystem.Modules.Finance.Application.Services;
using ERPSystem.Modules.Procurement.Application.Services;
using ERPSystem.Modules.Procurement.Entities;
using ERPSystem.Modules.Procurement.Infrastructure;
using ERPSystem.Shared.Infrastructure;
using ERPSystem.Shared.MultiTenancy;
using ERPSystem.Shared.SeedData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPSystem.Host.Controllers;

/// <summary>
/// Admin endpoints — manual triggers for sensitive operations.
/// Sprint-4 Day 2 (DEC-042). Admin-only.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AdminController> _logger;

    // In-memory job tracker (per-process; resets on restart).
    // For production, swap with DB-backed job table or Hangfire.
    private static readonly ConcurrentDictionary<Guid, SeedJobStatus> _jobs = new();

    public AdminController(IServiceProvider serviceProvider, ILogger<AdminController> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// تشغيل يدوي لـ AlFajr scenario seeder (~5K records — safe).
    /// يعيد jobId فوراً، التنفيذ في background.
    /// </summary>
    [HttpPost("seed/alfajr")]
    public IActionResult TriggerAlFajrSeed()
    {
        var jobId = Guid.NewGuid();
        _jobs[jobId] = new SeedJobStatus(jobId, "alfajr", "queued", DateTime.UtcNow);

        _ = Task.Run(async () =>
        {
            await RunSeedJobAsync(jobId, "alfajr", async (sp, ct) =>
            {
                var seeder = sp.GetRequiredService<ScenarioSeederHostedService>();
                await seeder.StartAsync(ct);
            });
        });

        return Accepted(new
        {
            jobId,
            scenario = "alfajr",
            status = "started",
            statusEndpoint = $"/api/admin/seed/status/{jobId}",
            note = "Background execution. Poll status endpoint for progress."
        });
    }

    /// <summary>
    /// تشغيل يدوي لـ AlBurj scenario seeder (~30K records — DEC-009 prevention).
    /// NOT YET IMPLEMENTED — الـ AlBurjSeeder class was removed in DEC-009 cleanup.
    /// Use POST /api/admin/seed/alfajr for the supported scenario.
    /// </summary>
    [HttpPost("seed/alburj")]
    public IActionResult TriggerAlBurjSeed()
    {
        _logger.LogWarning("AlBurj seed requested but class not implemented (DEC-009 prevention). Use /seed/alfajr.");
        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            error = "alburj_seeder_not_implemented",
            message = "AlBurjSeeder was removed after DEC-009 incident (30K records flooded the DB). " +
                      "Use POST /api/admin/seed/alfajr for the supported scenario. " +
                      "Implementing AlBurjSeeder safely is tracked in Sprint-5+ roadmap.",
            alternative = "/api/admin/seed/alfajr"
        });
    }

    /// <summary>
    /// استعلام حالة job معين.
    /// </summary>
    [HttpGet("seed/status/{jobId}")]
    public IActionResult GetSeedStatus(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var status))
        {
            return NotFound(new { jobId, error = "job_not_found_or_already_cleaned" });
        }
        return Ok(status);
    }

    /// <summary>
    /// قائمة آخر 20 job (للمسؤول).
    /// </summary>
    [HttpGet("seed/jobs")]
    public IActionResult ListSeedJobs()
    {
        var recent = _jobs.Values
            .OrderByDescending(j => j.StartedAt)
            .Take(20)
            .ToList();
        return Ok(new { count = recent.Count, jobs = recent });
    }

    // ============ Private helpers ============

    private async Task RunSeedJobAsync(Guid jobId, string scenario, Func<IServiceProvider, CancellationToken, Task> work)
    {
        _jobs[jobId] = _jobs[jobId] with { Status = "running", StartedAt = DateTime.UtcNow };
        _logger.LogInformation("Seed job {JobId} ({Scenario}) started", jobId, scenario);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var scope = _serviceProvider.CreateScope();
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            await work(scope.ServiceProvider, cts.Token);

            sw.Stop();
            _jobs[jobId] = _jobs[jobId] with { Status = "completed", FinishedAt = DateTime.UtcNow, DurationSeconds = sw.Elapsed.TotalSeconds };
            _logger.LogInformation("Seed job {JobId} ({Scenario}) completed in {Sec}s", jobId, scenario, sw.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _jobs[jobId] = _jobs[jobId] with
            {
                Status = "failed",
                FinishedAt = DateTime.UtcNow,
                DurationSeconds = sw.Elapsed.TotalSeconds,
                Error = ex.Message
            };
            _logger.LogError(ex, "Seed job {JobId} ({Scenario}) FAILED after {Sec}s", jobId, scenario, sw.Elapsed.TotalSeconds);
        }
    }

    // ============ Status record ============

    private record SeedJobStatus(
        Guid JobId,
        string Scenario,
        string Status,
        DateTime StartedAt,
        DateTime? FinishedAt = null,
        double? DurationSeconds = null,
        string? Error = null
    );

    // ============================================================
    // DEC-076: Finance Backfill (one-shot, idempotent)
    // Fire-and-forget pattern — returns jobId immediately, processes in background
    // 1. Opening Balance JE (Dr Cash 5M / Cr Capital 5M) — idempotent via reference check
    // 2. Bill AP Posting — for each bill without journal_entry_id, create + post JE
    // ============================================================
    [HttpPost("finance/backfill")]
    public IActionResult TriggerFinanceBackfill([FromServices] ITenantContext tenantCtx)
    {
        var tenantId = tenantCtx.TenantId ?? throw new UnauthorizedAccessException("Tenant context missing");
        var jobId = Guid.NewGuid();
        _jobs[jobId] = new SeedJobStatus(jobId, "finance-backfill", "queued", DateTime.UtcNow);

        _ = Task.Run(async () =>
        {
            await RunSeedJobAsync(jobId, "finance-backfill", async (sp, ct) =>
            {
                var journalSvc = sp.GetRequiredService<IJournalEntryService>();
                var billRepo = sp.GetRequiredService<IVendorBillRepository>();
                var db = sp.GetRequiredService<IDbConnectionFactory>();
                var userId = Guid.Empty;
                var summary = new
                {
                    opening_balance_created = false,
                    opening_balance_skipped = false,
                    bills_processed = 0,
                    bills_skipped = 0,
                    bills_with_errors = 0,
                    total_debits_posted = 0m,
                    total_credits_posted = 0m,
                    errors = new List<string>()
                };

                using var conn = await db.CreateOltpConnectionAsync(ct);

                // 1) Opening Balance JE — idempotent via reference check
                var existingOpening = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                    "SELECT COUNT(*) FROM journal_entries WHERE tenant_id = @T AND reference LIKE 'OPENING-BALANCE%'",
                    new { T = tenantId }, cancellationToken: ct));

                if (existingOpening > 0)
                {
                    summary = summary with { opening_balance_skipped = true };
                    _logger.LogInformation("DEC-076: Opening balance already exists, skipping");
                }
                else
                {
                    var accts = await conn.QueryAsync<(string Code, Guid AcctId)>(new CommandDefinition(
                        "SELECT code, id FROM accounts WHERE tenant_id = @T AND code IN ('1210', '3100')",
                        new { T = tenantId }, cancellationToken: ct));
                    Guid? cashId = null, capitalId = null;
                    foreach (var (code, acctId) in accts)
                    {
                        if (code == "1210") cashId = acctId;
                        if (code == "3100") capitalId = acctId;
                    }

                    if (cashId != null && capitalId != null)
                    {
                        const decimal openingAmount = 5_000_000m;
                        var draft = await journalSvc.CreateDraftAsync(tenantId, userId, new PostJournalEntryRequest
                        {
                            EntryDate = new DateTime(2024, 1, 1),
                            Description = "Opening Balance — Owner's Capital Investment",
                            Reference = "OPENING-BALANCE-DEC-076",
                            Lines = new List<PostJournalLineRequest>
                            {
                                new() { AccountId = cashId.Value, Debit = openingAmount, Credit = 0m,
                                        Description = "Cash on hand (opening)" },
                                new() { AccountId = capitalId.Value, Debit = 0m, Credit = openingAmount,
                                        Description = "Owner's capital contribution" }
                            }
                        }, ct);

                        if (draft.Succeeded)
                        {
                            var post = await journalSvc.PostAsync(tenantId, userId, draft.Value!.Id, ct);
                            if (post.Succeeded)
                            {
                                summary = summary with { opening_balance_created = true };
                                _logger.LogInformation("DEC-076: Opening balance created ({N} LYD)", openingAmount);
                            }
                            else { ((List<string>)summary.errors).Add($"Opening balance post failed: {post.Error}"); }
                        }
                        else { ((List<string>)summary.errors).Add($"Opening balance draft failed: {draft.Error}"); }
                    }
                    else
                    {
                        ((List<string>)summary.errors).Add($"Opening balance skipped — accounts missing (1210={cashId}, 3100={capitalId})");
                    }
                }

                // 2) Look up Inventory + AP account IDs
                var billAccts = await conn.QueryAsync<(string Code, Guid AcctId)>(new CommandDefinition(
                    "SELECT code, id FROM accounts WHERE tenant_id = @T AND code IN ('1240', '2210')",
                    new { T = tenantId }, cancellationToken: ct));
                Guid? inventoryId = null, apId = null;
                foreach (var (code, acctId) in billAccts)
                {
                    if (code == "1240") inventoryId = acctId;
                    if (code == "2210") apId = acctId;
                }

                if (inventoryId == null || apId == null)
                {
                    ((List<string>)summary.errors).Add($"Bill AP backfill skipped — accounts missing (1240={inventoryId}, 2210={apId})");
                }
                else
                {
                    // 3) Backfill AP JEs for all posted bills without journal_entry_id
                    var bills = await billRepo.ListAsync(tenantId, null, null, null, 0, 200, ct);
                    var billsToProcess = bills.Where(b => b.Status == VendorBillStatus.Posted && (!b.JournalEntryId.HasValue || b.JournalEntryId == Guid.Empty)).ToList();

                    foreach (var bill in billsToProcess)
                    {
                        try
                        {
                            var draft = await journalSvc.CreateDraftAsync(tenantId, userId, new PostJournalEntryRequest
                            {
                                EntryDate = bill.BillDate,
                                Description = $"Vendor Bill {bill.BillNumber} (backfilled)",
                                Reference = $"BILL-{bill.BillNumber}",
                                Lines = new List<PostJournalLineRequest>
                                {
                                    new() { AccountId = inventoryId.Value, Debit = bill.TotalAmount, Credit = 0m,
                                            Description = $"Inventory — Bill {bill.BillNumber}" },
                                    new() { AccountId = apId.Value, Debit = 0m, Credit = bill.TotalAmount,
                                            Description = $"A/P — Bill {bill.BillNumber}" }
                                }
                            }, ct);

                            if (!draft.Succeeded)
                            {
                                ((List<string>)summary.errors).Add($"Bill {bill.BillNumber} draft failed: {draft.Error}");
                                summary = summary with { bills_with_errors = summary.bills_with_errors + 1 };
                                continue;
                            }

                            var post = await journalSvc.PostAsync(tenantId, userId, draft.Value!.Id, ct);
                            if (!post.Succeeded)
                            {
                                ((List<string>)summary.errors).Add($"Bill {bill.BillNumber} post failed: {post.Error}");
                                summary = summary with { bills_with_errors = summary.bills_with_errors + 1 };
                                continue;
                            }

                            bill.JournalEntryId = draft.Value!.Id;
                            bill.UpdatedAt = DateTime.UtcNow;
                            bill.UpdatedBy = userId;
                            await billRepo.UpdateAsync(bill, ct);

                            summary = summary with
                            {
                                bills_processed = summary.bills_processed + 1,
                                total_debits_posted = summary.total_debits_posted + bill.TotalAmount,
                                total_credits_posted = summary.total_credits_posted + bill.TotalAmount
                            };
                            _logger.LogInformation("DEC-076: Bill {N} backfilled — JE={JE}", bill.BillNumber, draft.Value.Id);
                        }
                        catch (Exception ex)
                        {
                            ((List<string>)summary.errors).Add($"Bill {bill.BillNumber}: {ex.Message}");
                            summary = summary with { bills_with_errors = summary.bills_with_errors + 1 };
                        }
                    }
                    summary = summary with { bills_skipped = bills.Count - billsToProcess.Count };
                }

                _logger.LogInformation("DEC-076: Backfill complete — {OB}, bills={P}/{S}, errors={E}",
                    summary.opening_balance_created ? "created" : "skipped",
                    summary.bills_processed, summary.bills_skipped, summary.bills_with_errors);
            });
        });

        return Accepted(new
        {
            jobId,
            scenario = "finance-backfill",
            status = "started",
            statusEndpoint = $"/api/admin/seed/status/{jobId}",
            note = "Background execution. Poll status endpoint for progress."
        });
    }
}