# ERP-SYSTEM — Fast Backend Restart (for code changes)
# استعمل: .\restart-backend.ps1
#
# Fast: ~3 sec vs 15-30 sec (full stop+start).
# Use this when:
# - You changed a .cs file but didn't change Program.cs or schema
# - You want to apply code changes quickly
# - Frontend doesn't need restart (Next.js Fast Refresh handles it)
#
# For FULL restart (schema changes, Program.cs changes): use stop-dev.ps1 + start-dev.ps1

Write-Host "=== إعادة تشغيل Backend (سريع) ===" -ForegroundColor Yellow

# Kill backend processes only (keep frontend + postgres)
Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
Get-Process -Name "ERPSystem.Host" -ErrorAction SilentlyContinue | Stop-Process -Force

$ProjectRoot = $PSScriptRoot
$backendDir = Join-Path $ProjectRoot "src\backend\Host"

# Build incrementally (dotnet picks up only changed files)
Write-Host "  -> dotnet build (incremental)..." -ForegroundColor Yellow
Push-Location $backendDir
dotnet build 2>&1 | Select-String -Pattern "error|Build succeeded|Build FAILED" | Select-Object -First 2 | ForEach-Object { Write-Host "  $($_)" }
Pop-Location

# Restart
Write-Host "  -> تشغيل Backend..." -ForegroundColor Yellow
$env:ASPNETCORE_ENVIRONMENT = "Development"
Start-Process -FilePath "dotnet" -ArgumentList "run","--no-build","--urls=http://localhost:5000" `
    -WorkingDirectory $backendDir `
    -RedirectStandardOutput "$env:TEMP\backend.log" `
    -RedirectStandardError "$env:TEMP\backend.err.log" `
    -PassThru -NoNewWindow | Out-Null

# Quick health check (5 sec max)
$ready = $false
for ($i = 1; $i -le 10; $i++) {
    Start-Sleep -Seconds 1
    try {
        $r = Invoke-WebRequest -Uri "http://localhost:5000/health" -UseBasicParsing -TimeoutSec 2
        if ($r.StatusCode -eq 200) { $ready = $true; break }
    } catch {}
}

if ($ready) {
    Write-Host "  OK Backend شغّال على http://localhost:5000" -ForegroundColor Green
    Write-Host ""
    Write-Host "Frontend (port 3000) لم يتأثر — Next.js Fast Refresh يتعامل معه." -ForegroundColor Cyan
} else {
    Write-Host "  X Backend لم يبدأ بعد 10 ثواني. افحص $env:TEMP\backend.err.log" -ForegroundColor Red
}
