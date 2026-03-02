# ChurchDisplayApp

A professional display management system designed for churches to manage and project lyrics, announcements, and media during services.

## Features

- **Display Management**: Project content to a dedicated secondary monitor or projector.
- **Remote Control**: Control the service flow from any device on the same network via a web-browser based remote control.
- **Monitor Selection**: Easily select which connected display to use for live output.
- **Service Plan Management**: Organize service elements (songs, prayers, etc.) in a sequential order.
- **Media Support**: Play background music and main media files with independent volume controls.

## System Requirements

- **Operating System**: Windows 10 or Windows 11.
- **Runtime**: .NET 6.0 or later (depending on the build target).
- **VLC Media Player**: Required for media playback functionality.

## Build Instructions

1.  Open `ChurchDisplayApp.sln` in Visual Studio 2022.
2.  Restore NuGet packages.
3.  Build the solution in `Release` or `Debug` configuration.
4.  Run the `build-release.bat` script for a production-ready folder.

## Remote Control

To use the remote control:
1.  Launch the application.
2.  Ensure your controlling device (phone, tablet, or laptop) is on the same Wi-Fi network.
3.  The application will display the server's IP address and port.
4.  Navigate to that address in your web browser.

## Documentation

Comprehensive setup and installer information can be found in [DOCS.md](DOCS.md).
