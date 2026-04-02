@echo off
setlocal enabledelayedexpansion

REM Build script for ChurchDisplayApp Windows Installer
echo ===================================================
echo Building Church Display App Release & Installer
echo ===================================================
echo.

REM 1. Clean previous builds
echo Cleaning previous builds...
if exist "bin\Publish" (
    echo Removing bin\Publish...
    rmdir /s /q "bin\Publish"
)
dotnet clean -c Release
echo.

REM 2. Build Release version
echo Building ChurchDisplayApp Release...
dotnet build -c Release
if !errorLevel! neq 0 (
    echo [ERROR] Build failed.
    pause
    exit /b 1
)
echo.

REM 3. Publish self-contained executable folder
echo Publishing self-contained executable for win-x64...
REM We use --self-contained true to include the .NET runtime
REM We use win-x64 to target 64-bit Windows
dotnet publish -c Release --self-contained true --runtime win-x64 -p:PublishSingleFile=false -p:PublishReadyToRun=false -o bin\Publish\win-x64
if !errorLevel! neq 0 (
    echo [ERROR] Publish failed.
    pause
    exit /b 1
)
echo.

REM 4. Check for Inno Setup 6
set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not exist "!ISCC!" (
    echo [WARNING] Inno Setup 6 not found at "!ISCC!".
    echo Skipping installer creation. 
    echo.
    echo The portable version is available at: bin\Publish\win-x64\
    echo.
    pause
    exit /b 0
)

REM 5. Create Windows Installer
echo Creating Windows Installer using Inno Setup...
"!ISCC!" ChurchDisplayApp.iss
if !errorLevel! neq 0 (
    echo [ERROR] Installer creation failed.
    pause
    exit /b 1
)

echo.
echo ===================================================
echo DONE!
echo ===================================================
echo.
echo Portable Version: bin\Publish\win-x64\
echo Installer Output: bin\Installer\
echo.
pause
