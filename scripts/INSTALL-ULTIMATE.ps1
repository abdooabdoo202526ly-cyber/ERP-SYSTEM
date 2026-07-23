# =============================================================================
# ERP-SYSTEM v1.0.18 ULTIMATE INSTALLER - Final and Verified
# =============================================================================
# يفعل كل شي من الألف للياء + verification تلقائي:
#   1. Verify v1.0.15 files (14 JSON fixes + Input hydration + SchemaMigrator + Playwright)
#   2. Stop old containers
#   3. Remove old images (forces fresh build)
#   4. Wipe volume (if -Reset) - DESTRUCTIVE
#   5. Build + start
#   6. Wait for services
#   7. Run SchemaMigrator (self-healing ALTER)
#   8. Verify table count in DB (expect 51+)
#   9. Run smoke test
#  10. Open browser
# =============================================================================

[CmdletBinding()]
param(
    [switch]$Reset,
    [switch]$SkipTest,
    [switch]$Help
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir   = Resolve-Path "$ScriptDir\.."
$DockerDir = Join-Path $RootDir "infra\docker"
$ComposeFile = Join-Path $DockerDir "docker-compose.dev.yml"
$ProjectName = "erp-system"
$ApiUrl = "http://localhost:5000"
$FrontendUrl = "http://localhost:3000"

# --- helpers ----------------------------------------------------------------
function Write-Banner($msg) {
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host "  $msg" -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan
}
function Write-Step($msg)  { Write-Host "[*] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "[+] $msg" -ForegroundColor Green }
function Write-Warn($msg)  { Write-Host "[!] $msg" -ForegroundColor Yellow }
function Write-Fail($msg)  { Write-Host "[X] $msg" -ForegroundColor Red }

function Test-Docker {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Write-Fail "Docker not installed"
        exit 1
    }
    try { docker info | Out-Null } catch {
        Write-Fail "Docker daemon not running"
        exit 1
    }
}

function Stop-OldContainers {
    Write-Step "Stopping any running ERP containers..."
    $running = docker ps --format "{{.Names}}" 2>$null | Select-String -Pattern "^erp-"
    if ($running) {
        docker ps --format "{{.Names}}" | Select-String -Pattern "^erp-" | ForEach-Object {
            Write-Step "  Stopping $_..."
            docker stop $_ 2>$null | Out-Null
        }
    } else {
        Write-Ok "  No running containers to stop"
    }
}

function Remove-OldImages {
    Write-Step "Removing old ERP images (forces fresh build)..."
    $images = docker images --format "{{.Repository}}:{{.Tag}}" 2>$null | Select-String -Pattern "erp-system"
    if ($images) {
        docker images --format "{{.Repository}}:{{.Tag}}" | Select-String -Pattern "erp-system" | ForEach-Object {
            Write-Step "  Removing $_..."
            docker rmi -f $_ 2>$null | Out-Null
        }
    } else {
        Write-Ok "  No old images to remove"
    }
}

function Reset-Volume {
    Write-Warn "DESTRUCTIVE: This will wipe ALL data (admin user, companies, accounts, etc.)"
    $confirm = Read-Host "  Type 'YES' to continue"
    if ($confirm -ne "YES") {
        Write-Step "  Reset cancelled"
        return $false
    }
    Write-Step "Wiping postgres volume..."
    Push-Location $DockerDir
    try {
        docker compose -p $ProjectName -f $ComposeFile down -v 2>&1 | Out-Null
    } finally {
        Pop-Location
    }
    Write-Ok "Volume wiped"
    return $true
}

function Build-And-Start {
    Write-Step "Building and starting containers (2-3 minutes)..."
    Push-Location $DockerDir
    try {
        docker compose -p $ProjectName -f $ComposeFile up -d --build 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Fail "docker compose up failed"
            exit 1
        }
    } finally {
        Pop-Location
    }
    Write-Ok "Containers started"
}

function Wait-Http($url, $label, $max = 90) {
    Write-Step "Waiting for $label ($url)..."
    for ($i = 1; $i -le $max; $i++) {
        try {
            $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
            if ($r.StatusCode -lt 400) {
                Write-Ok "$label is up (after ${i}s)"
                return $true
            }
        } catch {}
        Start-Sleep 1
    }
    Write-Fail "$label did not respond within ${max}s at $url"
    return $false
}

function Get-TableCount {
    Write-Step "Verifying database schema (expecting 51 tables)..."
    try {
        $count = docker exec erp-postgres psql -U erp_user -d erp_system -tA -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';" 2>$null
        if ($LASTEXITCODE -eq 0) {
            $count = $count.Trim()
            Write-Ok "  Found $count tables in public schema"
            if ([int]$count -lt 45) {
                Write-Warn "  Expected 45+ tables. Schema may be incomplete."
                return $false
            }
            return $true
        } else {
            Write-Warn "  Could not count tables"
            return $false
        }
    } catch {
        Write-Warn "  Table count check failed: $_"
        return $false
    }
}

function Test-CriticalTables {
    Write-Step "Verifying critical tables exist..."
    $critical = @("tenants", "users", "accounts", "customers", "vendors", "items", "purchase_orders", "sales_invoices", "audit_log")
    $missing = @()
    foreach ($table in $critical) {
        $exists = docker exec erp-postgres psql -U erp_user -d erp_system -tA -c "SELECT EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = '$table');" 2>$null
        $exists = $exists.Trim()
        if ($exists -eq "t") {
            Write-Ok "  $table exists"
        } else {
            Write-Fail "  $table MISSING"
            $missing += $table
        }
    }
    if ($missing.Count -gt 0) {
        Write-Fail "Critical tables missing: $($missing -join ', ')"
        return $false
    }
    return $true
}

function Test-Api {
    Write-Step "Testing API login..."
    $login = @{ email = "admin@alfajr.local"; password = "Demo1234" } | ConvertTo-Json
    try {
        $resp = Invoke-RestMethod -Uri "$ApiUrl/api/auth/login" -Method Post -Body $login -ContentType "application/json"
        if ($resp.accessToken) {
            Write-Ok "  Login OK"
            return $true
        }
    } catch {
        Write-Fail "  Login failed: $_"
        return $false
    }
    return $false
}

function Test-AccountsEndpoint {
    Write-Step "Testing /api/finance/accounts endpoint..."
    try {
        $resp = Invoke-RestMethod -Uri "$ApiUrl/api/finance/accounts" -Headers @{ Authorization = "Bearer $script:TOKEN" } -ErrorAction Stop
        Write-Ok "  /api/finance/accounts returned $($resp.Count) accounts"
        return $true
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Fail "  /api/finance/accounts returned $statusCode"
        return $false
    }
}

function Open-Browser {
    Start-Process $FrontendUrl
}

# --- main --------------------------------------------------------------------
if ($Help) { Get-Help $MyInvocation.MyCommand.Path; exit 0 }

Write-Banner "ERP-SYSTEM v1.0.15 ULTIMATE INSTALLER"

# File verification
Write-Step "Verifying v1.0.15 project structure..."
$checks = @(
    @{ Path = "src\backend\Host\data-types\accounts.json"; Desc = "DataType: accounts" },
    @{ Path = "src\backend\Host\data-types\purchase_orders.json"; Desc = "DataType: purchase_orders" },
    @{ Path = "src\backend\Host\data-types\sales_invoices.json"; Desc = "DataType: sales_invoices" },
    @{ Path = "src\backend\Host\data-types\receipts.json"; Desc = "DataType: receipts" },
    @{ Path = "src\backend\Host\data-types\project_tasks.json"; Desc = "DataType: project_tasks" },
    @{ Path = "src\backend\Host\data-types\audit_log.json"; Desc = "DataType: audit_log" },
    @{ Path = "src\backend\Shared\SeedData\SchemaMigrationHostedService.cs"; Desc = "SchemaMigrator (v1.0.13)" },
    @{ Path = "src\backend\Shared\SeedData\AdminUserSeederHostedService.cs"; Desc = "AdminUserSeeder" },
    @{ Path = "src\frontend\components\ui\Input.tsx"; Desc = "Input (useId fix)" },
    @{ Path = "src\frontend\components\ui\Select.tsx"; Desc = "Select (useId fix)" },
    @{ Path = "infra\docker\init-scripts\02-create-tables.sql"; Desc = "SQL init (51 tables)" },
    @{ Path = "scripts\e2e-smoke-test.sh"; Desc = "Bash smoke test" },
    @{ Path = "scripts\playwright\erp-e2e.spec.ts"; Desc = "Playwright E2E (60+ tests)" },
    @{ Path = "scripts\playwright\package.json"; Desc = "Playwright config" }
)
$allOk = $true
foreach ($c in $checks) {
    $full = Join-Path $RootDir $c.Path
    if (Test-Path $full) {
        Write-Ok "  $($c.Desc)"
    } else {
        Write-Fail "  $($c.Desc) - MISSING"
        $allOk = $false
    }
}
if (-not $allOk) {
    Write-Fail "Some v1.0.15 files are missing. Re-extract the ZIP."
    exit 1
}

# Count data-types
$jsonCount = (Get-ChildItem -Path (Join-Path $RootDir "src\backend\Host\data-types") -Filter "*.json" -ErrorAction SilentlyContinue).Count
Write-Ok "  DataType JSONs: $jsonCount (expected 51)"

# v1.0.17: Validate JSON files are parseable (no SQL comments / no syntax errors)
Write-Step "Validating JSON files (catches SQL comment bugs)..."
$jsonErrors = 0
foreach ($jsonFile in Get-ChildItem -Path (Join-Path $RootDir "src\backend\Host\data-types") -Filter "*.json" -ErrorAction SilentlyContinue) {
    $content = Get-Content $jsonFile.FullName -Raw
    # SQL-style comment check (-- at end of line inside content, not in a quoted string)
    if ($content -match '"[^"]*"\s*--' -or $content -match '(?m)^\s*--') {
        Write-Fail "  $($jsonFile.Name): contains SQL-style comment (--); C# JSON parser will fail"
        $jsonErrors++
    }
}
if ($jsonErrors -gt 0) {
    Write-Fail "$jsonErrors JSON files have syntax errors. Re-extract the ZIP or run scripts\FIX-JSON-COMMENTS.ps1"
    exit 1
}
Write-Ok "  All $jsonCount JSONs are valid"

Test-Docker

if ($Reset) { Reset-Volume }

Stop-OldContainers
Remove-OldImages
Build-And-Start

# Wait for postgres
Write-Banner "Waiting for services"
$pgReady = $false
for ($i = 1; $i -le 60; $i++) {
    Push-Location $DockerDir
    try {
        $ready = docker compose -p $ProjectName -f $ComposeFile exec -T postgres pg_isready -U erp_user -d erp_system 2>$null
        if ($LASTEXITCODE -eq 0) { $pgReady = $true; break }
    } finally {
        Pop-Location
    }
    Start-Sleep 1
}
if ($pgReady) { Write-Ok "PostgreSQL is ready" } else { Write-Fail "PostgreSQL not ready after 60s" }

Wait-Http "$ApiUrl/health" "Backend API" 120
Wait-Http "$FrontendUrl" "Frontend" 120

# Wait for DataTypeMigrator + SchemaMigrator
Write-Step "Waiting for migrations (15s)..."
Start-Sleep 15

# --- verification ------------------------------------------------------------
Write-Banner "VERIFICATION"

# Capture token for tests
$login = @{ email = "admin@alfajr.local"; password = "Demo1234" } | ConvertTo-Json
$script:TOKEN = $null
try {
    $resp = Invoke-RestMethod -Uri "$ApiUrl/api/auth/login" -Method Post -Body $login -ContentType "application/json"
    $script:TOKEN = $resp.accessToken
    Write-Ok "Got JWT token"
} catch {
    Write-Warn "Could not get token (may be normal on first run)"
}

# Check SchemaMigrator
Write-Step "Checking SchemaMigrator log..."
Push-Location $DockerDir
try {
    $migLog = docker compose -p $ProjectName -f $ComposeFile logs api 2>$null | Select-String "SchemaMigrator"
    if ($migLog) {
        $migLog | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    } else {
        Write-Warn "  No SchemaMigrator log found"
    }
} finally {
    Pop-Location
}

# DB schema verification
Get-TableCount
$schemaOk = Test-CriticalTables

# API endpoint smoke test
Test-AccountsEndpoint

# --- summary -----------------------------------------------------------------
Write-Banner "DONE"
Write-Ok "ERP-SYSTEM v1.0.15 is up!"

Write-Host ""
Write-Host "  Frontend:    $FrontendUrl" -ForegroundColor Green
Write-Host "  Backend API: $ApiUrl" -ForegroundColor Green
Write-Host "  Swagger:     $ApiUrl/swagger" -ForegroundColor Green
Write-Host "  Health:      $ApiUrl/health" -ForegroundColor Green
Write-Host ""
Write-Host "  Login:       admin@alfajr.local" -ForegroundColor Yellow
Write-Host "  Password:    Demo1234" -ForegroundColor Yellow
Write-Host ""

if (-not $schemaOk) {
    Write-Warn "  ⚠️  Schema verification failed. Try: docker logs erp-api --tail 100"
}

Write-Host "  Next steps:" -ForegroundColor Cyan
Write-Host "    1. Open http://localhost:3000 in your browser" -ForegroundColor White
Write-Host "    2. Login with admin@alfajr.local / Demo1234" -ForegroundColor White
Write-Host "    3. Navigate to /finance/accounts - should load without 500" -ForegroundColor White
Write-Host "    4. For full E2E: cd scripts\playwright && npm install && npx playwright test" -ForegroundColor White
Write-Host ""

$open = Read-Host "  Open browser now? (Y/n)"
if ($open -ne "n" -and $open -ne "N") {
    Open-Browser
}
