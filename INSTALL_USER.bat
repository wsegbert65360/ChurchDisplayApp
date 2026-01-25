@echo off
echo Church Display App Installer (User Version)
echo ==========================================
echo.

REM Create installation directory in user's Documents
set INSTALL_DIR=%USERPROFILE%\Documents\ChurchDisplayApp
echo Creating installation directory: %INSTALL_DIR%
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"

REM Copy the application
echo Copying Church Display App...
copy "ChurchDisplayApp.exe" "%INSTALL_DIR%\" /Y

REM Create desktop shortcut
echo Creating desktop shortcut...
powershell "$WshShell = New-Object -comObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut('%USERPROFILE%\Desktop\Church Display App.lnk'); $Shortcut.TargetPath = '%INSTALL_DIR%\ChurchDisplayApp.exe'; $Shortcut.Save()"

REM Create Start Menu shortcut
echo Creating Start Menu shortcut...
if not exist "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Church Display App" mkdir "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Church Display App"
powershell "$WshShell = New-Object -comObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut('%APPDATA%\Microsoft\Windows\Start Menu\Programs\Church Display App\Church Display App.lnk'); $Shortcut.TargetPath = '%INSTALL_DIR%\ChurchDisplayApp.exe'; $Shortcut.Save()"

echo.
echo Installation Complete!
echo.
echo Church Display App has been installed to:
echo - Desktop shortcut
echo - Start Menu > Programs > Church Display App  
echo - Installation folder: %INSTALL_DIR%
echo.
echo You can now run the app from the desktop shortcut!
echo.
pause
