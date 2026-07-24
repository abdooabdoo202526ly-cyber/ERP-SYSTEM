# =============================================================================
# ERP-SYSTEM - Quick Start (DEFAULT — Uses Native Mode)
# =============================================================================
# هذا السكريبت الافتراضي. يكشف تلقائياً:
# - Docker شغّال → يستخدم Docker
# - Docker مكسور → يستخدم Native (بدون Docker)
# =============================================================================

[CmdletBinding()]
param(
    [switch]$Down,
    [switch]$Status,
    [switch]$Setup,
    [switch]$ForceNative,
    [switch]$Help
)

if ($Help) {
    Write-Host ""
    Write-Host "Usage:" -ForegroundColor Cyan
    Write-Host "  .\scripts\quickstart.ps1            # start (auto-detect)"
    Write-Host "  .\scripts\quickstart.ps1 -Setup     # create databases"
    Write-Host "  .\scripts\quickstart.ps1 -Status   # health check"
    Write-Host "  .\scripts\quickstart.ps1 -Down     # stop everything"
    Write-Host "  .\scripts\quickstart.ps1 -ForceNative  # skip Docker"
    Write-Host ""
    exit 0
}

# Always delegate to start.ps1 (smart wrapper)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
& "$ScriptDir\start.ps1" @PSBoundParameters
exit $LASTEXITCODE
