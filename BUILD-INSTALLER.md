# Church Display App - Building Windows Installer

## Prerequisites

1. **Inno Setup** - Download and install from: https://jrsoftware.org/isdl.php
   - Choose the full installer (includes IDE)

2. **.NET 10.0 SDK** - Already included in development environment

## Build Steps

### Option 1: Automatic Build Script
```bash
cd c:\Projects\ChurchDisplayApp
build-release.bat
```

### Option 2: Manual Build
```bash
# Build Release version
dotnet build -c Release

# Publish self-contained executable
dotnet publish -c Release -r win-x64 --self-contained true
```

## Create Windows Installer

1. **Open Inno Setup**
   - File → Open
   - Navigate to: `c:\Projects\ChurchDisplayApp\ChurchDisplayApp.iss`

2. **Build Installer**
   - Build → Compile
   - Output file: `bin\Installer\ChurchDisplayApp-1.0.0-Setup.exe`

3. **Test Installer**
   - Run the generated `.exe` file
   - Should install to `Program Files\ChurchDisplayApp\`

## Delivery

The final installer (`ChurchDisplayApp-1.0.0-Setup.exe`) is:
- **Self-contained** - No .NET required on target PC
- **Portable** - All dependencies included
- **Standalone** - Works on any Windows 10/11 x64 PC
- **All users install** - Installs to Program Files (requires admin)

## What's Included

- ChurchDisplayApp.exe (executable)
- All .NET runtime libraries
- System.Windows.Forms support
- MediaElement codecs for video/audio playback

## Notes

- First run may take a moment as .NET initializes
- Multiple monitor detection works automatically
- Requires Windows 10 or later (x64)
