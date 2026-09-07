@echo off
REM Run as Administrator!
NET SESSION >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] This script must be run as Administrator!
    echo Right-click and select "Run as administrator"
    pause
    exit /b 1
)

echo ========================================
echo   Installing POS Print Agent Service
echo ========================================
echo.

cd /d "%~dp0"

POS.PrintAgent.Installer.exe install "%~dp0POS.PrintAgent.Service.exe"

echo.
pause
