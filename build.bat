@echo off
echo ========================================
echo   POS Print Agent - Build Script
echo ========================================
echo.

REM Clean previous build
if exist "publish" rmdir /s /q "publish"
mkdir publish

echo [INFO] Publishing POS.PrintAgent.Service...
dotnet publish POS.PrintAgent.Service -c Release -r win-x64 --self-contained true -o ./publish ^
    -p:PublishSingleFile=false ^
    -p:PublishReadyToRun=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Service build failed!
    pause
    exit /b 1
)

echo [INFO] Copying Templates to publish folder...
if not exist "publish\Templates" mkdir "publish\Templates"
xcopy /Y /E "POS.PrintAgent.Service\Templates\*" "publish\Templates\"

echo.
echo [INFO] Publishing POS.PrintAgent.Installer...
dotnet publish POS.PrintAgent.Installer -c Release -r win-x64 --self-contained true -o ./publish ^
    -p:PublishSingleFile=false

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Installer build failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo   Build completed successfully!
echo ========================================
echo.
echo Output folder: .\publish
echo.
echo Next steps:
echo   1. Copy the 'publish' folder to the target machine
echo   2. Open CMD as Administrator in that folder
echo   3. Run: POS.PrintAgent.Installer.exe install
echo.
pause
