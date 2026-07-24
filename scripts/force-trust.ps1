# =============================================================================
# Force pg_hba.conf to TRUST mode (auto-edit)
# =============================================================================
# This script WILL modify pg_hba.conf directly using PowerShell.
# Run as Administrator!
# =============================================================================

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

function Write-Step($msg)  { Write-Host "[*] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "[+] $msg" -ForegroundColor Green }
function Write-Warn($msg)  { Write-Host "[!] $msg" -ForegroundColor Yellow }

# Find pg_hba.conf
$Paths = @(
    "C:\Program Files\PostgreSQL\17\data\pg_hba.conf",
    "C:\Program Files\PostgreSQL\16\data\pg_hba.conf",
    "C:\Program Files\PostgreSQL\15\data\pg_hba.conf"
)
$pgHba = $null
foreach ($p in $Paths) {
    if (Test-Path $p) { $pgHba = $p; break }
}
if (-not $pgHba) {
    Write-Host "[X] pg_hba.conf not found" -ForegroundColor Red
    exit 1
}
Write-Step "Found: $pgHba"

# Check admin
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host ""
    Write-Host "[X] Must run as Administrator!" -ForegroundColor Red
    Write-Host ""
    Write-Host "  1. Right-click PowerShell" -ForegroundColor Yellow
    Write-Host "  2. Select 'Run as administrator'" -ForegroundColor Yellow
    Write-Host "  3. Re-run this script" -ForegroundColor Yellow
    Write-Host ""
    pause
    exit 1
}

# Backup
$backup = "$pgHba.bak.$(Get-Date -Format 'yyyyMMddHHmmss')"
Copy-Item $pgHba $backup -Force
Write-Ok "Backup: $backup"

# Read
$content = [System.IO.File]::ReadAllText($pgHba)
Write-Step "Current IPv4/IPv6 lines:"
$content -split "`r?`n" | Where-Object { $_ -match "127\.0\.0\.1/32|::1/128" } | ForEach-Object {
    Write-Host "  $_"
}

# Replace all auth methods to trust for IPv4 and IPv6 local
# Method: replace scram-sha-256 / md5 with trust ONLY in lines that have 127.0.0.1/32 or ::1/128
$newContent = $content

# IPv4
$newContent = $newContent -replace "(host\s+all\s+all\s+127\.0\.0\.1/32\s+)\S+", '$1trust'
# IPv6
$newContent = $newContent -replace "(host\s+all\s+all\s+::1/128\s+)\S+", '$1trust'
# Local (Unix socket) - if present
$newContent = $newContent -replace "(local\s+all\s+all\s+)(?:trust|scram-sha-256|md5|password|peer|ident|cert|reject)", '$1trust'

if ($newContent -eq $content) {
    Write-Warn "No changes (file may already be trust or has different format)"
} else {
    [System.IO.File]::WriteAllText($pgHba, $newContent, [System.Text.Encoding]::ASCII)
    Write-Ok "Updated pg_hba.conf"
}

# Show new content
Write-Host ""
Write-Step "New IPv4/IPv6 lines:"
$newContent -split "`r?`n" | Where-Object { $_ -match "127\.0\.0\.1/32|::1/128" } | ForEach-Object {
    Write-Host "  $_"
}

# Restart
Write-Step "Restarting PostgreSQL..."
$svc = Get-Service -Name "postgresql*" | Select-Object -First 1
if ($svc) {
    Restart-Service $svc.Name -Force
    Write-Ok "Service restarted: $($svc.Name)"
} else {
    Write-Warn "Service not found, restart manually"
}
Start-Sleep 3

# Find psql
$psql = $null
foreach ($p in @("C:\Program Files\PostgreSQL\17\bin", "C:\Program Files\PostgreSQL\16\bin", "C:\Program Files\PostgreSQL\15\bin")) {
    $full = Join-Path $p "psql.exe"
    if (Test-Path $full) {
        $env:Path = "$env:Path;$p"
        $psql = $full
        break
    }
}

# Test
if ($psql) {
    Write-Step "Testing connection..."
    $test = & $psql -U postgres -h localhost -p 5432 -tAc "SELECT 1;" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Ok "Connection works! No password needed."
    } else {
        Write-Host ""
        Write-Host "[X] Still cannot connect. Last error:" -ForegroundColor Red
        Write-Host "  $test" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Done! Now run quickstart.ps1" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "  .\scripts\quickstart.ps1" -ForegroundColor Cyan
Write-Host ""
pause
