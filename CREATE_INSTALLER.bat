@echo off
setlocal enabledelayedexpansion

echo Creating Church Display App Installer
echo =====================================
echo.

REM Check if running as administrator
net session >nul 2>&1
if !errorLevel! neq 0 (
    echo This script requires administrator privileges.
    echo Please right-click this file and select "Run as administrator".
    echo.
    pause
    exit /b 1
)

echo Running with administrator privileges...
echo.

REM Set installation directory
set INSTALL_DIR=%PROGRAMFILES%\ChurchDisplayApp
echo Installation directory: %INSTALL_DIR%

REM Create installation directory
if not exist "%INSTALL_DIR%" (
    echo Creating installation directory...
    mkdir "%INSTALL_DIR%"
)

REM Check for published files
if not exist "bin\Publish\win-x64\ChurchDisplayApp.exe" (
    echo ERROR: Published files not found!
    echo Please run build-release.bat first.
    pause
    exit /b 1
)

REM Copy application files
echo Copying application files...
xcopy "bin\Publish\win-x64\*.*" "%INSTALL_DIR%\" /E /I /Y
if !errorLevel! neq 0 (
    echo ERROR: Failed to copy application files.
    pause
    exit /b 1
)

REM Create desktop shortcut
echo Creating desktop shortcut...
powershell -Command "$WshShell = New-Object -comObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut('%PUBLIC%\Desktop\Church Display App.lnk'); $Shortcut.TargetPath = '%INSTALL_DIR%\ChurchDisplayApp.exe'; $Shortcut.Save()"

REM Create Start Menu shortcut
echo Creating Start Menu shortcut...
if not exist "%PROGRAMDATA%\Microsoft\Windows\Start Menu\Programs\Church Display App" (
    mkdir "%PROGRAMDATA%\Microsoft\Windows\Start Menu\Programs\Church Display App"
)
powershell -Command "$WshShell = New-Object -comObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut('%PROGRAMDATA%\Microsoft\Windows\Start Menu\Programs\Church Display App\Church Display App.lnk'); $Shortcut.TargetPath = '%INSTALL_DIR%\ChurchDisplayApp.exe'; $Shortcut.Save()"

REM Add to Windows Registry for uninstall
echo Adding uninstall information...
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ChurchDisplayApp" /v DisplayName /t REG_SZ /d "Church Display App" /f
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ChurchDisplayApp" /v InstallLocation /t REG_SZ /d "%INSTALL_DIR%" /f
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ChurchDisplayApp" /v UninstallString /t REG_SZ /d "cmd.exe /c \"%INSTALL_DIR%\UNINSTALL.bat\"" /f

REM Copy the static uninstall script to the installation directory
echo Creating uninstall script...
copy "%~dp0UNINSTALL.bat" "%INSTALL_DIR%\UNINSTALL.bat" >nul
if !errorLevel! neq 0 (
    echo ERROR: Failed to copy uninstall script.
    pause
    exit /b 1
)

echo.
echo =====================================
echo Installation Complete Successfully!
echo =====================================
echo.
echo Church Display App has been installed to:
echo - %INSTALL_DIR%
echo - Desktop shortcut (for all users)
echo - Start Menu (for all users)
echo.
echo You can now run the app from:
echo - Desktop shortcut "Church Display App"
echo - Start Menu ^> Programs ^> Church Display App
echo.
echo To uninstall, use Add/Remove Programs or run: %INSTALL_DIR%\UNINSTALL.bat
echo.
pause
