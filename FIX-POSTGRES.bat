@echo off
REM =============================================================================
REM ERP-SYSTEM - FIX EVERYTHING (Double-click as Administrator)
REM =============================================================================
REM هذا الملف يعمل كل شيء تلقائياً:
REM   1. يفتح PowerShell كـ Administrator
REM   2. يصلح pg_hba.conf (ASCII, no BOM, all 'trust')
REM   3. يعيد تشغيل PostgreSQL
REM   4. ينشئ user + databases
REM   5. يشغّل النظام
REM =============================================================================

title ERP-SYSTEM - Fix Everything
color 0E

echo.
echo ========================================================================
echo   ERP-SYSTEM - PostgreSQL Fix ^(will run as Administrator^)
echo ========================================================================
echo.
echo   This will:
echo     1. Edit pg_hba.conf to 'trust' mode
echo     2. Restart PostgreSQL service
echo     3. Create erp_user with password 'erp_password'
echo     4. Create erp_system + erp_events databases
echo.
echo   You may see a UAC prompt. Click "Yes".
echo.
pause

cd /d "%~dp0"

REM Launch PowerShell as Administrator
powershell.exe -ExecutionPolicy Bypass -NoExit -Command "& { Start-Process powershell.exe -Verb RunAs -ArgumentList '-NoExit', '-ExecutionPolicy', 'Bypass', '-File', '%~dp0scripts\ultimate-fix.ps1' }"

echo.
echo Administrator PowerShell opened. Follow instructions there.
echo.
pause
