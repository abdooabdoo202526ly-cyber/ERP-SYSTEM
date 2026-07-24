# =============================================================================
# Fix PostgreSQL pg_hba.conf — Set trust mode (no password)
# =============================================================================
# ⚠️ SECURITY WARNING: This disables password authentication for local
# connections. Use only for local development. NEVER in production.
# =============================================================================

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

function Write-Step($msg)  { Write-Host "[*] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "[+] $msg" -ForegroundColor Green }
function Write-Warn($msg)  { Write-Host "[!] $msg" -ForegroundColor Yellow }
function Write-Fail($msg)  { Write-Host "[X] $msg" -ForegroundColor Red; exit 1 }

# Find pg_hba.conf
$possiblePaths = @(
    "C:\Program Files\PostgreSQL\17\data\pg_hba.conf",
    "C:\Program Files\PostgreSQL\16\data\pg_hba.conf",
    "C:\Program Files\PostgreSQL\15\data\pg_hba.conf",
    "C:\Program Files\PostgreSQL\14\data\pg_hba.conf",
    "C:\Program Files (x86)\PostgreSQL\17\data\pg_hba.conf"
)

$pgHba = $null
foreach ($p in $possiblePaths) {
    if (Test-Path $p) {
        $pgHba = $p
        break
    }
}

if (-not $pgHba) {
    Write-Fail "pg_hba.conf not found. Search manually in: C:\Program Files\PostgreSQL*\data\"
}

Write-Step "Found: $pgHba"
Write-Warn "This will disable password auth for LOCAL connections (dev only)"
Write-Host ""

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Fail "Must run as Administrator! Right-click PowerShell → 'Run as administrator'"
}

# Backup
$backup = "$pgHba.bak.$(Get-Date -Format 'yyyyMMddHHmmss')"
Copy-Item $pgHba $backup
Write-Ok "Backup created: $backup"

# Read current
$content = Get-Content $pgHba -Raw

# Show current settings for these lines
Write-Host ""
Write-Host "Current lines (127.0.0.1/32 and ::1/128):" -ForegroundColor Yellow
$content -split "`n" | Where-Object { $_ -match "127\.0\.0\.1/32|::1/128" } | ForEach-Object {
    Write-Host "  $_" -ForegroundColor Gray
}
Write-Host ""

# Replace scram-sha-256 / md5 with trust for these lines
$newContent = $content -replace "(host\s+all\s+all\s+127\.0\.0\.1/32\s+)\S+", '$1trust'
$newContent = $newContent -replace "(host\s+all\s+all\s+::1/128\s+)\S+", '$1trust'

if ($newContent -eq $content) {
    Write-Warn "No changes needed (already 'trust' or format different)"
} else {
    [System.IO.File]::WriteAllText($pgHba, $newContent)
    Write-Ok "Updated pg_hba.conf"
}

# Show new settings
Write-Host ""
Write-Host "New lines:" -ForegroundColor Yellow
$content -split "`n" | Where-Object { $_ -match "127\.0\.0\.1/32|::1/128" } | ForEach-Object {
    Write-Host "  $_" -ForegroundColor Gray
}
Write-Host ""

# Restart PostgreSQL
Write-Step "Restarting PostgreSQL service..."
$svc = Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($svc) {
    try {
        Restart-Service $svc.Name -Force -ErrorAction Stop
        Write-Ok "Service restarted: $($svc.Name)"
    } catch {
        Write-Warn "Cannot restart automatically. Please restart manually:"
        Write-Host "    Restart-Service $($svc.Name)" -ForegroundColor Gray
    }
} else {
    Write-Warn "PostgreSQL service not found"
}

# Test
Write-Step "Testing connection (no password)..."
$env:PGPASSWORD = ""
Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
$test = & psql -U postgres -h localhost -p 5432 -tAc "SELECT 1;" 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Ok "Connection works without password!"
} else {
    Write-Warn "Test failed: $test"
}

Write-Host ""
Write-Ok "Done! Now run: .\scripts\quickstart.ps1"
Write-Host ""
