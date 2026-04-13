# Installation & Build Guide

The only supported method for creating the Church Display App installer is using `build-release.bat` (which calls Inno Setup 6).

## Prerequisites
* **.NET 8.0 SDK** (required for building the application)
* **Inno Setup 6** (required for compiling the installer executable)

## Building the Installer
1. Run `build-release.bat` from the repository root.
2. The script will automatically clean, build, publish the self-contained app, and invoke Inno Setup.
3. The generated installer executable will be output to the `bin\Installer\` directory.

**Note:** The generated installer automatically handles:
* **VC++ Redistributable:** Downloads and installs it if missing on the target system.
* **Firewall Rules:** Automatically configures Windows Defender Firewall to allow inbound traffic on ports 8088 (Primary) and 8090 (Fallback).
* **Shortcuts:** Creates Desktop and Start Menu shortcuts.

## Portable Build
To build a portable version without creating an installer:
1. Run `publish.bat`.
2. The self-contained portable output will be available in `bin\Publish\win-x64\`.
