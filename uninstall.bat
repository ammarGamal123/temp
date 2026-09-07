@echo off
NET SESSION >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] This script must be run as Administrator!
    pause
    exit /b 1
)

echo ========================================
echo   Uninstalling POS Print Agent Service
echo ========================================
echo.

cd /d "%~dp0"
POS.PrintAgent.Installer.exe uninstall

echo.
pause
