# =============================================================================
# ERP-SYSTEM - Quick Start (ONE COMMAND DOES EVERYTHING)
# =============================================================================
# هذا السكريبت يعمل كل شيء تلقائياً:
#   1. يفحص PostgreSQL
#   2. ينشئ databases إذا ما موجودة
#   3. يثبّت npm dependencies إذا ما مثبتة
#   4. يشغّل Backend
#   5. يشغّل Frontend
#   6. يعرض URLs
#
# فقط شغّل:  .\scripts\quickstart.ps1
# =============================================================================

[CmdletBinding()]
param(
    [switch]$Down,
    [switch]$Status
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir   = Resolve-Path "$ScriptDir\.."
$BackendLog = Join-Path $RootDir ".backend.log"
$BackendErr = Join-Path $RootDir ".backend.err.log"
$FrontendLog = Join-Path $RootDir ".frontend.log"
$FrontendErr = Join-Path $RootDir ".frontend.err.log"
$FrontendDir = Join-Path $RootDir "src\frontend"

# PostgreSQL paths to check
$PsqlPaths = @(
    "C:\Program Files\PostgreSQL\15\bin\psql.exe",
    "C:\Program Files\PostgreSQL\16\bin\psql.exe",
    "C:\Program Files\PostgreSQL\17\bin\psql.exe",
    "C:\Program Files (x86)\PostgreSQL\15\bin\psql.exe"
)

function Write-Step($msg)  { Write-Host "[*] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "[+] $msg" -ForegroundColor Green }
function Write-Warn($msg)  { Write-Host "[!] $msg" -ForegroundColor Yellow }
function Write-Fail($msg)  { Write-Host "[X] $msg" -ForegroundColor Red; exit 1 }

# --- Stop -------------------------------------------------------------------
if ($Down) {
    Write-Step "Stopping all services..."
    Get-Process -Name "dotnet","node" -ErrorAction SilentlyContinue | Where-Object {
        $_.CommandLine -match "Host|next" -or $_.Path -match "next-server"
    } | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Ok "Stopped"
    exit 0
}

# --- Status -----------------------------------------------------------------
if ($Status) {
    foreach ($svc in @(
        @{ Name="Backend";  Url="http://localhost:5000/health" },
        @{ Name="Frontend"; Url="http://localhost:3000" }
    )) {
        try {
            $r = Invoke-WebRequest -Uri $svc.Url -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
            Write-Host "  [OK] $($svc.Name) ($($svc.Url))" -ForegroundColor Green
        } catch {
            Write-Host "  [DOWN] $($svc.Name) ($($svc.Url))" -ForegroundColor Red
        }
    }
    exit 0
}

# --- Main flow --------------------------------------------------------------
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "     ERP-SYSTEM (All-in-One, No Docker)" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# 1) Find psql
$psql = Get-Command psql -ErrorAction SilentlyContinue
if (-not $psql) {
    foreach ($p in $PsqlPaths) {
        if (Test-Path $p) {
            $psqlDir = Split-Path $p
            $env:Path += ";$psqlDir"
            Write-Step "Found psql at: $p (added to PATH)"
            $psql = Get-Command psql
            break
        }
    }
}

if (-not $psql) {
    Write-Host ""
    Write-Warn "PostgreSQL psql NOT FOUND in PATH"
    Write-Host ""
    Write-Host "  Step 1: Install PostgreSQL 15+ from:" -ForegroundColor Yellow
    Write-Host "    https://www.postgresql.org/download/windows/" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Step 2: Use password: postgres" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Step 3: After install, re-run this script." -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

Write-Ok "psql found: $($psql.Source)"

# 2) Check PostgreSQL is up
$pgPort = Test-NetConnection -ComputerName localhost -Port 5432 -WarningAction SilentlyContinue
if (-not $pgPort.TcpTestSucceeded) {
    Write-Warn "PostgreSQL not running on :5432"
    $svc = Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($svc) {
        Write-Step "Starting service: $($svc.Name)..."
        Start-Service $svc.Name -ErrorAction Stop
        Start-Sleep 2
    } else {
        Write-Fail "PostgreSQL service not found. Install it first."
    }
}
Write-Ok "PostgreSQL is up on :5432"

# 3) Test connection + create databases
$env:PGPASSWORD = "postgres"
$test = & psql -U postgres -h localhost -p 5432 -c "SELECT 1;" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Warn "Cannot connect to PostgreSQL with user 'postgres' and password 'postgres'"
    Write-Host "    Set your password: `$env:PGPASSWORD = 'YOUR_PASSWORD'"
    Write-Fail "Connection failed"
}

# 4) Check if databases exist
$dbs = & psql -U postgres -h localhost -p 5432 -tAc "SELECT datname FROM pg_database WHERE datname IN ('erp_system','erp_events')" 2>&1
$hasErpSystem = $dbs -match "erp_system"
$hasErpEvents = $dbs -match "erp_events"

if (-not $hasErpSystem -or -not $hasErpEvents) {
    Write-Step "Creating databases..."
    & psql -U postgres -h localhost -p 5432 -c "CREATE USER erp_user WITH PASSWORD 'erp_password' SUPERUSER;" 2>$null
    & psql -U postgres -h localhost -p 5432 -c "CREATE DATABASE erp_system OWNER erp_user;" 2>$null
    & psql -U postgres -h localhost -p 5432 -c "CREATE DATABASE erp_events OWNER erp_user;" 2>$null
    Write-Ok "Databases created: erp_system, erp_events"
} else {
    Write-Ok "Databases exist: erp_system, erp_events"
}

# 5) Self-heal appsettings.json
$SettingsFile = Join-Path $RootDir "src\backend\Host\appsettings.json"
if (Test-Path $SettingsFile) {
    $content = Get-Content $SettingsFile -Raw
    if ($content -match "Database=neondb") {
        Write-Step "Fixing appsettings.json (neondb → erp_system)..."
        $newContent = $content -replace "Database=neondb", "Database=erp_system"
        Set-Content -Path $SettingsFile -Value $newContent -NoNewline
        Write-Ok "Fixed"
    }
    $binFile = Join-Path $RootDir "src\backend\Host\bin\Debug\net9.0\appsettings.json"
    if (Test-Path $binFile) {
        $binContent = Get-Content $binFile -Raw
        if ($binContent -match "Database=neondb") {
            $newBin = $binContent -replace "Database=neondb", "Database=erp_system"
            Set-Content -Path $binFile -Value $newBin -NoNewline
            Write-Ok "Fixed bin/Debug copy"
        }
    }
}

# 6) Install npm dependencies if needed
if (-not (Test-Path (Join-Path $FrontendDir "node_modules"))) {
    Write-Step "Installing npm dependencies (this takes 3-5 minutes)..."
    Push-Location $FrontendDir
    npm install 2>&1 | Out-Null
    Pop-Location
    Write-Ok "npm dependencies installed"
} else {
    Write-Ok "npm dependencies already installed"
}

# 7) Stop any existing services
Write-Step "Stopping any existing services..."
Get-Process -Name "dotnet","node" -ErrorAction SilentlyContinue | Where-Object {
    $_.CommandLine -match "Host|next" -or $_.Path -match "next-server"
} | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep 1

# 8) Start Backend
Write-Step "Starting Backend (.NET 9) on http://localhost:5000 ..."
$backend = Start-Process -FilePath "dotnet" `
    -ArgumentList "run","--project","Host","--urls","http://localhost:5000" `
    -WorkingDirectory (Join-Path $RootDir "src\backend") `
    -RedirectStandardOutput $BackendLog `
    -RedirectStandardError  $BackendErr `
    -WindowStyle Hidden `
    -PassThru

Write-Step "Waiting for Backend..."
$backendUp = $false
for ($i = 1; $i -le 90; $i++) {
    try {
        $r = Invoke-WebRequest -Uri "http://localhost:5000/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
        if ($r.StatusCode -lt 400) { $backendUp = $true; break }
    } catch {}
    Start-Sleep 1
}
if (-not $backendUp) {
    Write-Warn "Backend did not respond within 90s. Check $BackendErr"
    Write-Host "    Last lines:"
    if (Test-Path $BackendErr) {
        Get-Content $BackendErr -Tail 10 | ForEach-Object { Write-Host "      $_" }
    }
} else {
    Write-Ok "Backend is up (after ${i}s)"
}

# 9) Start Frontend
Write-Step "Starting Frontend (Next.js 14) on http://localhost:3000 ..."
$frontend = Start-Process -FilePath "cmd.exe" `
    -ArgumentList "/c","set NEXT_PUBLIC_API_URL=http://localhost:5000 && npm run dev" `
    -WorkingDirectory $FrontendDir `
    -RedirectStandardOutput $FrontendLog `
    -RedirectStandardError  $FrontendErr `
    -WindowStyle Hidden `
    -PassThru

Write-Step "Waiting for Frontend..."
$frontendUp = $false
for ($i = 1; $i -le 90; $i++) {
    try {
        $r = Invoke-WebRequest -Uri "http://localhost:3000" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
        if ($r.StatusCode -lt 400) { $frontendUp = $true; break }
    } catch {}
    Start-Sleep 1
}
if (-not $frontendUp) {
    Write-Warn "Frontend did not respond within 90s. Check $FrontendErr"
} else {
    Write-Ok "Frontend is up (after ${i}s)"
}

Write-Host ""
if ($backendUp -and $frontendUp) {
    Write-Ok "All services started!"
} else {
    Write-Warn "Some services may not be ready. Check logs."
}
Write-Host ""
Write-Host "  Frontend:  http://localhost:3000"  -ForegroundColor Cyan
Write-Host "  Backend:   http://localhost:5000"  -ForegroundColor Cyan
Write-Host "  Swagger:   http://localhost:5000/swagger" -ForegroundColor Cyan
Write-Host "  Login:     admin@alfajr.local / Demo1234" -ForegroundColor Cyan
Write-Host ""
Write-Host "Commands:" -ForegroundColor Yellow
Write-Host "  .\scripts\quickstart.ps1 -Status   # health check" -ForegroundColor Gray
Write-Host "  .\scripts\quickstart.ps1 -Down     # stop everything" -ForegroundColor Gray
Write-Host ""
