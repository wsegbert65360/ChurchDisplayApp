# ChurchDisplayApp - Architectural Blueprint

## 🏛️ Project Overview
ChurchDisplayApp is a professional Windows-based display management system built primarily for the projection of lyrics, announcements, and media during church services. It emphasizes reliability, visual quality, and ease of use.

## 🏗️ Technical Stack
- **Framework**: WPF (Windows Presentation Foundation)
- **Target Runtime**: .NET 10.0 (compatible with 8.0/later)
- **Language**: C# 14+
- **Media Engine**: LibVLC (VLC.DotNet) for hardware-accelerated playback of diverse media formats.
- **Audio Backend**: Dual independent WASAPI audio sessions (VLC + NAudio).
- **UI Architecture**: Standard MVVM-lite (Model-View-ViewModel) with custom `RelayCommand` and `BaseViewModel` implementations.

## 🎨 Design System
The application uses a centralized design system to ensure visual consistency and a premium feel.
- **Color Palette**: Defined in `AppConstants.Colors`.
    - **Primary Accent**: `PulseLightBlue` (Hex: `#00BFFF`) used for active indicators, progress bars, and glowing animations.
- **Typography**: Focused on readability for projection.
- **Visual Effects**: Subtle pulsing animations and high-fidelity image scaling.
- **Live Output Progress Bar**: 7px height bar at the bottom of the projected display, showing media playback progress.

## 🔊 Audio Architecture

### Dual Independent Audio Sessions
The application uses two completely separate audio subsystems, each with its own isolated WASAPI session to prevent volume contamination and ensure only one audio source plays at a time.

| System | Backend | Audio API | Purpose |
|--------|---------|-----------|---------|
| **VLC Media Player** | LibVLCSharp | WASAPI (`--aout=wasapi`) | Main media playback (playlist videos, audio files) |
| **Background Music** | NAudio | `WasapiOut` | Intro songs, kids sermon music, ambient BGM |

### Why WASAPI for Both
Both players use WASAPI (Windows Audio Session API) to ensure fully independent per-session volume controls in the Windows Volume Mixer. This prevents the volume contamination bug where a lower-volume BGM session would affect the main media player's effective volume. Legacy APIs (DirectSound, WaveOutEvent) share the same per-process audio session and were replaced to fix this.

### Volume Management
- **Main Media Volume**: Stored in `AppSettings.MainMediaVolume` (0.0–1.0), applied via `MediaPlayer.Volume` (0–100 integer) in `LiveOutputWindow`.
- **BGM Volume**: Stored in `AppSettings.BackgroundMusicVolume` (0.0–1.0), applied via `AudioFileReader.Volume` (float) in `BackgroundMusicService`.
- **VLC Re-application**: Volume is set both immediately and after a 150ms delay on the `Playing` event to handle VLC's asynchronous audio output initialization.

### Audio Mutual Exclusivity
The `MediaControlService` ensures only one audio system is active at a time:

| User Action | BGM State | Main Media State |
|-------------|-----------|------------------|
| Play playlist item | Auto-paused | Starts playing |
| Stop media | Auto-resumed (if was auto-paused) | Stopped |
| Blank display | Auto-resumed (if was auto-paused) | Paused/blanked |
| Pause media | No change | Paused (BGM stays paused) |
| Resume paused media | Auto-paused (if playing) | Resumed |
| Play BGM button | Starts playing | Stopped first |
| Next/Previous track | Auto-paused | New track starts |

The `AutoPause()`/`AutoResume()` mechanism uses a boolean flag (`_mainMediaAutoPausedBgm`) to track whether BGM was silenced by media playback, ensuring it only resumes when appropriate (not if the user manually stopped BGM).

### Volume Restoration on Window Recreate
When `LiveOutputWindow` is recreated (e.g., after being closed), the saved volume from `MainViewModel.Volume` is explicitly reapplied via `MediaControlService.SetVolume()` to prevent the hardcoded default of 100 from overriding user preferences.

## ⚙️ Core Services & Components

### 1. Settings Management (`AppSettings.cs`)
- **Strategy**: Asynchronous, debounced auto-save.
- **Mechanism**: Serializes to `settings.json`.
- **Hardening**: Uses `CancellationTokenSource` to debounce rapid changes and a thread-safe `SaveImmediate()` for critical persistence.

### 2. Media Control (`MediaControlService.cs`)
- Manages playback of background music and main media elements.
- Handles independent volume levels and media state synchronization.
- Coordinates auto-pause/resume of BGM when main media starts/stops.
- Exposes `NotifyBgmAutoPaused()` for external BGM pause notification (used by ViewModel resume shortcut).
- Updates live window reference when the display window is recreated.

### 3. Display Projection (`LiveOutputWindow.cs`)
- Optimized for secondary monitor/projector output.
- Features blackout (blank screen), toggle display, and seamless transitions.
- Disposes VLC `Media` objects properly after each playback to prevent memory leaks.
- Applies volume on the `Playing` event with a delayed re-application for reliability.
- Progress bar (7px) at the bottom of the display window.

### 4. Background Music (`BackgroundMusicService.cs`)
- Uses NAudio `WasapiOut` for independent audio session isolation.
- Supports auto-pause (for media overlap prevention) and manual pause/stop.
- Looping playback with automatic restart on track end.
- Pulse animation support for visual BGM status indication.
- Proper cleanup/disposal of `WasapiOut` and `AudioFileReader` instances.

### 5. Playlist Management (`PlaylistManager.cs`)
- `ObservableCollection<PlaylistItem>` with drag-and-drop reordering support.
- **Dirty tracking**: `IsDirty` property automatically detects any collection change (add, remove, reorder) via `CollectionChanged` event. Resets on save and load.
- Save/Load via JSON serialization (`.pls` files).
- **Close Playlist**: Prompts user with Yes/No/Cancel dialog if unsaved changes exist before clearing.

### 6. Remote Control (`RemoteControlServer.cs`)
- Embedded HTTP server providing a web-based interface.
- Allows control via any smartphone or tablet on the local network.
- QR code display for easy connection.

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
*Last Updated: April 2, 2026*
