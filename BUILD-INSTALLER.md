# ChurchDisplayApp - Build & Install Guide

> This document is written as step-by-step instructions for an AI code agent
> to build and install the application. All commands assume a Windows host
> with PowerShell or cmd, and .NET 10.0 SDK installed.

---

## Prerequisites

Before starting, verify the following tools are available:

| Tool | Check Command | Required For |
|------|--------------|-------------|
| .NET 10.0 SDK | `dotnet --version` (should show 10.x) | All builds |
| Inno Setup 6 (optional) | `where iscc` or check `C:\Program Files (x86)\Inno Setup 6\ISCC.exe` | Windows Installer only |

If .NET 10.0 SDK is not installed, download it from https://dotnet.microsoft.com/download/dotnet/10.0

---

## Option A: Run Directly from Build (No Installer)

This is the fastest path. Build the app and run the executable directly
from the publish folder. No admin privileges or Inno Setup required.

### Step 1: Navigate to the project directory

```cmd
cd C:\Projects\ChurchDisplayApp
```

The project root must contain `ChurchDisplayApp.csproj`.

### Step 2: Publish the application

```cmd
dotnet publish -c Release -r win-x64 --self-contained true
```

This produces a self-contained build (includes the .NET runtime) at:

```
bin\Release\net10.0-windows\win-x64\publish\
```

Key files in the publish folder:
- `ChurchDisplayApp.exe` — the application
- `RemoteControl\index.html` — web remote control UI (auto-copied by csproj)
- `Sounds\SalC5Light2.sf2` — amen resolve soundfont (auto-copied by csproj)
- All .NET runtime DLLs (included because `--self-contained true`)

### Step 3: Run the application

```cmd
cd bin\Release\net10.0-windows\win-x64\publish
ChurchDisplayApp.exe
```

Or simply double-click `ChurchDisplayApp.exe` in File Explorer.

### Notes for Direct Run

- **No admin required** to run the app itself.
- **Run as Administrator** is recommended on first launch so the app can
  automatically add a Windows Firewall rule for the remote control feature.
- Settings are stored in `%APPDATA%\ChurchDisplayApp\settings.json`.
- Logs are stored in `%APPDATA%\ChurchDisplayApp\logs\`.
- The publish folder can be copied to any Windows 10/11 x64 machine and
  run directly — it is fully portable.

---

## Option B: Create a Full Windows Installer

This produces a professional `.exe` installer with desktop/start menu
shortcuts, a proper uninstaller entry, and compressed distribution.

### Step 1: Install Inno Setup 6

Download the installer from https://jrsoftware.org/isdl.php and install
with default options. This adds `ISCC.exe` (the Inno Setup compiler) to
`C:\Program Files (x86)\Inno Setup 6\`.

### Step 2: Publish the application

```cmd
cd C:\Projects\ChurchDisplayApp
dotnet publish -c Release -r win-x64 --self-contained true -o bin\Publish\win-x64
```

The `-o bin\Publish\win-x64` flag outputs directly to the folder that
the Inno Setup script (`ChurchDisplayApp.iss`) expects.

**Important**: The `ChurchDisplayApp.iss` file references the source path
`bin\Publish\win-x64\*`. If you publish to a different output folder,
the installer script will need to be updated to match.

### Step 3: Build the installer with Inno Setup

Run this from the project root (requires cmd, not PowerShell, for the
batch script path detection):

```cmd
build-release.bat
```

This script does the following automatically:
1. Cleans previous builds (`dotnet clean`)
2. Builds in Release mode (`dotnet build -c Release`)
3. Publishes self-contained to `bin\Publish\win-x64`
4. Runs `ISCC.exe` to compile `ChurchDisplayApp.iss` into an installer
5. Outputs the installer to `bin\Installer\ChurchDisplayApp-1.0.0-Setup.exe`

If `build-release.bat` cannot find Inno Setup at the expected path, run
the compiler manually:

```cmd
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" ChurchDisplayApp.iss
```

### Step 4: Test the installer

Run the generated installer:

```
bin\Installer\ChurchDisplayApp-1.0.0-Setup.exe
```

The installer will:
- Install to `C:\Program Files\ChurchDisplayApp\`
- Create a Start Menu entry under "Church Display App"
- Optionally create a Desktop shortcut
- Add an uninstaller entry in Windows "Add or Remove Programs"

### Alternative: Full Build + Sync Script

The `sync-fcc.bat` script does everything `build-release.bat` does, plus
copies the finished installer to a sync folder for distribution:

```cmd
sync-fcc.bat
```

This copies to `D:\FCC Sync Folder\ChurchDisplayApp-Setup.exe`. Edit the
`SYNC_DIR` variable in the script to change the destination.

---

## Inno Setup Script Reference

There are two `.iss` files in the project. Use `ChurchDisplayApp.iss` (the
primary one):

| File | Purpose |
|------|---------|
| `ChurchDisplayApp.iss` | Primary installer. Sources from `bin\Publish\win-x64\`. Outputs to `bin\Installer\`. Includes license, admin requirement, x64 enforcement. |
| `installer.iss` | Secondary/simpler installer. Sources from `bin\Release\net10.0-windows\win-x64\publish\`. Outputs to current directory. |

To customize the installer, edit the `[Setup]` section of `ChurchDisplayApp.iss`:

```ini
AppVersion=1.0.0          ; Bump this for each release
DefaultDirName={pf}\ChurchDisplayApp
OutputBaseFilename=ChurchDisplayApp-1.0.0-Setup
```

---

## Quick Reference: Build Commands

| Goal | Command |
|------|---------|
| Debug build | `dotnet build` |
| Release build | `dotnet build -c Release` |
| Publish (portable, no installer) | `dotnet publish -c Release -r win-x64 --self-contained true` |
| Publish + Inno installer | `build-release.bat` |
| Publish + installer + sync | `sync-fcc.bat` |
| Single-file EXE (no folder) | `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true` |

---

## File Size Estimates

| Build Type | Size |
|-----------|------|
| Debug build folder | ~200 MB |
| Release self-contained publish | ~450-500 MB |
| Inno installer (LZMA compressed) | ~250-300 MB |
| Single-file publish | ~150 MB |

The large size is due to bundling the full .NET runtime for zero-dependency
operation on the target machine.

---

## Troubleshooting

| Problem | Solution |
|---------|---------|
| `dotnet` command not found | Install .NET 10.0 SDK from https://dotnet.microsoft.com |
| ISCC not found by build script | Verify Inno Setup 6 is installed at `C:\Program Files (x86)\Inno Setup 6\` |
| Port 80 fails at runtime | Run the app as Administrator, or the app will fall back to port 8088 automatically |
| Remote control not reachable | Run app as Administrator on first launch to add firewall rule |
| Installer sources not found | Ensure you published with `-o bin\Publish\win-x64` before running ISCC |
| VLC media not playing | Verify VLC redistributable files are in the publish folder |
