@echo off
setlocal
title Titan Fitness Gym Management Server
color 0A
echo ===================================================
echo     Starting Titan Fitness Gym Management Server...
echo ===================================================
echo.
echo On this PC:     http://localhost:5254
echo On your phone:  http://YOUR-PC-WIFI-IP:5254/
echo API for app:    http://YOUR-PC-WIFI-IP:5254/api/v1/mobile
echo Find your PC's Wi-Fi IPv4 address with: ipconfig
echo.
echo * Do not close this window while using the Gym Management system.
echo ===================================================

set "APP_DIR=%~dp0GymManagement.Web\publish"
if not exist "%APP_DIR%\GymManagement.Web.exe" (
    echo [ERROR] Published server not found: "%APP_DIR%\GymManagement.Web.exe"
    pause
    exit /b 1
)

set "DOTNET_READY=1"
dotnet --list-runtimes 2>nul | findstr /B /C:"Microsoft.NETCore.App 8." >nul || set "DOTNET_READY=0"
dotnet --list-runtimes 2>nul | findstr /B /C:"Microsoft.AspNetCore.App 8." >nul || set "DOTNET_READY=0"
if "%DOTNET_READY%"=="0" (
    echo.
    echo [SETUP NEEDED] Install the Windows x64 .NET 8 SDK once:
    echo https://dotnet.microsoft.com/en-us/download/dotnet/8.0
    echo Then double-click START_GYM_SERVER.bat again.
    pause
    exit /b 1
)

pushd "%APP_DIR%" || exit /b 1

set "Logging__EventLog__LogLevel__Default=None"
GymManagement.Web.exe --urls "http://0.0.0.0:5254"
set "EXIT_CODE=%ERRORLEVEL%"
popd
echo.
echo Server stopped (exit code %EXIT_CODE%).
pause
exit /b %EXIT_CODE%
