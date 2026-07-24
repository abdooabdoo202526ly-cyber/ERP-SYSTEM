# =============================================================================
# ERP-SYSTEM - Quick Start (One Command Does Everything)
# =============================================================================
# تشغيل كامل للنظام بدون Docker. يفعل:
#   1. البحث عن psql
#   2. فحص PostgreSQL
#   3. الاتصال بكلمة سر (افتراضية أو يطلبها)
#   4. إنشاء databases (erp_system, erp_events)
#   5. إصلاح appsettings.json
#   6. تثبيت npm dependencies
#   7. تشغيل Backend + Frontend
# =============================================================================

[CmdletBinding()]
param(
    [switch]$Down,
    [switch]$Status
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir   = Resolve-Path "$ScriptDir\.."
$BackendLog   = Join-Path $RootDir ".backend.log"
$BackendErr   = Join-Path $RootDir ".backend.err.log"
$FrontendLog  = Join-Path $RootDir ".frontend.log"
$FrontendErr  = Join-Path $RootDir ".frontend.err.log"
$FrontendDir  = Join-Path $RootDir "src\frontend"
$SettingsFile = Join-Path $RootDir "src\backend\Host\appsettings.json"

# PostgreSQL paths to check
$PsqlPaths = @(
    "C:\Program Files\PostgreSQL\15\bin\psql.exe",
    "C:\Program Files\PostgreSQL\16\bin\psql.exe",
    "C:\Program Files\PostgreSQL\17\bin\psql.exe",
    "C:\Program Files (x86)\PostgreSQL\15\bin\psql.exe",
    "C:\Program Files\PostgreSQL\14\bin\psql.exe"
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

# 1) Find psql in PATH or common locations
$psql = Get-Command psql -ErrorAction SilentlyContinue
if (-not $psql) {
    foreach ($p in $PsqlPaths) {
        if (Test-Path $p) {
            $psqlDir = Split-Path $p
            $env:Path = "$env:Path;$psqlDir"
            $psql = Get-Command psql
            Write-Step "Found psql at: $p (added to PATH)"
            break
        }
    }
}

if (-not $psql) {
    Write-Host ""
    Write-Warn "PostgreSQL psql NOT FOUND"
    Write-Host ""
    Write-Host "  Install PostgreSQL 15+ from:" -ForegroundColor Yellow
    Write-Host "    https://www.postgresql.org/download/windows/" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Use password: postgres" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

Write-Ok "psql: $($psql.Source)"

# 2) Check PostgreSQL port
$pgPort = Test-NetConnection -ComputerName localhost -Port 5432 -WarningAction SilentlyContinue
if (-not $pgPort.TcpTestSucceeded) {
    Write-Warn "PostgreSQL not running on :5432"
    $svc = Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($svc) {
        Write-Step "Starting service: $($svc.Name)..."
        try {
            Start-Service $svc.Name -ErrorAction Stop
            Start-Sleep 2
        } catch {
            Write-Fail "Cannot start PostgreSQL service. Run as Administrator."
        }
    } else {
        Write-Fail "PostgreSQL service not found. Install it first."
    }
}
Write-Ok "PostgreSQL is up on :5432"

# 3) Test connection with common passwords, or ask user
# v1.0.34-Hotfix7: On Windows, .pgpass must be at %APPDATA%\postgresql\pgpass.conf
# NOT at ~/.pgpass (that's Unix path)
$pgPassDir = Join-Path $env:APPDATA "postgresql"
if (-not (Test-Path $pgPassDir)) {
    New-Item -ItemType Directory -Path $pgPassDir -Force | Out-Null
}
$pgPassFile = Join-Path $pgPassDir "pgpass.conf"
Write-Step "Using pgpass file: $pgPassFile"

$connected = $false
$usedPassword = $null

$passwords = @("postgres", "admin", "password", "123456", "12345678", "erp_password", "P@ssw0rd", "Postgres123", "root", "qwerty", "letmein")
foreach ($pw in $passwords) {
    "localhost:5432:*:postgres:$pw" | Set-Content $pgPassFile -Force
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:\PGPASSFILE -ErrorAction SilentlyContinue

    $test = & psql -U postgres -h localhost -p 5432 -tAc "SELECT 1;" 2>&1
    if ($LASTEXITCODE -eq 0) {
        $connected = $true
        $usedPassword = $pw
        Write-Ok "Connected with password: $pw"
        break
    }
}

if (-not $connected) {
    Write-Warn "Cannot connect with common passwords."
    Write-Host ""
    Write-Host "  Please enter your PostgreSQL 'postgres' user password." -ForegroundColor Yellow
    Write-Host "  (Type it then press Enter)" -ForegroundColor Gray
    Write-Host ""
    $pw = Read-Host "Password"
    if ([string]::IsNullOrWhiteSpace($pw)) {
        Write-Fail "Password cannot be empty"
    }
    "localhost:5432:*:postgres:$pw" | Set-Content $pgPassFile -Force
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    $test = & psql -U postgres -h localhost -p 5432 -tAc "SELECT 1;" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Fail "Connection failed. Check your password and try again."
    }
    $usedPassword = $pw
    Write-Ok "Connected with custom password"
}

# 4) Check if databases exist
$dbs = & psql -U postgres -h localhost -p 5432 -tAc "SELECT datname FROM pg_database WHERE datname IN ('erp_system','erp_events')" 2>&1
$hasErpSystem = $dbs -match "erp_system"
$hasErpEvents = $dbs -match "erp_events"

if (-not $hasErpSystem -or -not $hasErpEvents) {
    Write-Step "Creating databases (this is the first time)..."
    # Drop any partial state
    & psql -U postgres -h localhost -p 5432 -c "DROP DATABASE IF EXISTS erp_system;" 2>$null | Out-Null
    & psql -U postgres -h localhost -p 5432 -c "DROP DATABASE IF EXISTS erp_events;" 2>$null | Out-Null
    & psql -U postgres -h localhost -p 5432 -c "DROP USER IF EXISTS erp_user;" 2>$null | Out-Null

    # Create with simple password (works on all PG versions)
    & psql -U postgres -h localhost -p 5432 -c "CREATE USER erp_user WITH PASSWORD 'erp_password' SUPERUSER;" 2>&1 | Out-Null
    & psql -U postgres -h localhost -p 5432 -c "CREATE DATABASE erp_system OWNER erp_user;" 2>&1 | Out-Null
    & psql -U postgres -h localhost -p 5432 -c "CREATE DATABASE erp_events OWNER erp_user;" 2>&1 | Out-Null
    Write-Ok "Databases created: erp_system, erp_events"
} else {
    Write-Ok "Databases exist: erp_system, erp_events"
}

# 5) Self-heal appsettings.json (fix old 'neondb' references)
if (Test-Path $SettingsFile) {
    $content = Get-Content $SettingsFile -Raw
    if ($content -match "Database=neondb") {
        Write-Step "Fixing appsettings.json (neondb → erp_system)..."
        $newContent = $content -replace "Database=neondb", "Database=erp_system"
        [System.IO.File]::WriteAllText($SettingsFile, $newContent)
        Write-Ok "Fixed"
    }
}

# Also fix bin/Debug copy
$binFile = Join-Path $RootDir "src\backend\Host\bin\Debug\net9.0\appsettings.json"
if (Test-Path $binFile) {
    $binContent = Get-Content $binFile -Raw
    if ($binContent -match "Database=neondb") {
        $newBin = $binContent -replace "Database=neondb", "Database=erp_system"
        [System.IO.File]::WriteAllText($binFile, $newBin)
        Write-Ok "Fixed bin/Debug copy"
    }
}

# 6) Install npm dependencies if needed
if (-not (Test-Path (Join-Path $FrontendDir "node_modules"))) {
    Write-Step "Installing npm dependencies (this takes 3-5 minutes)..."
    Push-Location $FrontendDir
    & npm install 2>&1 | Out-Null
    Pop-Location
    if (Test-Path (Join-Path $FrontendDir "node_modules")) {
        Write-Ok "npm dependencies installed"
    } else {
        Write-Warn "npm install may have failed. Will continue anyway."
    }
} else {
    Write-Ok "npm dependencies already installed"
}

# 7) Stop any existing services
Write-Step "Stopping any existing services..."
Get-Process -Name "dotnet","node" -ErrorAction SilentlyContinue | Where-Object {
    $_.CommandLine -match "Host|next" -or $_.Path -match "next-server"
} | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep 2

# 8) Start Backend
Write-Step "Starting Backend (.NET 9) on http://localhost:5000 ..."
$backendProcess = Start-Process -FilePath "dotnet" `
    -ArgumentList "run","--project","Host","--urls","http://localhost:5000" `
    -WorkingDirectory (Join-Path $RootDir "src\backend") `
    -RedirectStandardOutput $BackendLog `
    -RedirectStandardError  $BackendErr `
    -WindowStyle Hidden `
    -PassThru

Write-Step "Waiting for Backend (max 120s)..."
$backendUp = $false
for ($i = 1; $i -le 120; $i++) {
    try {
        $r = Invoke-WebRequest -Uri "http://localhost:5000/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
        if ($r.StatusCode -lt 400) { $backendUp = $true; break }
    } catch {}
    Start-Sleep 1
}
if ($backendUp) {
    Write-Ok "Backend is up (after ${i}s)"
} else {
    Write-Warn "Backend did not respond within 120s"
    Write-Host "    Last lines of error log:" -ForegroundColor Yellow
    if (Test-Path $BackendErr) {
        Get-Content $BackendErr -Tail 15 | ForEach-Object { Write-Host "      $_" -ForegroundColor Gray }
    }
}

# 9) Start Frontend
Write-Step "Starting Frontend (Next.js 14) on http://localhost:3000 ..."
$frontendProcess = Start-Process -FilePath "cmd.exe" `
    -ArgumentList "/c","set NEXT_PUBLIC_API_URL=http://localhost:5000 && npm run dev" `
    -WorkingDirectory $FrontendDir `
    -RedirectStandardOutput $FrontendLog `
    -RedirectStandardError  $FrontendErr `
    -WindowStyle Hidden `
    -PassThru

Write-Step "Waiting for Frontend (max 90s)..."
$frontendUp = $false
for ($i = 1; $i -le 90; $i++) {
    try {
        $r = Invoke-WebRequest -Uri "http://localhost:3000" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
        if ($r.StatusCode -lt 400) { $frontendUp = $true; break }
    } catch {}
    Start-Sleep 1
}
if ($frontendUp) {
    Write-Ok "Frontend is up (after ${i}s)"
} else {
    Write-Warn "Frontend did not respond within 90s"
}

Write-Host ""
if ($backendUp -and $frontendUp) {
    Write-Ok "========================================" -ForegroundColor Green
    Write-Ok "  All services started successfully!" -ForegroundColor Green
    Write-Ok "========================================" -ForegroundColor Green
} else {
    Write-Warn "Some services may not be ready. Check logs in $RootDir"
}
Write-Host ""
Write-Host "  Frontend:  http://localhost:3000"  -ForegroundColor Cyan
Write-Host "  Backend:   http://localhost:5000"  -ForegroundColor Cyan
Write-Host "  Swagger:   http://localhost:5000/swagger" -ForegroundColor Cyan
Write-Host "  Login:     admin@alfajr.local / Demo1234" -ForegroundColor Cyan
Write-Host ""
Write-Host "Commands:" -ForegroundColor Yellow
Write-Host "  .\scripts\quickstart.ps1 -Status" -ForegroundColor Gray
Write-Host "  .\scripts\quickstart.ps1 -Down" -ForegroundColor Gray
Write-Host ""
