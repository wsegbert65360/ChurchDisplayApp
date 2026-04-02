@echo off
echo Church Display App Uninstaller
echo ==============================
echo.

REM Remove desktop shortcut
echo Removing desktop shortcut...
if exist "%USERPROFILE%\Desktop\Church Display App.lnk" del "%USERPROFILE%\Desktop\Church Display App.lnk"

REM Remove Start Menu shortcuts
echo Removing Start Menu shortcuts...
if exist "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Church Display App\Church Display App.lnk" del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Church Display App\Church Display App.lnk"
if exist "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Church Display App" rmdir "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Church Display App"

REM Remove application files
set INSTALL_DIR=%PROGRAMFILES%\ChurchDisplayApp
echo Removing application files from: %INSTALL_DIR%
if exist "%INSTALL_DIR%\ChurchDisplayApp.exe" del "%INSTALL_DIR%\ChurchDisplayApp.exe"
if exist "%INSTALL_DIR%" rmdir "%INSTALL_DIR%"

echo.
echo Uninstallation Complete!
echo.
echo Church Display App has been removed from your system.
echo.
pause
