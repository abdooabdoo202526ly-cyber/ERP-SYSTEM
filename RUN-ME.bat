@echo off
REM =============================================================================
REM ERP-SYSTEM - RUN-ME.BAT (Double-click to start!)
REM =============================================================================
REM هذا الملف يفتح PowerShell ويشغّل النظام بدون Docker.
REM فقط double-click على هذا الملف من File Explorer.
REM =============================================================================

title ERP-SYSTEM Launcher
color 0B

echo.
echo ============================================================
echo              ERP-SYSTEM (Native Mode - No Docker)
echo ============================================================
echo.

REM Check if .NET is installed
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [X] .NET 9 SDK is NOT installed.
    echo     Download: https://dot.net
    echo.
    pause
    exit /b 1
)

REM Check if Node is installed
where node >nul 2>&1
if errorlevel 1 (
    echo [X] Node.js 20+ is NOT installed.
    echo     Download: https://nodejs.org
    echo.
    pause
    exit /b 1
)

REM Check if PostgreSQL is installed
where psql >nul 2>&1
if errorlevel 1 (
    echo [!] PostgreSQL psql not in PATH.
    echo     Add to PATH: C:\Program Files\PostgreSQL\15\bin
    echo     Or install from: https://www.postgresql.org/download/windows/
    echo.
    pause
)

echo [+] Pre-flight checks done.
echo [*] Starting ERP-SYSTEM in Native mode (no Docker)...
echo.
echo     Frontend:  http://localhost:3000
echo     Backend:   http://localhost:5000
echo     Login:     admin@alfajr.local / Demo1234
echo.
echo Press Ctrl+C to stop. Logs in .backend.log and .frontend.log
echo.

REM Run the start script in PowerShell
powershell -NoExit -ExecutionPolicy Bypass -File "%~dp0scripts\start.ps1" -ForceNative
