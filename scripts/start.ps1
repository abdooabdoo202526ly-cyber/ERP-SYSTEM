# =============================================================================
# ERP-SYSTEM - Universal Start (NO RECURSION)
# =============================================================================
# 1. يفحص Docker — لو شغّال → docker compose مباشرة
# 2. لو Docker مكسور → Native mode
# 3. افتراضياً: Native mode (أكثر أماناً)
# =============================================================================

[CmdletBinding()]
param(
    [switch]$Down,
    [switch]$Status,
    [switch]$Setup,
    [switch]$ForceNative,
    [switch]$Help
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir   = Resolve-Path "$ScriptDir\.."
$NativeScript = Join-Path $ScriptDir "start-native.ps1"
$DockerDir    = Join-Path $RootDir "infra\docker"
$ComposeFile  = Join-Path $DockerDir "docker-compose.dev.yml"
$ProjectName  = "erp-system"

function Write-Step($msg)  { Write-Host "[*] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "[+] $msg" -ForegroundColor Green }
function Write-Warn($msg)  { Write-Host "[!] $msg" -ForegroundColor Yellow }
function Write-Fail($msg)  { Write-Host "[X] $msg" -ForegroundColor Red; exit 1 }

if ($Help) {
    Write-Host ""
    Write-Host "Usage:" -ForegroundColor Cyan
    Write-Host "  .\scripts\start.ps1            # Native (default, safe)"
    Write-Host "  .\scripts\start.ps1 -ForceNative  # Native (explicit)"
    Write-Host "  .\scripts\start.ps1 -Setup     # create databases"
    Write-Host "  .\scripts\start.ps1 -Status   # health check"
    Write-Host "  .\scripts\start.ps1 -Down     # stop everything"
    Write-Host ""
    exit 0
}

# 1) Check if Docker is REALLY healthy
function Test-DockerHealthy {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        return @{ Healthy = $false; Reason = "Docker not installed" }
    }
    try {
        $info = docker info 2>&1
        if ($LASTEXITCODE -ne 0) {
            return @{ Healthy = $false; Reason = "docker info failed" }
        }
        # Try a real operation
        $ps = docker ps 2>&1
        if ($LASTEXITCODE -ne 0) {
            return @{ Healthy = $false; Reason = "docker ps failed" }
        }
        # Try compose
        $compose = docker compose version 2>&1
        if ($LASTEXITCODE -ne 0) {
            return @{ Healthy = $false; Reason = "docker compose not available" }
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

# Default to Native (safer)
$useNative = $true
if (-not $ForceNative) {
    $dockerStatus = Test-DockerHealthy
    if ($dockerStatus.Healthy) {
        Write-Ok "Docker is healthy"
        $useNative = $false
    } else {
        Write-Warn "Docker is NOT healthy: $($dockerStatus.Reason)"
    }
} else {
    Write-Step "ForceNative mode requested"
}

if ($useNative) {
    Write-Step "Using NATIVE mode (no Docker)..."
    & $NativeScript @PSBoundParameters
    exit $LASTEXITCODE
}

# Docker mode (direct, no recursion)
Write-Step "Using DOCKER mode..."
Write-Host ""

function Wait-Http($url, $label, $max = 90) {
    Write-Step "Waiting for $label ($url)..."
    for ($i = 1; $i -le $max; $i++) {
        try {
            $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
            if ($r.StatusCode -lt 400) {
                Write-Ok "$label is up (after ${i}s)"
                return
            }
        } catch {}
        Start-Sleep 1
    }
    Write-Fail "$label did not respond"
}

if ($Status) {
    foreach ($svc in @(
        @{ Name="Backend";  Url="http://localhost:5000/health" },
        @{ Name="Frontend"; Url="http://localhost:3000" }
    )) {
        try {
            $r = Invoke-WebRequest -Uri $svc.Url -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
            Write-Host "  [OK] $($svc.Name)" -ForegroundColor Green
        } catch {
            Write-Host "  [DOWN] $($svc.Name)" -ForegroundColor Red
        }
    }
    exit 0
}

if ($Down) {
    Push-Location $DockerDir
    docker compose -p $ProjectName -f $ComposeFile down
    Pop-Location
    Write-Ok "Stopped"
    exit 0
}

# Start
Push-Location $DockerDir
docker compose -p $ProjectName -f $ComposeFile up -d --build
Pop-Location
Write-Ok "Containers started"

# Wait for services
for ($i = 1; $i -le 60; $i++) {
    $ready = docker compose -p $ProjectName -f $ComposeFile exec -T postgres pg_isready -U erp_user -d erp_system 2>$null
    if ($LASTEXITCODE -eq 0) { Write-Ok "PostgreSQL is ready"; break }
    Start-Sleep 1
}

Wait-Http "http://localhost:5000/health" "Backend API" 120
Wait-Http "http://localhost:3000"        "Frontend"     120

Write-Host ""
Write-Ok "All services started!"
Write-Host ""
Write-Host "  Frontend:  http://localhost:3000"  -ForegroundColor Cyan
Write-Host "  Backend:   http://localhost:5000"  -ForegroundColor Cyan
Write-Host "  Login:     admin@alfajr.local / Demo1234" -ForegroundColor Cyan
