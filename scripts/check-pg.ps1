# =============================================================================
# ERP-SYSTEM - PostgreSQL Check & Repair
# =============================================================================
# Comprehensive PostgreSQL diagnosis for Windows + native mode
# =============================================================================

[CmdletBinding()]
param()

$ErrorActionPreference = "Continue"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

function Write-Step($msg)  { Write-Host "[*] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "[+] $msg" -ForegroundColor Green }
function Write-Warn($msg)  { Write-Host "[!] $msg" -ForegroundColor Yellow }
function Write-Fail($msg)  { Write-Host "[X] $msg" -ForegroundColor Red }

Write-Host ""
Write-Host "=== ERP-SYSTEM PostgreSQL Check ===" -ForegroundColor Cyan
Write-Host ""

# 1. Check if psql is in PATH
Write-Step "1. Checking psql in PATH..."
$psql = Get-Command psql -ErrorAction SilentlyContinue
if ($psql) {
    Write-Ok "psql found: $($psql.Source)"
    $version = & psql --version
    Write-Host "    $version"
} else {
    Write-Warn "psql NOT found in PATH"
    Write-Host ""
    Write-Host "  Options:" -ForegroundColor Yellow
    Write-Host "    1. Install PostgreSQL 15+: https://www.postgresql.org/download/windows/"
    Write-Host "    2. Add psql to PATH: `$env:Path += ';C:\Program Files\PostgreSQL\15\bin'"
    Write-Host ""
    $commonPaths = @(
        "C:\Program Files\PostgreSQL\15\bin\psql.exe",
        "C:\Program Files\PostgreSQL\16\bin\psql.exe",
        "C:\Program Files (x86)\PostgreSQL\15\bin\psql.exe"
    )
    foreach ($p in $commonPaths) {
        if (Test-Path $p) {
            Write-Warn "Found at: $p (not in PATH)"
            Write-Host "  Add to PATH: `$env:Path += ';$(Split-Path $p)'"
        }
    }
}

Write-Host ""

# 2. Check if PostgreSQL service is running
Write-Step "2. Checking PostgreSQL service..."
$pgService = Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($pgService) {
    Write-Host "    Service name: $($pgService.Name)"
    Write-Host "    Status: $($pgService.Status)"
    if ($pgService.Status -ne "Running") {
        Write-Warn "PostgreSQL service is NOT running. Starting..."
        try {
            Start-Service $pgService.Name -ErrorAction Stop
            Write-Ok "Service started"
        } catch {
            Write-Fail "Cannot start service: $_"
            Write-Host "  Try as Administrator: Start-Service $($pgService.Name)"
        }
    } else {
        Write-Ok "Service is running"
    }
} else {
    Write-Warn "No PostgreSQL service found"
    Write-Host "  Install PostgreSQL 15+ from https://www.postgresql.org/download/windows/"
}

Write-Host ""

# 3. Check port 5432
Write-Step "3. Checking port 5432..."
$port5432 = Test-NetConnection -ComputerName localhost -Port 5432 -WarningAction SilentlyContinue
if ($port5432.TcpTestSucceeded) {
    Write-Ok "Port 5432 is OPEN"
} else {
    Write-Warn "Port 5432 is CLOSED"
    Write-Host "  PostgreSQL is not listening. Start the service first."
}

Write-Host ""

# 4. Test connection
if ($psql -and $port5432.TcpTestSucceeded) {
    Write-Step "4. Testing connection to PostgreSQL..."
    $env:PGPASSWORD = "postgres"
    $result = & psql -U postgres -h localhost -p 5432 -c "SELECT version();" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Ok "Connection successful"
        Write-Host "    $result"
    } else {
        Write-Warn "Connection failed"
        Write-Host "    $result"
    }

    Write-Host ""

    # 5. Check if databases exist
    Write-Step "5. Checking ERP databases..."
    $dbs = & psql -U postgres -h localhost -p 5432 -c "SELECT datname FROM pg_database WHERE datname LIKE 'erp_%';" 2>&1
    if ($LASTEXITCODE -eq 0) {
        if ($dbs -match "erp_system") { Write-Ok "erp_system exists" } else { Write-Warn "erp_system MISSING" }
        if ($dbs -match "erp_events") { Write-Ok "erp_events exists" } else { Write-Warn "erp_events MISSING" }
    }

    Write-Host ""

    # 6. Create databases if missing
    if ($dbs -notmatch "erp_system" -or $dbs -notmatch "erp_events") {
        Write-Step "6. Creating missing databases..."
        $env:PGPASSWORD = "postgres"
        & psql -U postgres -h localhost -p 5432 -c "CREATE USER erp_user WITH PASSWORD 'erp_password';" 2>&1 | Out-Null
        & psql -U postgres -h localhost -p 5432 -c "CREATE DATABASE erp_system OWNER erp_user;" 2>&1 | Out-Null
        & psql -U postgres -h localhost -p 5432 -c "CREATE DATABASE erp_events OWNER erp_user;" 2>&1 | Out-Null
        Write-Ok "Databases created"
    }
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "If everything is OK, run:    .\scripts\start-native.ps1"
Write-Host "To start:                    .\scripts\start-native.ps1"
Write-Host "To check health:             .\scripts\start-native.ps1 -Status"
Write-Host "To stop:                     .\scripts\start-native.ps1 -Down"
Write-Host ""
Write-Host "If you see [X] errors:" -ForegroundColor Yellow
Write-Host "  1. Install PostgreSQL: https://www.postgresql.org/download/windows/"
Write-Host "  2. Add psql to PATH: `$env:Path += ';C:\Program Files\PostgreSQL\15\bin'"
Write-Host "  3. Run: .\scripts\check-pg.ps1 (this script)"
Write-Host "  4. Run: .\scripts\start-native.ps1 -Setup"
Write-Host "  5. Run: .\scripts\start-native.ps1"
Write-Host ""
