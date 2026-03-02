# ChurchDisplayApp - Documentation

This document provides comprehensive information on installing, setting up, and building the ChurchDisplayApp.

---

## 🚀 Installation Instructions

### Quick Install (Portable Version)
1. **Download** the `ChurchDisplayApp` package.
2. **Extract** all files to a folder on your computer (e.g., `C:\ChurchDisplayApp`).
3. **Run** `ChurchDisplayApp.exe`.

### Automated Installation (Recommended)
1. **Extract** the installation package ZIP.
2. **Right-click** on `INSTALL.bat` and select **"Run as administrator"**.
3. Follow the on-screen prompts.
4. The application will be installed to `C:\Program Files\ChurchDisplayApp\`, and shortcuts will be created on your Desktop and Start Menu.

### System Requirements
- **OS**: Windows 10 or Windows 11 (64-bit).
- **Runtime**: .NET 8.0/10.0 Runtime (included in self-contained builds).
- **Dependencies**: Visual C++ 2015-2022 Redistributable (x64) - *Required for media playback*.
- **Hardware**: Graphics card with DirectX support, second monitor or projector recommended.

---

## 🛠️ Building the Application

### Prerequisites
1. **.NET 10.0 SDK** (or version specified in project).
2. **Inno Setup** - Required for creating the Windows Installer (`.exe`). Download from [jrsoftware.org](https://jrsoftware.org/isdl.php).

### Build Steps
You can build the application using the provided scripts or manually via CLI.

#### Using Build Scripts
Run `build-release.bat` from the project root. This script handles the build and publish process.

#### Manual Build (CLI)
```powershell
# Build in Release mode
dotnet build -c Release

# Publish self-contained executable (includes all .NET runtime)
dotnet publish -c Release -r win-x64 --self-contained true
```
The published files will be located at: `bin\Release\net10.0-windows\win-x64\publish\`

---

## 📦 Creating the Windows Installer

ChurchDisplayApp uses Inno Setup to create a professional single-file installer.

1. **Open Inno Setup**.
2. **Open** the script file: `ChurchDisplayApp.iss` located in the project root.
3. Click **"Build" → "Compile"**.
4. The installer will be created at: `bin\Installer\ChurchDisplayApp-1.0.0-Setup.exe`.

### Installer Features
- **Self-Contained**: Includes all necessary .NET runtimes and codecs.
- **Shortcut Creation**: Automatically creates Desktop and Start Menu shortcuts.
- **Uninstaller Support**: Includes a clean uninstaller accessible via `UNINSTALL.bat` or Windows Settings.
- **Admin Required**: Installs to `Program Files` for all users.

---

## 🎯 Features & Usage

### Key Features
- ✅ **Multi-Monitor Support**: Automatic detection and fullscreen projection on second displays.
- ✅ **Media Playback**: Supports video and audio with progress controls and independent volume.
- ✅ **Background Music**: Pulsing visual feedback and dedicated controls.
- ✅ **Playlist Management**: Drag-and-drop support for organizing service elements.
- ✅ **Remote Control**: Web-based remote control for operation from mobile devices.

### Basic Usage
- **Add Media**: Use the "Add Files" button or drag-and-drop files into the playlist.
- **Go Live**: Double-click an item or use the "Go Live" button to project it.
- **Blank Screen**: Hides the current content on the live display.
- **Toggle Display**: Quickly show or hide the live window.

---

## 🗑️ Uninstallation
To remove the application:
1. **Right-click** on `UNINSTALL.bat` in the installation folder.
2. Select **"Run as administrator"**.
3. Follow the prompts to remove all shortcuts and application files.

---

## 📞 Support
For issues or contributions, please visit the GitHub repository:  
[github.com/wsegbert65360/ChurchDisplayApp](https://github.com/wsegbert65360/ChurchDisplayApp)
