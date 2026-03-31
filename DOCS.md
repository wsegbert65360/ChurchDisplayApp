# ChurchDisplayApp - Documentation

This document provides comprehensive information on installing, setting up, and building the ChurchDisplayApp.

---

## 🚀 Installation Instructions

### Automated Installation (Recommended)
ChurchDisplayApp is distributed as a professional Windows Installer.
1. **Download** `ChurchDisplayApp-Setup.exe`.
2. **Run** the installer.
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
2. **Inno Setup 6** - Required for creating the Windows Installer (`.exe`). Download from [jrsoftware.org](https://jrsoftware.org/isdl.php).

### Build Steps
The easiest way to build and deploy is using the automated sync script.

#### Using Automated Sync (Recommended)
Run **`sync-fcc.bat`** from the project root. This script:
1. Cleans and publishes the app in Release mode.
2. Compiles the Inno Setup installer.
3. Automatically syncs the installer to the designated deployment folder (e.g., `D:\FCC Sync Folder`).

#### Manual Build (CLI)
```powershell
# Build in Release mode
dotnet build -c Release

# Publish self-contained executable
dotnet publish -c Release -r win-x64 --self-contained true
```
The published files (used by the installer) will be at: `bin\Publish\win-x64\`

---

## 📦 Windows Installer Details

ChurchDisplayApp uses Inno Setup to create a professional single-file installer.

1. **Configuration**: The installer logic is defined in `ChurchDisplayApp.iss`.
2. **Compilation**: The `sync-fcc.bat` script calls `ISCC.exe` to compile the installer.
3. **Output**: The installer is generated at `bin\Installer\ChurchDisplayApp-1.0.0-Setup.exe`.

### Installer Features
- **Self-Contained**: Includes all necessary .NET runtimes and codecs.
- **Shortcut Creation**: Automatically creates Desktop and Start Menu shortcuts.
- **Uninstaller Support**: Includes a clean uninstaller accessible via Windows Settings.
- **Admin Required**: Installs to `Program Files` for all users.

---

## 🎯 Features & Usage

### Key Features
- ✅ **Multi-Monitor Support**: Automatic detection and fullscreen projection on second displays.
- ✅ **Media Playback**: Supports video and audio with progress controls and independent volume.
- ✅ **Background Music**: Pulsing visual feedback and dedicated controls.
- ✅ **Playlist Management**: Drag-and-drop support for organizing service elements.
- ✅ **Remote Control**: Web-based remote control for operation from mobile devices.
- ✅ **Hardened Persistence**: Debounced, thread-safe settings preservation.

### Basic Usage
- **Add Media**: Use the "Add Files" button or drag-and-drop files into the playlist.
- **Go Live**: Double-click an item or use the "Go Live" button to project it.
- **Blank Screen**: Hides the current content on the live display.
- **Toggle Display**: Quickly show or hide the live window.

---

## 🗑️ Uninstallation
To remove the application, use the standard Windows **"Add or Remove Programs"** interface or run the uninstaller created in the installation directory.

---

## 📞 Support
For issues or contributions, please visit the GitHub repository:  
[github.com/wsegbert65360/ChurchDisplayApp](https://github.com/wsegbert65360/ChurchDisplayApp)
