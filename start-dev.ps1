# ERP-SYSTEM — Start Dev Environment (Optimized v3 — uses Start-Process for detached processes)
# استعمل: .\start-dev.ps1
#
# Optimizations vs v1:
# - Parallel backend + frontend startup (saves 5-10s)
# - Detached processes (Start-Process -PassThru) — survive script exit
# - Reduced polling timeout (30s → 15s, with success-break)
# - Short Redis timeouts in Program.cs (ConnectTimeout=1s, SyncTimeout=500ms)
# - Removed unnecessary Start-Sleep calls

$ErrorActionPreference = 'Continue'
$ProjectRoot = $PSScriptRoot

Write-Host "=== ERP-SYSTEM Dev Launcher ===" -ForegroundColor Cyan
Write-Host ""

# --- 1. PostgreSQL ---
Write-Host "[1/4] التحقق من PostgreSQL..." -ForegroundColor Yellow
$pgService = Get-Service postgresql-x64-15 -ErrorAction SilentlyContinue
if ($null -eq $pgService) {
    Write-Host "  X خدمة PostgreSQL غير موجودة!" -ForegroundColor Red
    exit 1
}
if ($pgService.Status -ne 'Running') {
    Start-Service postgresql-x64-15
}
Write-Host "  OK PostgreSQL شغّال على :5432" -ForegroundColor Green

# --- 2. Cleanup (fast: <1 sec) ---
Write-Host "[2/4] تنظيف العمليات القديمة..." -ForegroundColor Yellow
Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
Get-NetTCPConnection -LocalPort 3000 -State Listen -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
Write-Host "  OK تم تنظيف المنافذ 5000 و 3000" -ForegroundColor Green

# --- 3. Backend + Frontend (parallel, detached) ---
Write-Host "[3/4] تشغيل Backend + Frontend (موازي + مفصول)..." -ForegroundColor Yellow
$env:ASPNETCORE_ENVIRONMENT = "Development"
$backendDir = Join-Path $ProjectRoot "src\backend\Host"
$frontendDir = Join-Path $ProjectRoot "src\frontend"

# Make sure .env.local exists
if (-not (Test-Path (Join-Path $frontendDir ".env.local"))) {
    "NEXT_PUBLIC_API_URL=http://localhost:5000" | Set-Content -Path (Join-Path $frontendDir ".env.local") -Encoding UTF8
}

# Backend: build once if needed
$backendDll = Join-Path $backendDir "bin\Debug\net9.0\ERPSystem.Host.dll"
if (-not (Test-Path $backendDll)) {
    Write-Host "  -> أول تشغيل: dotnet build..." -ForegroundColor Yellow
    Push-Location $backendDir
    dotnet build --nologo -v quiet 2>&1 | Out-Null
    Pop-Location
}

# Backend: detached process (survives script exit)
$backendLog = "$env:TEMP\erp-backend.log"
$backendErr = "$env:TEMP\erp-backend.err.log"
$backendProcess = Start-Process -FilePath "dotnet" `
    -ArgumentList "run","--no-build","--urls=http://localhost:5000" `
    -WorkingDirectory $backendDir `
    -RedirectStandardOutput $backendLog `
    -RedirectStandardError $backendErr `
    -PassThru -NoNewWindow

# Frontend: detached process
$frontendLog = "$env:TEMP\erp-frontend.log"
$frontendProcess = Start-Process -FilePath "npm.cmd" `
    -ArgumentList "run","dev" `
    -WorkingDirectory $frontendDir `
    -RedirectStandardOutput $frontendLog `
    -RedirectStandardError "$env:TEMP\erp-frontend.err.log" `
    -PassThru -NoNewWindow

Write-Host "  -> تم تشغيل العمليات: Backend PID=$($backendProcess.Id) Frontend PID=$($frontendProcess.Id)" -ForegroundColor Cyan

# --- 4. Health checks (parallel, shorter polling) ---
Write-Host "  -> انتظار بدء Backend + Frontend..." -ForegroundColor Yellow
$backendReady = $false
$frontendReady = $false
for ($i = 1; $i -le 15; $i++) {
    Start-Sleep -Seconds 1
    if (-not $backendReady) {
        if ($backendProcess.HasExited) {
            Write-Host "  X Backend مات بعد $i ثانية. افحص $backendErr" -ForegroundColor Red
        } else {
            try {
                $r = Invoke-WebRequest -Uri "http://localhost:5000/health" -UseBasicParsing -TimeoutSec 2
                if ($r.StatusCode -eq 200) { $backendReady = $true }
            } catch {}
        }
    }
    if (-not $frontendReady) {
        if ($frontendProcess.HasExited) {
            Write-Host "  X Frontend مات بعد $i ثانية. افحص $env:TEMP\erp-frontend.err.log" -ForegroundColor Red
        } else {
            try {
                $r = Invoke-WebRequest -Uri "http://localhost:3000" -UseBasicParsing -TimeoutSec 2
                if ($r.StatusCode -eq 200) { $frontendReady = $true }
            } catch {}
        }
    }
    if ($backendReady -and $frontendReady) { break }
}

if ($backendReady) {
    Write-Host "  OK Backend شغّال على http://localhost:5000" -ForegroundColor Green
} else {
    Write-Host "  ! Backend لم يبدأ بعد 15 ثانية. افحص $backendErr" -ForegroundColor Yellow
}
if ($frontendReady) {
    Write-Host "  OK Frontend شغّال على http://localhost:3000" -ForegroundColor Green
} else {
    Write-Host "  ! Frontend لم يبدأ بعد 15 ثانية. افحص $env:TEMP\erp-frontend.err.log" -ForegroundColor Yellow
}

# --- ملخص ---
Write-Host ""
Write-Host "=== النظام جاهز ===" -ForegroundColor Green
Write-Host ""
Write-Host "الروابط:" -ForegroundColor Cyan
Write-Host "  Frontend:  http://localhost:3000"
Write-Host "  Swagger:   http://localhost:5000/swagger"
Write-Host "  Health:    http://localhost:5000/health/ready"
Write-Host ""
Write-Host "بيانات الدخول الجاهزة:" -ForegroundColor Cyan
Write-Host "  Email:     anas@demo.local"
Write-Host "  Password:  Demo1234"
Write-Host "  Tenant:    DemoCompany"
Write-Host ""
Write-Host "للتعديل بدون restart: استخدم .\restart-backend.ps1 (سريع)" -ForegroundColor Yellow
Write-Host "لإيقاف النظام: .\stop-dev.ps1" -ForegroundColor Yellow
Write-Host "Logs: $env:TEMP\erp-{backend,frontend}.log" -ForegroundColor DarkGray
