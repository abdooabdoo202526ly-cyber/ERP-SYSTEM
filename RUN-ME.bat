@echo off
REM =============================================================================
REM ERP-SYSTEM - RUN-ME.BAT (Native mode, no Docker)
REM =============================================================================
REM Double-click هذا الملف لبدء النظام بدون Docker.
REM =============================================================================

title ERP-SYSTEM Launcher
color 0B

echo.
echo ============================================================
echo              ERP-SYSTEM (Native Mode - No Docker)
echo ============================================================
echo.

cd /d "%~dp0"

powershell -NoExit -ExecutionPolicy Bypass -File "%~dp0RUN-ME.ps1"
