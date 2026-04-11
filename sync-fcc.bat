@echo off
setlocal enabledelayedexpansion

echo ===================================================
echo Church Display App - FULL BUILD ^& SYNC
echo ===================================================

REM 1. Clean and Publish
echo Publishing ChurchDisplayApp...
dotnet publish -c Release --self-contained true --runtime win-x64 -p:PublishSingleFile=false -p:PublishReadyToRun=false -o bin\Publish\win-x64
if !errorLevel! neq 0 (
    echo [ERROR] Publish failed.
    exit /b 1
)

REM 2. Build Installer
set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if exist "!ISCC!" (
    echo Creating Windows Installer...
    "!ISCC!" ChurchDisplayApp.iss
    if !errorLevel! neq 0 (
        echo [ERROR] Installer creation failed.
        exit /b 1
    )
) else (
    echo [WARNING] Inno Setup 6 not found. Skipping installer build.
)

REM 3. Sync to D:\FCC Sync Folder
set "SYNC_DIR=D:\FCC Sync Folder"
if exist "!SYNC_DIR!" (
    echo Syncing to !SYNC_DIR!...
    
    REM Find the latest setup file in bin\Installer
    set "LATEST_SETUP="
    for /f "delims=" %%F in ('dir /b /o-d "bin\Installer\ChurchDisplayApp-*-Setup.exe" 2^>nul') do (
        if not defined LATEST_SETUP set "LATEST_SETUP=%%F"
    )
    
    if defined LATEST_SETUP (
        echo Copying Latest Installer: !LATEST_SETUP!...
        copy /Y "bin\Installer\!LATEST_SETUP!" "!SYNC_DIR!\ChurchDisplayApp-Setup.exe"
    ) else (
        echo [ERROR] No installer found in bin\Installer\
    )
    
    echo.
    echo Sync Complete Successfully!
) else (
    echo [ERROR] Sync directory !SYNC_DIR! not found.
)

echo.
echo Process Complete.
