# ChurchDisplayApp - Architectural Blueprint

## 🏛️ Project Overview
ChurchDisplayApp is a professional Windows-based display management system built primarily for the projection of lyrics, announcements, and media during church services. It emphasizes reliability, visual quality, and ease of use.

## 🏗️ Technical Stack
- **Framework**: WPF (Windows Presentation Foundation)
- **Target Runtime**: .NET 10.0 (compatible with 8.0/later)
- **Language**: C# 12+
- **Media Engine**: LibVLC (VLC.DotNet) for hardware-accelerated playback of diverse media formats.
- **UI Architecture**: Standard MVVM-lite (Model-View-ViewModel) with custom `RelayCommand` and `BaseViewModel` implementations.

## 🎨 Design System
The application uses a centralized design system to ensure visual consistency and a premium feel.
- **Color Palette**: Defined in `AppConstants.Colors`. 
    - **Primary Accent**: `PulseLightBlue` (Hex: `#00BFFF`) used for active indicators, progress bars, and glowing animations.
- **Typography**: Focused on readability for projection.
- **Visual Effects**: Subtle pulsing animations and high-fidelity image scaling.

## ⚙️ Core Services & Components

### 1. Settings Management (`AppSettings.cs`)
- **Strategy**: Asynchronous, debounced auto-save.
- **Mechanism**: Serializes to `settings.json`.
- **Hardening**: Uses `CancellationTokenSource` to debounce rapid changes and a thread-safe `SaveImmediate()` for critical persistence.

### 2. Media Control (`MediaControlService.cs`)
- Manages playback of background music and main media elements.
- Handles independent volume levels and media state synchronization.

### 3. Display Projection (`LiveOutputWindow.xaml`)
- Optimized for secondary monitor/projector output.
- Features blackout (blank screen), toggle display, and seamless transitions.

### 4. Remote Control (`RemoteControlServer.cs`)
- Embedded HTTP server providing a web-based interface.
- Allows control via any smartphone or tablet on the local network.

## 📦 Deployment & Maintenance

### Build Pipeline (`sync-fcc.bat`)
A professional automation script that:
1. Performs a **Self-Contained** publish (no external .NET needed on target).
2. Triggers **Inno Setup** to compile the Windows Installer.
3. Automatically synchronized the installer to deployment folders (e.g., `D:\FCC Sync Folder`).

### Installer Logic (`ChurchDisplayApp.iss`)
- Standard Windows Installer focusing on `Program Files` installation.
- Clean uninstallation process.
- Desktop and Start Menu shortcut management.

---
*Last Updated: March 31, 2026*
