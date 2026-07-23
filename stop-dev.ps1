# ERP-SYSTEM — Stop Dev Environment (Fast: ~1 sec)
# استعمل: .\stop-dev.ps1

Write-Host "=== إيقاف ERP-SYSTEM ===" -ForegroundColor Yellow

# الطريقة الأسرع: kill بـ TCP owner مباشرة (لا Get-CimInstance)
Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
Get-NetTCPConnection -LocalPort 3000 -State Listen -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }

# Fallback: kill by name (يغطي الـ children مثل dotnet/node)
Get-Process -Name "ERPSystem.Host","dotnet","node","cmd" -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowTitle -eq "" } |
    Stop-Process -Force -ErrorAction SilentlyContinue

# لا Start-Sleep ولا Test-NetConnection — العمليات ماتت فوراً

Write-Host "OK تم إيقاف كل العمليات على :5000 و :3000" -ForegroundColor Green
Write-Host ""
Write-Host "PostgreSQL بقي شغّالاً (للحفاظ على البيانات)." -ForegroundColor Cyan
Write-Host "لإيقافه: Stop-Service postgresql-x64-15"
