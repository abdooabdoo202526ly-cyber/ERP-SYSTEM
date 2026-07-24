# =============================================================================
# ERP-SYSTEM - Start (NATIVE ONLY)
# =============================================================================
# هذا السكريبت يستخدم Native mode فقط.
# لو تريد Docker، استخدم: .\scripts\quickstart.ps1 (يعمل فقط لو Docker سليم)
# =============================================================================

[CmdletBinding()]
param(
    [switch]$Down,
    [switch]$Status,
    [switch]$Setup,
    [switch]$Help
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$NativeScript = Join-Path $ScriptDir "start-native.ps1"

function Write-Step($msg)  { Write-Host "[*] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "[+] $msg" -ForegroundColor Green }
function Write-Warn($msg)  { Write-Host "[!] $msg" -ForegroundColor Yellow }

if ($Help) {
    Write-Host ""
    Write-Host "Usage:" -ForegroundColor Cyan
    Write-Host "  .\scripts\start.ps1            # start in native mode (no Docker)"
    Write-Host "  .\scripts\start.ps1 -Setup     # create databases"
    Write-Host "  .\scripts\start.ps1 -Status   # health check"
    Write-Host "  .\scripts\start.ps1 -Down     # stop"
    Write-Host ""
    Write-Host "Note: Docker mode is disabled. If you have working Docker, use:"
    Write-Host "  .\scripts\quickstart.ps1"
    Write-Host ""
    exit 0
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  ERP-SYSTEM (Native Mode - NO Docker)" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# Direct call to native (no Docker check, no recursion)
& $NativeScript @PSBoundParameters
exit $LASTEXITCODE
