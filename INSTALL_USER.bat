@echo off
setlocal enabledelayedexpansion

echo Church Display App Installer (User Version)
echo ==========================================
echo.
echo This version installs the app to your Documents folder 
echo and does not require administrator privileges.
echo.

REM Set installation directory in user's Documents
set "INSTALL_DIR=%USERPROFILE%\Documents\ChurchDisplayApp"
echo Target installation directory: %INSTALL_DIR%

REM Create installation directory
if not exist "%INSTALL_DIR%" (
    echo Creating installation directory...
    mkdir "%INSTALL_DIR%"
    if !errorLevel! neq 0 (
        echo [ERROR] Failed to create installation directory.
        pause
        exit /b 1
    )
)

REM Check for published files
if not exist "bin\Publish\win-x64\ChurchDisplayApp.exe" (
    echo [ERROR] Published files not found. Please run build-release.bat first.
    pause
    exit /b 1
)

REM Copy published output using xcopy to ensure subdirectories and all assets are included
echo Copying application files and dependencies...
echo.

REM /E copies directories and subdirectories, including empty ones.
REM /I If destination does not exist and copying more than one file, assumes that destination must be a directory.
REM /Y Suppresses prompting to confirm you want to overwrite an existing destination file.
xcopy "bin\Publish\win-x64\*" "%INSTALL_DIR%\" /E /I /Y
if !errorLevel! gtr 4 (
    echo [ERROR] Failed to copy files. Error code: !errorLevel!
    pause
    exit /b 1
)

REM Create desktop shortcut using PowerShell
echo Creating desktop shortcut...
powershell -NoProfile -Command "$WshShell = New-Object -comObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut('%USERPROFILE%\Desktop\Church Display App.lnk'); $Shortcut.TargetPath = '%INSTALL_DIR%\ChurchDisplayApp.exe'; $Shortcut.WorkingDirectory = '%INSTALL_DIR%'; $Shortcut.Save()"

REM Create Start Menu shortcut
echo Creating Start Menu shortcut...
set "START_MENU_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Church Display App"
if not exist "%START_MENU_DIR%" (
    mkdir "%START_MENU_DIR%"
)
powershell -NoProfile -Command "$WshShell = New-Object -comObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut('%START_MENU_DIR%\Church Display App.lnk'); $Shortcut.TargetPath = '%INSTALL_DIR%\ChurchDisplayApp.exe'; $Shortcut.WorkingDirectory = '%INSTALL_DIR%'; $Shortcut.Save()"

echo.
echo ===========================================
echo Installation Complete Successfully!
echo ===========================================
echo.
echo You can now run the app from the desktop shortcut!
echo.
pause
