# =============================================================================
# Fix appsettings.json — Database name (neondb → erp_system)
# =============================================================================
# Self-healing: يصلح تلقائياً إذا appsettings.json ما زال يحوي 'neondb'
# =============================================================================

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir   = Resolve-Path "$ScriptDir\.."
$SettingsFile = Join-Path $RootDir "src\backend\Host\appsettings.json"

function Write-Step($msg)  { Write-Host "[*] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "[+] $msg" -ForegroundColor Green }
function Write-Warn($msg)  { Write-Host "[!] $msg" -ForegroundColor Yellow }
function Write-Fail($msg)  { Write-Host "[X] $msg" -ForegroundColor Red }

Write-Host ""
Write-Host "=== Fix appsettings.json ===" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $SettingsFile)) {
    Write-Fail "Not found: $SettingsFile"
}

$content = Get-Content $SettingsFile -Raw

if ($content -match "Database=neondb") {
    Write-Warn "Found 'neondb' in appsettings.json - FIXING..."
    $newContent = $content -replace "Database=neondb", "Database=erp_system"
    Set-Content -Path $SettingsFile -Value $newContent -NoNewline
    Write-Ok "Fixed: neondb → erp_system"
} elseif ($content -match "Database=erp_system") {
    Write-Ok "Already correct (Database=erp_system)"
} else {
    Write-Warn "Unknown database name in appsettings.json"
    Write-Host "  Current Postgres line:"
    $content -split "`n" | Select-String "Postgres" | ForEach-Object { Write-Host "    $_" }
}

# Also fix bin/Debug copy
$binFile = Join-Path $RootDir "src\backend\Host\bin\Debug\net9.0\appsettings.json"
if (Test-Path $binFile) {
    $binContent = Get-Content $binFile -Raw
    if ($binContent -match "Database=neondb") {
        Write-Warn "Found 'neondb' in bin/Debug copy - FIXING..."
        $newBin = $binContent -replace "Database=neondb", "Database=erp_system"
        Set-Content -Path $binFile -Value $newBin -NoNewline
        Write-Ok "Fixed bin/Debug copy"
    }
}

Write-Host ""
Write-Host "Current connection string:" -ForegroundColor Cyan
Get-Content $SettingsFile | Select-String "Postgres" | ForEach-Object {
    Write-Host "  $_" -ForegroundColor Gray
}
Write-Host ""
Write-Ok "Done! Restart the backend to apply changes."
Write-Host ""
Write-Host "Restart:" -ForegroundColor Cyan
Write-Host "  .\scripts\start-native.ps1 -Down"
Write-Host "  .\scripts\start-native.ps1"
Write-Host ""
