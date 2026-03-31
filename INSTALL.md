# Church Display App - Installation Instructions

## Quick Install (Recommended)

1. **Download** the `ChurchDisplayApp-Setup.exe` installer.
2. **Run** the installer.
3. **Follow** the on-screen wizard to complete the installation.
4. **Launch** the application from your Desktop or Start Menu.

## System Requirements

- **OS**: Windows 10 or Windows 11 (64-bit)
- **Runtime**: .NET 8.0/10.0 Runtime (packaged in the installer)
- **Media Support**: Visual C++ 2015-2022 Redistributable (x64) - **Required for video playback**
  Download: [aka.ms/vs/17/release/vc_redist.x64.exe](https://aka.ms/vs/17/release/vc_redist.x64.exe)
- **Hardware**: Graphics card with DirectX support, second monitor or projector recommended.

## First Time Setup

1. **Launch** the application.
2. **Select Background Music**: When prompted, choose the folder containing your church's background music.
3. **Configure Display**: Go to Settings to select which monitor/projector should be used for the live output.
4. **Add Media**: Use the "Add Files" button or drag-and-drop media onto the playlist.
5. **Go Live**: Double-click an item in the playlist to start the projection.

## Building from Source (Developers)

If you are a developer and want to build the installer yourself:
1. Ensure **Inno Setup 6** is installed on your Windows machine.
2. Open the solution in **Visual Studio 2022**.
3. Run **`sync-fcc.bat`** from the command line. This script will build the release, generate the installer, and copy it to the deployment folder.

## Support

For technical assistance, please refer to the [DOCS.md](DOCS.md) file or contact your church's IT support team.
