@echo off
:: Build script for VPS File Manager
:: Double-click to build the installer

echo ========================================
echo VPS File Manager - Build Script
echo ========================================
echo.

:: Execute PowerShell script
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-installer.ps1"

:: Keep window open to see results
echo.
echo Press any key to exit...
pause >nul
