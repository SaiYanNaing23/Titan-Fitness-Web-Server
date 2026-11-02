@echo off
setlocal
title Titan Fitness Member Mobile App (Expo)
color 0B
echo ===================================================
echo     Starting Titan Fitness Mobile App (Expo)...
echo ===================================================

pushd "%~dp0GymManagement.Mobile" || exit /b 1

if not exist "node_modules" (
    echo [INFO] Installing required dependencies...
    call npm install --legacy-peer-deps
)

echo [INFO] Starting Expo Development Server...
call npx expo start -c

popd
pause
