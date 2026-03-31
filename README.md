# ChurchDisplayApp

A professional display management system designed for churches to manage and project lyrics, announcements, and media during services.

## Features

- **Display Management**: Project content to a dedicated secondary monitor or projector.
- **Remote Control**: Control the service flow from any device on the same network via a web-browser based remote control.
- **Monitor Selection**: Easily select which connected display to use for live output.
- **Service Plan Management**: Organize service elements (songs, prayers, etc.) in a sequential order.
- **Media Support**: Play background music and main media files with independent volume controls.
- **Hardened System**: Thread-safe settings with debounced auto-save and a centralized premium design system.

## System Requirements

- **Operating System**: Windows 10 or Windows 11 (64-bit).
- **Runtime**: .NET 8.0/10.0 Runtime (packaged in the installer).
- **VLC Media Player**: Required for media playback functionality (packaged in the installer).

## Installation

ChurchDisplayApp is distributed as a standard Windows Installer.
1. Download `ChurchDisplayApp-Setup.exe`.
2. Run the installer and follow the prompts.
3. Shortcuts will be created on your Desktop and Start Menu.

## Build Instructions (Developers)

1. Open `ChurchDisplayApp.sln` in Visual Studio 2022.
2. Ensure **Inno Setup 6** is installed on your system.
3. Run the **`sync-fcc.bat`** script from the project root to:
   - Build and Publish the application.
   - Generate the Windows Installer.
   - Deploy the installer to the sync directory.

## Remote Control

To use the remote control:
1. Launch the application.
2. Ensure your controlling device (phone, tablet, or laptop) is on the same Wi-Fi network.
3. The application will display the server's IP address and port.
4. Navigate to that address in your web browser.

## Documentation

Detailed setup and technical information can be found in [DOCS.md](DOCS.md) and [BLUEPRINT.md](BLUEPRINT.md).
