# =============================================================================
# ERP-SYSTEM - RUN-ME.PS1 (Native mode, no Docker, no recursion)
# =============================================================================

[CmdletBinding()]
param()

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

function Write-Step($msg)  { Write-Host "[*] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "[+] $msg" -ForegroundColor Green }
function Write-Warn($msg)  { Write-Host "[!] $msg" -ForegroundColor Yellow }
function Write-Fail($msg)  { Write-Host "[X] $msg" -ForegroundColor Red }

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "        ERP-SYSTEM (Native Mode - No Docker)" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# Pre-flight checks
function Test-Tool($name, $command) {
    if (Get-Command $command -ErrorAction SilentlyContinue) {
        Write-Host "  [+] $name" -ForegroundColor Green
        return $true
    } else {
        Write-Host "  [X] $name NOT FOUND" -ForegroundColor Red
        return $false
    }
}

Write-Host "Pre-flight checks:" -ForegroundColor Cyan
$hasDotnet = Test-Tool ".NET 9 SDK" "dotnet"
$hasNode   = Test-Tool "Node.js 20+" "node"
$hasPsql   = Test-Tool "PostgreSQL psql" "psql"
Write-Host ""

if (-not $hasDotnet) {
    Write-Warn "Install .NET 9 from: https://dot.net"
}
if (-not $hasNode) {
    Write-Warn "Install Node.js 20+ from: https://nodejs.org"
}
if (-not $hasPsql) {
    Write-Warn "Install PostgreSQL 15+ from: https://www.postgresql.org/download/windows/"
    Write-Host "  Then add to PATH: `$env:Path += ';C:\Program Files\PostgreSQL\15\bin'" -ForegroundColor Yellow
}

if (-not $hasDotnet -or -not $hasNode) {
    Write-Host ""
    Write-Fail "Install missing tools and re-run."
}

Write-Step "Starting ERP-SYSTEM in Native mode..."
Write-Host ""
Write-Host "  Frontend:  http://localhost:3000" -ForegroundColor Cyan
Write-Host "  Backend:   http://localhost:5000" -ForegroundColor Cyan
Write-Host "  Swagger:   http://localhost:5000/swagger" -ForegroundColor Cyan
Write-Host "  Login:     admin@alfajr.local / Demo1234" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press Ctrl+C to stop." -ForegroundColor Yellow
Write-Host ""

# Directly call start-native.ps1 (no recursion, no Docker)
& "$ScriptDir\scripts\start-native.ps1"
