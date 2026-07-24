# =============================================================================
# ERP-SYSTEM - Universal Start (Auto-detect Docker vs Native)
# =============================================================================
# هذا السكريبت الذكي:
# 1. يفحص Docker — لو شغّال → يستخدمه
# 2. لو Docker مكسور/غير مثبت → يستخدم Native mode تلقائياً
# 3. لو PostgreSQL غير جاهز → يطلب المساعدة
# =============================================================================

[CmdletBinding()]
param(
    [switch]$Down,
    [switch]$Status,
    [switch]$Setup,
    [switch]$ForceNative
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir   = Resolve-Path "$ScriptDir\.."
$NativeScript = Join-Path $ScriptDir "start-native.ps1"
$QuickScript  = Join-Path $ScriptDir "quickstart.ps1"

function Write-Step($msg)  { Write-Host "[*] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "[+] $msg" -ForegroundColor Green }
function Write-Warn($msg)  { Write-Host "[!] $msg" -ForegroundColor Yellow }
function Write-Fail($msg)  { Write-Host "[X] $msg" -ForegroundColor Red }

# 1) Check if Docker is healthy
function Test-DockerHealthy {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        return @{ Healthy = $false; Reason = "Docker not installed" }
    }
    try {
        $info = docker info 2>&1
        if ($LASTEXITCODE -ne 0) {
            return @{ Healthy = $false; Reason = "Docker daemon not responding" }
        }
        # Try a real operation
        docker ps 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            return @{ Healthy = $false; Reason = "Docker commands fail" }
        }
        return @{ Healthy = $true; Reason = "OK" }
    } catch {
        return @{ Healthy = $false; Reason = "Docker error: $_" }
    }
}

# 2) Detect mode
Write-Host ""
Write-Host "=== ERP-SYSTEM Universal Start ===" -ForegroundColor Cyan
Write-Host ""

$dockerStatus = Test-DockerHealthy
$useNative = $ForceNative -or -not $dockerStatus.Healthy

if (-not $useNative) {
    Write-Ok "Docker is healthy"
    Write-Step "Using Docker mode (quickstart.ps1)..."
    & $QuickScript @args
    exit $LASTEXITCODE
} else {
    Write-Warn "Docker is broken or not available: $($dockerStatus.Reason)"
    Write-Step "Auto-switching to Native mode (no Docker required)..."
    Write-Host ""
    & $NativeScript @args
    exit $LASTEXITCODE
}
