# =============================================================================
# ERP-SYSTEM v1.0.15 - DATABASE DIAGNOSTIC
# =============================================================================
# يكشف بالضبط إيش في الـ DB:
#   1. عدد الـ tables
#   2. لو accounts موجود
#   3. الـ schemas المتاحة
#   4. الـ user permissions
#   5. يفجر SELECT مباشر على accounts
#
# Usage:
#   .\scripts\DIAGNOSE-DB.ps1
# =============================================================================

[CmdletBinding()]
param(
    [switch]$Help
)

$ErrorActionPreference = "Continue"

if ($Help) { Get-Help $MyInvocation.MyCommand.Path; exit 0 }

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  ERP-SYSTEM v1.0.15 - Database Diagnostic" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# Check postgres container
$pgRunning = docker ps --format "{{.Names}}" 2>$null | Select-String -Pattern "^erp-postgres$"
if (-not $pgRunning) {
    Write-Host "[X] erp-postgres is NOT running" -ForegroundColor Red
    Write-Host "    Start it: docker compose -p erp-system -f infra\docker\docker-compose.dev.yml up -d postgres" -ForegroundColor Yellow
    exit 1
}
Write-Host "[+] erp-postgres is running" -ForegroundColor Green

# Run diagnostics
$queries = @(
    @{ Name = "Postgres version"; Sql = "SELECT version();" }
    @{ Name = "Current DB"; Sql = "SELECT current_database(), current_user, current_schema();" }
    @{ Name = "All schemas"; Sql = "SELECT schema_name FROM information_schema.schemata ORDER BY schema_name;" }
    @{ Name = "All tables count"; Sql = "SELECT COUNT(*) AS table_count FROM information_schema.tables WHERE table_schema = 'public';" }
    @{ Name = "Critical tables check"; Sql = "SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename IN ('tenants','users','accounts','customers','vendors','items','purchase_orders','sales_invoices','audit_log','companies') ORDER BY tablename;" }
    @{ Name = "accounts table existence"; Sql = "SELECT EXISTS (SELECT FROM pg_tables WHERE schemaname = 'public' AND tablename = 'accounts') AS exists;" }
    @{ Name = "accounts columns"; Sql = "SELECT column_name, data_type FROM information_schema.columns WHERE table_name = 'accounts' ORDER BY ordinal_position;" }
    @{ Name = "Try direct SELECT accounts"; Sql = "SELECT COUNT(*) AS row_count FROM accounts;" }
    @{ Name = "SchemaMigrator log"; Sql = "DO $$ BEGIN RAISE NOTICE 'See docker logs erp-api | grep SchemaMigrator'; END $$;" }
    @{ Name = "DataTypeMigrator log hint"; Sql = "DO $$ BEGIN RAISE NOTICE 'Run: docker logs erp-api 2>&1 | grep DataTypeMigrator | tail -20'; END $$;" }
)

foreach ($q in $queries) {
    Write-Host ""
    Write-Host "[*] $($q.Name)" -ForegroundColor Cyan
    $result = docker exec erp-postgres psql -U erp_user -d erp_system -tA -c $q.Sql 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "    $($result | Out-String).Trim()" -ForegroundColor Gray
    } else {
        Write-Host "    [X] FAILED: $result" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Additional checks" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# Check API log for SchemaMigrator
Write-Host ""
Write-Host "[*] SchemaMigrator log entries:" -ForegroundColor Cyan
$schemaLog = docker logs erp-api --tail 200 2>&1 | Select-String "SchemaMigrator"
if ($schemaLog) {
    $schemaLog | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
} else {
    Write-Host "    (no SchemaMigrator log found)" -ForegroundColor Yellow
}

# Check DataTypeMigrator results
Write-Host ""
Write-Host "[*] DataTypeMigrator summary:" -ForegroundColor Cyan
$dataTypeLog = docker logs erp-api --tail 500 2>&1 | Select-String "DataTypeMigrator.*Done" | Select-Object -Last 1
if ($dataTypeLog) {
    Write-Host "    $dataTypeLog" -ForegroundColor Gray
} else {
    Write-Host "    (no DataTypeMigrator summary found)" -ForegroundColor Yellow
}

# Most recent errors
Write-Host ""
Write-Host "[*] Most recent API errors (accounts):" -ForegroundColor Cyan
$recentErrors = docker logs erp-api --tail 200 2>&1 | Select-String "finance/accounts" | Select-Object -First 5
if ($recentErrors) {
    $recentErrors | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
} else {
    Write-Host "    (no recent errors)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  NEXT STEPS" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "If 'accounts table' shows MISSING:" -ForegroundColor Yellow
Write-Host "  1. cd F:\erpsystem7-23-2026\ERP-SYSTEM-v1.0.15" -ForegroundColor White
Write-Host "  2. .\scripts\INSTALL-ULTIMATE.ps1 -Reset" -ForegroundColor White
Write-Host "  3. Wait for the script to finish" -ForegroundColor White
Write-Host "  4. The 'Found 51 tables' line should appear" -ForegroundColor White
Write-Host ""
Write-Host "If accounts exists but query fails:" -ForegroundColor Yellow
Write-Host "  -> schema corruption, run: .\scripts\EMERGENCY-RESET-DB.ps1" -ForegroundColor White
Write-Host ""
Write-Host "Copy/paste the output above and send to Mavis for diagnosis." -ForegroundColor Cyan
