@echo off
title Stop Titan Fitness Gym Management Server
color 0C
echo ===================================================
echo     Stopping Titan Fitness Gym Management Server...
echo ===================================================

taskkill /F /IM GymManagement.Web.exe >nul 2>&1

echo Server has been stopped.
timeout /t 2 >nul
