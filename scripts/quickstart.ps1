# =============================================================================
# ERP-SYSTEM - Simple Start (FINAL, Minimal)
# =============================================================================
# يعتمد على:
# 1. PostgreSQL 17 مثبت (افتراضي بدون كلمة سر / trust mode)
# 2. .NET 9 SDK
# 3. Node.js 20+
#
# شغّل من PowerShell عادي (مش Administrator):
#   cd "F:\erpsystem7-22-2026\ERP-SYSTEM-v1.0.34-hotfix2 (2)"
#   .\scripts\quickstart.ps1
# =============================================================================

[CmdletBinding()]
param()

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir   = Resolve-Path "$ScriptDir\.."
$BackendLog   = Join-Path $RootDir ".backend.log"
$BackendErr   = Join-Path $RootDir ".backend.err.log"
$FrontendLog  = Join-Path $RootDir ".frontend.log"
$FrontendErr  = Join-Path $RootDir ".frontend.err.log"
$FrontendDir  = Join-Path $RootDir "src\frontend"
$SettingsFile = Join-Path $RootDir "src\backend\Host\appsettings.json"

function Write-Step($msg)  { Write-Host "[*] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "[+] $msg" -ForegroundColor Green }
function Write-Warn($msg)  { Write-Host "[!] $msg" -ForegroundColor Yellow }
function Write-Fail($msg)  { Write-Host "[X] $msg" -ForegroundColor Red; exit 1 }

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "     ERP-SYSTEM (Simple Start)" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# 1) Find psql
$psql = $null
$PsqlPaths = @(
    "C:\Program Files\PostgreSQL\17\bin\psql.exe",
    "C:\Program Files\PostgreSQL\16\bin\psql.exe",
    "C:\Program Files\PostgreSQL\15\bin\psql.exe"
)
foreach ($p in $PsqlPaths) {
    if (Test-Path $p) {
        $env:Path = "$env:Path;$(Split-Path $p)"
        $psql = Get-Command psql
        Write-Ok "psql: $p"
        break
    }
}
if (-not $psql) {
    Write-Fail "psql not found. Install PostgreSQL 15+ first."
}

# 2) Test connection (trust mode expected)
Write-Step "Testing PostgreSQL connection..."
$test = & psql -U postgres -h localhost -p 5432 -tAc "SELECT 1;" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Warn "Cannot connect without password."
    Write-Host ""
    Write-Host "  AUTO-FIX: Restarting PostgreSQL service to apply pg_hba.conf..." -ForegroundColor Cyan
    $svc = Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($svc) {
        try {
            Restart-Service $svc.Name -Force -ErrorAction Stop
            Write-Ok "Service restarted. Waiting 3s..."
            Start-Sleep 3
        } catch {
            Write-Warn "Cannot restart as non-admin. Run this in Administrator PowerShell:"
            Write-Host "    Restart-Service $($svc.Name)" -ForegroundColor Yellow
        }
    }
    # Test again
    $test = & psql -U postgres -h localhost -p 5432 -tAc "SELECT 1;" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Warn "Still cannot connect. The pg_hba.conf needs editing."
        Write-Host ""
        Write-Host "  MANUAL FIX:" -ForegroundColor Yellow
        Write-Host "    1. Open: C:\Program Files\PostgreSQL\17\data\pg_hba.conf" -ForegroundColor White
        Write-Host "    2. Find lines with '127.0.0.1/32' and '::1/128'" -ForegroundColor White
        Write-Host "    3. Change 'scram-sha-256' to 'trust'" -ForegroundColor White
        Write-Host "    4. Save and run: Restart-Service postgresql-x64-17" -ForegroundColor White
        Write-Host ""
        Write-Fail "Fix pg_hba.conf and run again."
    }
    Write-Ok "Connected after restart!"
}
Write-Ok "PostgreSQL is up on :5432"

# 3) Create databases if missing
Write-Step "Checking databases..."
$dbs = & psql -U postgres -h localhost -p 5432 -tAc "SELECT datname FROM pg_database WHERE datname IN ('erp_system','erp_events')" 2>&1
$hasErpSystem = $dbs -match "erp_system"
$hasErpEvents = $dbs -match "erp_events"

if (-not $hasErpSystem) {
    Write-Step "Creating erp_system..."
    & psql -U postgres -h localhost -p 5432 -c "CREATE DATABASE erp_system;" 2>$null | Out-Null
    Write-Ok "erp_system created"
} else {
    Write-Ok "erp_system exists"
}

if (-not $hasErpEvents) {
    Write-Step "Creating erp_events..."
    & psql -U postgres -h localhost -p 5432 -c "CREATE DATABASE erp_events;" 2>$null | Out-Null
    Write-Ok "erp_events created"
} else {
    Write-Ok "erp_events exists"
}

# 4) Fix appsettings.json
if (Test-Path $SettingsFile) {
    $content = Get-Content $SettingsFile -Raw
    if ($content -match "Database=neondb") {
        Write-Step "Fixing appsettings.json..."
        $new = $content -replace "Database=neondb", "Database=erp_system"
        [System.IO.File]::WriteAllText($SettingsFile, $new)
        Write-Ok "Fixed"
    }
}

# 5) Install npm deps if missing
if (-not (Test-Path (Join-Path $FrontendDir "node_modules"))) {
    Write-Step "Installing npm dependencies (3-5 min)..."
    Push-Location $FrontendDir
    & npm install 2>&1 | Out-Null
    Pop-Location
    Write-Ok "npm done"
} else {
    Write-Ok "npm deps OK"
}

# 6) Stop existing
Write-Step "Stopping existing services..."
Get-Process -Name "dotnet","node" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep 2

# 7) Start Backend
Write-Step "Starting Backend on :5000..."
Start-Process -FilePath "dotnet" `
    -ArgumentList "run","--project","Host","--urls","http://localhost:5000" `
    -WorkingDirectory (Join-Path $RootDir "src\backend") `
    -RedirectStandardOutput $BackendLog `
    -RedirectStandardError  $BackendErr `
    -WindowStyle Hidden

# 8) Wait Backend
$backendUp = $false
for ($i = 1; $i -le 90; $i++) {
    try {
        $r = Invoke-WebRequest -Uri "http://localhost:5000/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
        if ($r.StatusCode -lt 400) { $backendUp = $true; break }
    } catch {}
    Start-Sleep 1
}
if ($backendUp) {
    Write-Ok "Backend up (after ${i}s)"
} else {
    Write-Warn "Backend not responding after 90s"
    if (Test-Path $BackendErr) {
        Write-Host "Last error lines:" -ForegroundColor Red
        Get-Content $BackendErr -Tail 8 | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    }
}

# 9) Start Frontend
Write-Step "Starting Frontend on :3000..."
Start-Process -FilePath "cmd.exe" `
    -ArgumentList "/c","set NEXT_PUBLIC_API_URL=http://localhost:5000 && npm run dev" `
    -WorkingDirectory $FrontendDir `
    -RedirectStandardOutput $FrontendLog `
    -RedirectStandardError  $FrontendErr `
    -WindowStyle Hidden

# 10) Wait Frontend
$frontendUp = $false
for ($i = 1; $i -le 90; $i++) {
    try {
        $r = Invoke-WebRequest -Uri "http://localhost:3000" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
        if ($r.StatusCode -lt 400) { $frontendUp = $true; break }
    } catch {}
    Start-Sleep 1
}
if ($frontendUp) {
    Write-Ok "Frontend up (after ${i}s)"
} else {
    Write-Warn "Frontend not responding after 90s"
}

Write-Host ""
if ($backendUp -and $frontendUp) {
    Write-Ok "ALL SERVICES RUNNING!" -ForegroundColor Green
}
Write-Host ""
Write-Host "  Frontend:  http://localhost:3000"  -ForegroundColor Cyan
Write-Host "  Backend:   http://localhost:5000"  -ForegroundColor Cyan
Write-Host "  Login:     admin@alfajr.local / Demo1234" -ForegroundColor Cyan
Write-Host ""
