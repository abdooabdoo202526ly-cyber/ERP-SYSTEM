# =============================================================================
# ERP-SYSTEM - Quick Start (Windows PowerShell)
# =============================================================================
# Usage:
#   .\scripts\quickstart.ps1           # start everything
#   .\scripts\quickstart.ps1 -Down     # stop everything
#   .\scripts\quickstart.ps1 -Status   # show status
#   .\scripts\quickstart.ps1 -Logs     # tail logs
#   .\scripts\quickstart.ps1 -Reset    # stop + delete all data (DESTRUCTIVE)
# =============================================================================

[CmdletBinding()]
param(
    [switch]$Down,
    [switch]$Status,
    [switch]$Logs,
    [switch]$Reset,
    [switch]$Help
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir   = Resolve-Path "$ScriptDir\.."
$DockerDir = Join-Path $RootDir "infra\docker"
$ComposeFile = Join-Path $DockerDir "docker-compose.dev.yml"
$ProjectName = "erp-system"

# --- helpers ----------------------------------------------------------------
function Write-Step($msg)  { Write-Host "[*] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "[+] $msg" -ForegroundColor Green }
function Write-Warn($msg)  { Write-Host "[!] $msg" -ForegroundColor Yellow }
function Write-Fail($msg)  { Write-Host "[X] $msg" -ForegroundColor Red; exit 1 }

function Test-Docker {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Write-Fail "Docker not installed. Install Docker Desktop: https://www.docker.com/products/docker-desktop"
    }
    try { docker info | Out-Null } catch {
        Write-Fail "Docker daemon not running. Start Docker Desktop and retry."
    }
}

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
    Write-Fail "$label did not respond within ${max}s at $url"
}

function Start-Docker {
    Test-Docker
    Write-Step "Starting full stack via Docker Compose..."
    Push-Location $DockerDir
    docker compose -p $ProjectName -f $ComposeFile up -d --build
    Pop-Location
    Write-Ok "Containers started"

    Write-Step "Waiting for PostgreSQL..."
    for ($i = 1; $i -le 60; $i++) {
        $ready = docker compose -p $ProjectName -f $ComposeFile exec -T postgres pg_isready -U erp_user -d erp_system 2>$null
        if ($LASTEXITCODE -eq 0) { Write-Ok "PostgreSQL is ready (after ${i}s)"; break }
        Start-Sleep 1
    }

    Wait-Http "http://localhost:5000/health" "Backend API" 120
    Wait-Http "http://localhost:3000"        "Frontend"     120
}

function Start-Native {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Fail ".NET 9 SDK not installed: https://dot.net"
    }
    if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
        Write-Fail "Node.js 20+ not installed: https://nodejs.org"
    }

    Write-Step "Checking PostgreSQL on localhost:5432..."
    $pg = Test-NetConnection -ComputerName localhost -Port 5432 -WarningAction SilentlyContinue
    if (-not $pg.TcpTestSucceeded) {
        Write-Warn "PostgreSQL not responding on localhost:5432"
        Write-Host ""
        Write-Host "  Start it manually, then create databases:"
        Write-Host "    psql -U postgres -c `"CREATE USER erp_user WITH PASSWORD 'erp_password';`""
        Write-Host "    psql -U postgres -c `"CREATE DATABASE erp_system OWNER erp_user;`""
        Write-Host "    psql -U postgres -c `"CREATE DATABASE erp_events OWNER erp_user;`""
        Write-Fail "PostgreSQL not running"
    }

    Write-Step "Starting Backend..."
    Start-Process -FilePath "dotnet" `
        -ArgumentList "run","--project","Host","--urls","http://localhost:5000" `
        -WorkingDirectory (Join-Path $RootDir "src\backend") `
        -RedirectStandardOutput (Join-Path $RootDir ".backend.log") `
        -RedirectStandardError  (Join-Path $RootDir ".backend.err.log") `
        -WindowStyle Hidden
    Wait-Http "http://localhost:5000/health" "Backend API" 120

    Write-Step "Starting Frontend..."
    $frontendDir = Join-Path $RootDir "src\frontend"
    if (-not (Test-Path (Join-Path $frontendDir "node_modules"))) {
        Push-Location $frontendDir
        npm install
        Pop-Location
    }
    Start-Process -FilePath "npm" `
        -ArgumentList "run","dev" `
        -WorkingDirectory $frontendDir `
        -RedirectStandardOutput (Join-Path $RootDir ".frontend.log") `
        -RedirectStandardError  (Join-Path $RootDir ".frontend.err.log") `
        -WindowStyle Hidden `
        -Environment @{ NEXT_PUBLIC_API_URL = "http://localhost:5000" }
    Wait-Http "http://localhost:3000" "Frontend" 90
}

function Stop-Docker {
    Test-Docker
    Write-Step "Stopping containers..."
    Push-Location $DockerDir
    docker compose -p $ProjectName -f $ComposeFile down
    Pop-Location
    Write-Ok "Stopped"
}

function Stop-Native {
    Write-Step "Stopping local processes..."
    Get-Process -Name "dotnet","node" -ErrorAction SilentlyContinue | Where-Object {
        $_.CommandLine -match "Host|next" -or $_.Path -match "next-server"
    } | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Ok "Stopped"
}

function Print-Status {
    Write-Host ""
    Write-Host "[i] Container status:"
    if (Get-Command docker -ErrorAction SilentlyContinue) {
        try {
            Push-Location $DockerDir
            docker compose -p $ProjectName -f $ComposeFile ps
            Pop-Location
        } catch { Write-Host "  (no containers running)" }
    } else {
        Write-Host "  (Docker not available)"
    }
    Write-Host ""
    Write-Host "[i] Health checks:"
    foreach ($svc in @(
        @{ Name="Backend";  Url="http://localhost:5000/health" },
        @{ Name="Swagger";  Url="http://localhost:5000/swagger" },
        @{ Name="Frontend"; Url="http://localhost:3000" }
    )) {
        try {
            $r = Invoke-WebRequest -Uri $svc.Url -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
            Write-Host "  [+] $($svc.Name)  $($svc.Url)"
        } catch {
            Write-Host "  [X] $($svc.Name)  $($svc.Url)  (not responding)"
        }
    }
}

function Reset-Data {
    Write-Warn "This will DELETE all data. Continue? [y/N]"
    $ans = Read-Host
    if ($ans -notin @("y","Y")) { Write-Host "Aborted."; exit 0 }
    Test-Docker
    Push-Location $DockerDir
    docker compose -p $ProjectName -f $ComposeFile down -v
    Pop-Location
    Remove-Item "$RootDir\.backend.log","$RootDir\.backend.err.log","$RootDir\.frontend.log","$RootDir\.frontend.err.log" -ErrorAction SilentlyContinue
    Write-Ok "Reset complete"
}

# --- main -------------------------------------------------------------------
$mode = "docker"
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { $mode = "native" }
else { try { docker info | Out-Null } catch { $mode = "native" } }

Write-Host ""
Write-Host "ERP-SYSTEM Quick Start"
Write-Host "=========================="
Write-Host "  Mode: $mode"
Write-Host ""

if ($Help) {
    Get-Help $MyInvocation.MyCommand.Path -Full | Out-String | Write-Host
    exit 0
}

if ($Down) {
    if ($mode -eq "docker") { Stop-Docker } else { Stop-Native }
    exit 0
}
if ($Status) { Print-Status; exit 0 }
if ($Logs) {
    if ($mode -eq "docker") {
        Push-Location $DockerDir; docker compose -p $ProjectName -f $ComposeFile logs -f --tail=50; Pop-Location
    } else {
        Get-Content "$RootDir\.backend.log","$RootDir\.frontend.log" -Wait -Tail 50
    }
    exit 0
}
if ($Reset) { Reset-Data; exit 0 }

# default = start
if ($mode -eq "docker") { Start-Docker } else { Start-Native }

Write-Host ""
Write-Host "[+] ERP-SYSTEM is up!"
Write-Host "============================================="
Write-Host "  [Web]   Frontend:    http://localhost:3000"
Write-Host "  [API]   Backend:     http://localhost:5000"
Write-Host "  [Docs]  Swagger:     http://localhost:5000/swagger"
Write-Host "  [Ping]  Health:     http://localhost:5000/health"
Write-Host "============================================="
Write-Host "  [User]  Login:       admin@alfajr.local / Demo1234"
Write-Host "                      (created automatically on first start by the seed)"
Write-Host ""
Write-Host "  [Help]  Full docs:   README-DEV.md"
Write-Host "  [Logs]  Tail logs:   .\scripts\quickstart.ps1 -Logs"
Write-Host "  [Stop]  Stop:        .\scripts\quickstart.ps1 -Down"
Write-Host ""
