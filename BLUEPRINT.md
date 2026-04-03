# ChurchDisplayApp - Architectural Blueprint

## 🏛️ Project Overview
ChurchDisplayApp is a professional Windows-based display management system built primarily for the projection of lyrics, announcements, and media during church services. It emphasizes reliability, visual quality, and ease of use.

## 🏗️ Technical Stack
- **Framework**: WPF (Windows Presentation Foundation)
- **Target Runtime**: .NET 10.0 (compatible with 8.0/later)
- **Language**: C# 14+
- **Media Engine**: LibVLC (VLC.DotNet) for hardware-accelerated playback of diverse media formats.
- **Audio Backend**: WASAPI audio session for main media playback (VLC). Amen resolve uses MeltySynth + NAudio.
- **UI Architecture**: Standard MVVM-lite (Model-View-ViewModel) with custom `RelayCommand` and `BaseViewModel` implementations.

## 🎨 Design System
The application uses a centralized design system to ensure visual consistency and a premium feel.
- **Color Palette**: Defined in `AppConstants.Colors`.
    - **Primary Accent**: `PulseLightBlue` (Hex: `#00BFFF`) used for active indicators, progress bars, and glowing animations.
- **Typography**: Focused on readability for projection.
- **Visual Effects**: Subtle pulsing animations and high-fidelity image scaling.
- **Live Output Progress Bar**: 7px height bar at the bottom of the projected display, showing media playback progress.

## 🔊 Audio Architecture

### Single Main Media Audio Path
The application uses a single audio subsystem for media playback through LibVLC.

| System | Backend | Audio API | Purpose |
|--------|---------|-----------|---------|
| **Main Media Player** | LibVLCSharp | WASAPI (`--aout=wasapi`) | All playlist media playback |

### Per-Playlist-Item Volume
Each playlist item stores its own volume setting (0.0 to 1.0, default 0.8). When a playlist item is played:
1. The item's stored volume is applied to the media player before playback begins.
2. The global volume slider is synced to reflect the item's volume.
3. Users can edit the selected item's volume via the "Selected Item Volume" slider below the media controls.
4. Volume changes are persisted when saving the playlist.

### Volume Management
- **Per-Item Volume**: Stored in `PlaylistItem.Volume` (0.0–1.0), applied when an item starts playing.
- **Global Volume**: Stored in `AppSettings.MainMediaVolume` (0.0–1.0), synced from per-item volume on playback.
- **VLC Re-application**: Volume is set both immediately and after VLC's `Playing` event to handle asynchronous audio initialization.
- **Amen Resolve Volume**: Uses the currently playing item's volume or the global volume as a fallback.

### Amen Resolve
The Amen resolve service uses MeltySynth with a piano SoundFont (SalC5Light2.sf2) and NAudio (WaveOutEvent). It plays a Plagal Cadence (IV-I) chord progression with:
- A dedicated Amen button in the media controls section.
- An `/api/amen` endpoint in the remote control server.
- Behavior: stops current media, then plays the Amen resolve chord.

## ⚙️ Core Services & Components

### 1. Settings Management (`AppSettings.cs`)
- **Strategy**: Asynchronous, debounced auto-save.
- **Mechanism**: Serializes to `settings.json`.
- **Hardening**: Uses `CancellationTokenSource` to debounce rapid changes and a thread-safe `SaveImmediate()` for critical persistence.
- **Backward Compatibility**: Old BGM-related settings fields are preserved with `[Obsolete]` attributes so existing settings files load without errors.

### 2. Media Control (`MediaControlService.cs`)
- Manages playback of all media elements through a single path.
- Handles volume application before and during playback.
- Exposes `PlayMedia(filePath, itemVolume)` for per-item volume support.
- No longer manages background music auto-pause/resume.

### 3. Display Projection (`LiveOutputWindow.cs`)
- Optimized for secondary monitor/projector output.
- Features blackout (blank screen), toggle display, and seamless transitions.
- Disposes VLC `Media` objects properly after each playback to prevent memory leaks.
- Applies volume on the `Playing` event with a delayed re-application for reliability.
- Progress bar (7px) at the bottom of the display window.

### 4. Playlist Management (`PlaylistManager.cs`)
- `ObservableCollection<PlaylistItem>` with drag-and-drop reordering support.
- **Dirty tracking**: `IsDirty` property automatically detects any collection change (add, remove, reorder) via `CollectionChanged` event. Resets on save and load.
- **Per-Item Volume**: Each `PlaylistItem` stores its own `Volume` property (default 0.8). Implements `INotifyPropertyChanged` for UI binding.
- Save/Load via JSON serialization (`.pls` files).
- **Playlist Format v2**: `{ "version": 2, "items": [{ "fullPath": "...", "volume": 0.8 }] }`.
- **Backward Compatibility**: Old playlists (plain array of path strings) are automatically loaded with default volume.
- **Close Playlist**: Prompts user with Yes/No/Cancel dialog if unsaved changes exist before clearing.

### 5. Remote Control (`RemoteControlServer.cs`)
- Embedded HTTP server providing a web-based interface.
- Allows control via any smartphone or tablet on the local network.
- QR code display for easy connection.
- **Endpoints**: `/api/play`, `/api/pause`, `/api/stop`, `/api/blank`, `/api/amen`, `/api/volume/*`, `/api/playlist`, `/api/status`.

### 6. Amen Resolve (`AmenResolveService.cs`)
- Uses MeltySynth synthesizer with a piano SoundFont.
- Plays a Plagal Cadence (IV-I) with randomized velocity for a natural sound.
- Triggered by the dedicated Amen button or remote `/api/amen` endpoint.
- Runs asynchronously with a semaphore to prevent overlapping playback.

## 📐 UI Layout

### Sidebar (Left Panel)
- **ADD MEDIA** button to add files to the playlist.
- **Font size slider** (A-A) for playlist item text.
- **Playlist ListBox** with drag-and-drop reordering.
  - Each item shows: file type icon, file name, and per-item volume percentage.
- **Remote Control QR code** at the bottom.

### Main Content (Right Panel)

#### Media Controls Section
- Section label: "🎬 MEDIA CONTROLS"
- Seek bar with current time / duration display.
- Transport buttons: **Play**, **Pause**, **Stop**, **AMEN** (dedicated button).
- Current media title display.
- Global volume slider with percentage display.

#### Selected Item Volume Editor
- Slider to view and adjust the currently selected playlist item's volume.
- Changes are applied immediately if the item is currently playing.
- Persisted when the playlist is saved.

#### Playlist Controls Section
- Section label: "📂 PLAYLIST CONTROLS"
- Buttons: **LOAD**, **SAVE**, **NEW**, **BLANK**, **DISPLAY**, **CLOSE PLAYLIST**.

## 📦 Deployment & Maintenance

See [BUILD-INSTALLER.md](BUILD-INSTALLER.md) for complete step-by-step build and install instructions.

### Build Pipeline (`sync-fcc.bat`)
A professional automation script that:
1. Performs a **Self-Contained** publish (no external .NET needed on target).
2. Triggers **Inno Setup** to compile the Windows Installer.
3. Automatically synchronizes the installer to deployment folders.

### Installer Logic (`ChurchDisplayApp.iss`)
- Standard Windows Installer focusing on `Program Files` installation.
- Clean uninstallation process.
- Desktop and Start Menu shortcut management.
- Sources from `bin\Publish\win-x64\`.

---
*Last Updated: April 3, 2026*
