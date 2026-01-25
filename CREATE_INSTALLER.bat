@echo off
echo Creating Church Display App Installer
echo =====================================
echo.

REM Check if running as administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
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

REM Copy the main executable
echo Copying Church Display App executable...
copy "ChurchDisplayApp.exe" "%INSTALL_DIR%\" /Y
if %errorLevel% neq 0 (
    echo ERROR: Failed to copy executable file.
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
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ChurchDisplayApp" /v UninstallString /t REG_SZ /d "\"%INSTALL_DIR%\UNINSTALL.bat\"" /f

REM Create uninstall script
echo Creating uninstall script...
echo @echo off > "%INSTALL_DIR%\UNINSTALL.bat"
echo echo Church Display App Uninstaller >> "%INSTALL_DIR%\UNINSTALL.bat"
echo echo ============================== >> "%INSTALL_DIR%\UNINSTALL.bat"
echo echo. >> "%INSTALL_DIR%\UNINSTALL.bat"
echo echo Removing desktop shortcut... >> "%INSTALL_DIR%\UNINSTALL.bat"
echo del "%PUBLIC%\Desktop\Church Display App.lnk" /f >> "%INSTALL_DIR%\UNINSTALL.bat"
echo echo. >> "%INSTALL_DIR%\UNINSTALL.bat"
echo echo Removing Start Menu shortcuts... >> "%INSTALL_DIR%\UNINSTALL.bat"
echo rmdir "%PROGRAMDATA%\Microsoft\Windows\Start Menu\Programs\Church Display App" /s /q >> "%INSTALL_DIR%\UNINSTALL.bat"
echo echo. >> "%INSTALL_DIR%\UNINSTALL.bat"
echo echo Removing application files... >> "%INSTALL_DIR%\UNINSTALL.bat"
echo cd /d %%PROGRAMFILES%% >> "%INSTALL_DIR%\UNINSTALL.bat"
echo rmdir "ChurchDisplayApp" /s /q >> "%INSTALL_DIR%\UNINSTALL.bat"
echo echo. >> "%INSTALL_DIR%\UNINSTALL.bat"
echo echo Removing registry entries... >> "%INSTALL_DIR%\UNINSTALL.bat"
echo reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\ChurchDisplayApp" /f >> "%INSTALL_DIR%\UNINSTALL.bat"
echo echo. >> "%INSTALL_DIR%\UNINSTALL.bat"
echo echo Uninstallation Complete! >> "%INSTALL_DIR%\UNINSTALL.bat"
echo pause >> "%INSTALL_DIR%\UNINSTALL.bat"

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
echo To uninstall, run: %INSTALL_DIR%\UNINSTALL.bat
echo.
pause
