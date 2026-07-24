# =============================================================================
# ERP-SYSTEM - Native Start (No Docker Required)
# =============================================================================
# Use this when Docker is broken or disk is full.
# Requires: .NET 9 SDK, Node.js 20+, PostgreSQL 15+
# =============================================================================

[CmdletBinding()]
param(
    [switch]$Down,
    [switch]$Status,
    [switch]$Setup
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir   = Resolve-Path "$ScriptDir\.."

function Write-Step($msg)  { Write-Host "[*] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "[+] $msg" -ForegroundColor Green }
function Write-Warn($msg)  { Write-Host "[!] $msg" -ForegroundColor Yellow }
function Write-Fail($msg)  { Write-Host "[X] $msg" -ForegroundColor Red; exit 1 }

# --- Setup DB ---------------------------------------------------------------
function Setup-Database {
    Write-Step "Creating PostgreSQL databases..."

    $env:PGPASSWORD = "postgres"
    $psql = "psql"

    if (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
        Write-Fail "psql not found. Install PostgreSQL 15+: https://www.postgresql.org/download/windows/"
    }

    & psql -U postgres -c "CREATE USER erp_user WITH PASSWORD 'erp_password';" 2>$null
    & psql -U postgres -c "CREATE DATABASE erp_system OWNER erp_user;" 2>$null
    & psql -U postgres -c "CREATE DATABASE erp_events OWNER erp_user;" 2>$null
    Write-Ok "Databases created (or already exist)"
}

# --- Test PostgreSQL --------------------------------------------------------
function Test-Postgres {
    $pg = Test-NetConnection -ComputerName localhost -Port 5432 -WarningAction SilentlyContinue
    if (-not $pg.TcpTestSucceeded) {
        Write-Fail "PostgreSQL not responding on localhost:5432. Start PostgreSQL service first."
    }
    Write-Ok "PostgreSQL is up on :5432"
}

# --- Start Backend ----------------------------------------------------------
function Start-Backend {
    Write-Step "Starting Backend (.NET 9) on http://localhost:5000 ..."
    $backendLog = Join-Path $RootDir ".backend.log"
    $backendErr = Join-Path $RootDir ".backend.err.log"

    Start-Process -FilePath "dotnet" `
        -ArgumentList "run","--project","Host","--urls","http://localhost:5000" `
        -WorkingDirectory (Join-Path $RootDir "src\backend") `
        -RedirectStandardOutput $backendLog `
        -RedirectStandardError  $backendErr `
        -WindowStyle Hidden

    Write-Step "Waiting for Backend..."
    for ($i = 1; $i -le 90; $i++) {
        try {
            $r = Invoke-WebRequest -Uri "http://localhost:5000/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
            if ($r.StatusCode -lt 400) { Write-Ok "Backend is up (after ${i}s)"; return }
        } catch {}
        Start-Sleep 1
    }
    Write-Fail "Backend did not respond. Check $backendErr"
}

# --- Start Frontend ---------------------------------------------------------
function Start-Frontend {
    Write-Step "Starting Frontend (Next.js 14) on http://localhost:3000 ..."
    $frontendDir = Join-Path $RootDir "src\frontend"

    if (-not (Test-Path (Join-Path $frontendDir "node_modules"))) {
        Write-Step "Installing npm dependencies..."
        Push-Location $frontendDir
        npm install
        Pop-Location
    }

    $frontendLog = Join-Path $RootDir ".frontend.log"
    $frontendErr = Join-Path $RootDir ".frontend.err.log"

    Start-Process -FilePath "npm" `
        -ArgumentList "run","dev" `
        -WorkingDirectory $frontendDir `
        -RedirectStandardOutput $frontendLog `
        -RedirectStandardError  $frontendErr `
        -WindowStyle Hidden `
        -Environment @{ NEXT_PUBLIC_API_URL = "http://localhost:5000" }

    Write-Step "Waiting for Frontend..."
    for ($i = 1; $i -le 90; $i++) {
        try {
            $r = Invoke-WebRequest -Uri "http://localhost:3000" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
            if ($r.StatusCode -lt 400) { Write-Ok "Frontend is up (after ${i}s)"; return }
        } catch {}
        Start-Sleep 1
    }
    Write-Fail "Frontend did not respond. Check $frontendErr"
}

# --- Stop -------------------------------------------------------------------
function Stop-All {
    Write-Step "Stopping Backend + Frontend..."
    Get-Process -Name "dotnet","node" -ErrorAction SilentlyContinue | Where-Object {
        $_.CommandLine -match "Host|next" -or $_.Path -match "next-server"
    } | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Ok "Stopped"
}

# --- Status -----------------------------------------------------------------
function Print-Status {
    Write-Host ""
    Write-Host "[i] Health checks:" -ForegroundColor Cyan
    foreach ($svc in @(
        @{ Name="Backend";  Url="http://localhost:5000/health" },
        @{ Name="Swagger";  Url="http://localhost:5000/swagger" },
        @{ Name="Frontend"; Url="http://localhost:3000" }
    )) {
        try {
            $r = Invoke-WebRequest -Uri $svc.Url -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
            Write-Host "  [OK] $($svc.Name) ($($svc.Url))" -ForegroundColor Green
        } catch {
            Write-Host "  [DOWN] $($svc.Name) ($($svc.Url))" -ForegroundColor Red
        }
    }
}

# --- Main -------------------------------------------------------------------
if ($Setup) {
    Setup-Database
    exit 0
}

if ($Down) {
    Stop-All
    exit 0
}

if ($Status) {
    Print-Status
    exit 0
}

Write-Host ""
Write-Host "=== ERP-SYSTEM (Native, No Docker) ===" -ForegroundColor Cyan
Write-Host ""

Test-Postgres
Start-Backend
Start-Frontend

Write-Host ""
Write-Ok "All services started!"
Write-Host ""
Write-Host "  Frontend:  http://localhost:3000"  -ForegroundColor Cyan
Write-Host "  Backend:   http://localhost:5000"  -ForegroundColor Cyan
Write-Host "  Swagger:   http://localhost:5000/swagger" -ForegroundColor Cyan
Write-Host "  Login:     admin@alfajr.local / Demo1234" -ForegroundColor Cyan
Write-Host ""
Write-Host "Run '.\scripts\start-native.ps1 -Status' to check health"
Write-Host "Run '.\scripts\start-native.ps1 -Down' to stop"
Write-Host ""
