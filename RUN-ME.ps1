# =============================================================================
# ERP-SYSTEM - RUN-ME.PS1 (Native mode, no Docker)
# =============================================================================
# Click في File Explorer: "Run with PowerShell"
# أو من PowerShell: .\RUN-ME.ps1
# =============================================================================

[CmdletBinding()]
param()

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "        ERP-SYSTEM (Native Mode - No Docker required)" -ForegroundColor Cyan
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
$hasDotnet = Test-Tool ".NET 9 SDK    (dotnet --version)" "dotnet"
$hasNode   = Test-Tool "Node.js 20+   (node --version)" "node"
$hasPsql   = Test-Tool "PostgreSQL psql (psql --version)" "psql"
Write-Host ""

if (-not $hasDotnet) {
    Write-Host "Install .NET 9 from: https://dot.net" -ForegroundColor Yellow
}
if (-not $hasNode) {
    Write-Host "Install Node.js 20+ from: https://nodejs.org" -ForegroundColor Yellow
}
if (-not $hasPsql) {
    Write-Host "Install PostgreSQL 15+ from: https://www.postgresql.org/download/windows/" -ForegroundColor Yellow
    Write-Host "Then add psql to PATH: `$env:Path += ';C:\Program Files\PostgreSQL\15\bin'" -ForegroundColor Yellow
}

if (-not $hasDotnet -or -not $hasNode) {
    Write-Host ""
    Write-Host "Install missing tools and re-run." -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "[*] Starting ERP-SYSTEM..." -ForegroundColor Cyan
Write-Host ""
Write-Host "  Frontend:  http://localhost:3000" -ForegroundColor Cyan
Write-Host "  Backend:   http://localhost:5000" -ForegroundColor Cyan
Write-Host "  Swagger:   http://localhost:5000/swagger" -ForegroundColor Cyan
Write-Host "  Login:     admin@alfajr.local / Demo1234" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press Ctrl+C to stop." -ForegroundColor Yellow
Write-Host ""

# Run the start script in Native mode
& "$ScriptDir\scripts\start.ps1" -ForceNative
