# Church Display App - Windows Installer Setup Guide

## Quick Start for Creating Installer

### Step 1: Install Required Tools
- Download Inno Setup from: https://jrsoftware.org/isdl.php
- Install with default options

### Step 2: Build Release Executable
Run these commands in PowerShell from the project directory:

```powershell
cd c:\Projects\ChurchDisplayApp

# Build in Release mode
dotnet build -c Release

# Publish self-contained (includes all .NET runtime)
dotnet publish -c Release --self-contained --runtime win-x64
```

The executable will be at:
`bin\Release\net10.0-windows\win-x64\publish\ChurchDisplayApp.exe`

### Step 3: Create Windows Installer
1. Open Inno Setup
2. Open: `ChurchDisplayApp.iss` (in project root)
3. Click "Build" → "Compile"
4. Installer created at: `bin\Installer\ChurchDisplayApp-1.0.0-Setup.exe`

### Step 4: Distribute
Share the `.exe` installer file - it includes everything needed!

## What the Installer Provides

✅ **Self-Contained** - No dependencies to install
✅ **All Runtime Files** - .NET 10.0 runtime included  
✅ **Media Support** - MediaElement codecs included
✅ **System.Windows.Forms** - Multi-monitor detection support
✅ **Desktop Shortcut** - Optional shortcut creation
✅ **Start Menu** - Program added to Start Menu
✅ **Uninstaller** - Clean uninstall support

## Installer Features

- **No Admin Required** - Installs to user AppData
- **Single File** - One `.exe` to distribute
- **No .NET Check** - Works on any Windows PC
- **Multi-Monitor** - Automatic second-monitor fullscreen detection
- **Portable** - All files self-contained

## Customization (Optional)

Edit `ChurchDisplayApp.iss` to change:
- **AppName** - Product name
- **AppVersion** - Version number
- **AppPublisher** - Organization name
- **DefaultDirName** - Installation directory
- **OutputBaseFilename** - Installer filename

## File Size

- Debug build: ~200 MB
- Release (self-contained): ~450-500 MB
- Installer: ~250-300 MB (compressed)

The larger size is due to including full .NET 10.0 runtime for zero-dependency installation.

## Distribution

The generated `.exe` installer can be:
- Emailed directly
- Uploaded to cloud storage
- Burned to USB
- Run on any Windows 10/11 x64 PC
