@echo off
echo Church Display App Uninstaller
echo ==============================
echo.

REM Check if running as administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] This script requires administrator privileges.
    echo Please right-click and select "Run as administrator".
    pause
    exit /b 1
)

REM Remove desktop shortcut
echo Removing desktop shortcut...
if exist "%PUBLIC%\Desktop\Church Display App.lnk" del "%PUBLIC%\Desktop\Church Display App.lnk"

REM Remove Start Menu shortcuts
echo Removing Start Menu shortcuts...
if exist "%PROGRAMDATA%\Microsoft\Windows\Start Menu\Programs\Church Display App" (
    rmdir "%PROGRAMDATA%\Microsoft\Windows\Start Menu\Programs\Church Display App" /s /q
)

REM Remove application files
set INSTALL_DIR=%PROGRAMFILES%\ChurchDisplayApp
echo Removing application files from: %INSTALL_DIR%
if exist "%INSTALL_DIR%" (
    rmdir "%INSTALL_DIR%" /s /q
)

REM Remove registry entries
echo Removing registry entries...
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ChurchDisplayApp" /f >nul 2>&1

echo.
echo Uninstallation Complete!
echo.
echo Church Display App has been removed from your system.
echo.
pause
