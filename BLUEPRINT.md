# ChurchDisplayApp - Architectural Blueprint

## Project Overview

ChurchDisplayApp is a Windows WPF display control app for church services. It manages a service playlist, projects media to a live output window, shows a live preview in the operator window, and exposes a browser-based remote control for devices on the same local network.

The current program is centered on one playlist and one active media playback path. Images are rendered directly by WPF, while video and audio playback use LibVLC.

## Technical Stack

- Framework: WPF on .NET 8 (`net8.0-windows`)
- Language: C# with nullable reference types enabled
- Media playback: LibVLCSharp.WPF with VideoLAN.LibVLC.Windows
- Remote control: Embedded ASP.NET Core/Kestrel server
- Remote UI: Static HTML/CSS/JavaScript copied from `RemoteControl/`
- Audio support: VLC DirectSound for media; MeltySynth and NAudio for Amen resolve
- Persistence: JSON settings under `%APPDATA%\ChurchDisplayApp\settings.json`
- Installer: Inno Setup using the published `win-x64` output

## Runtime Flow

1. `App.xaml.cs` starts the application and shows the main WPF window.
2. `MainWindow` loads `AppSettings.Current`, initializes XAML, and starts VLC initialization asynchronously.
3. While VLC loads, the main window uses `LoadingPlaceholderViewModel` so bindings are valid.
4. When VLC is ready, `MainWindow` creates:
   - `LiveOutputWindow`
   - `MediaControlService`
   - `MainViewModel`
   - `PlaylistDragDropManager`
   - `AmenResolveService` if the SoundFont exists
5. The live output window is positioned on a selected external monitor when available.
6. The remote control server starts on port `8088`, falling back to `8090`.
7. The operator uses the playlist, media controls, service elements window, or remote browser UI to control playback.

## Core Components

### AppSettings

`AppSettings.cs` owns persisted user settings:

- Main media volume
- Default service volume
- Playlist font size
- Sidebar width proportion
- Last media directory
- Last playlist directory
- Service element slots
- Optional VLC audio device override

Settings use debounced saves through `Save()` and immediate saves through `SaveImmediate(force: true)` for shutdown-critical persistence.

`LastPlaylistDirectory` is serialized under the legacy JSON key `LastPlaylistSaveDirectory` so existing settings continue to load.

### MainWindow

`MainWindow.xaml.cs` is the orchestration layer. It:

- Initializes VLC on a background thread and creates WPF media objects on the UI thread.
- Owns the `PlaylistManager`.
- Creates the real `MainViewModel` after VLC is ready.
- Starts and stops the remote control server.
- Updates the live preview image on an adaptive timer.
- Handles playlist dialogs, drag/drop handlers, display toggling, Amen, and shutdown cleanup.
- Verifies expected firewall rules without blocking startup indefinitely.

### MainViewModel

`MainViewModel.cs` coordinates operator state:

- Selected playlist item
- Authoritative now-playing item
- Current media title
- Current time, duration, and progress
- Playback commands
- Single active media volume slider
- Playlist font size

When an item starts playing, the ViewModel applies that item's stored volume and syncs the operator volume slider. While that item remains the now-playing item, slider changes are written back to the item's `Volume`.

### MediaControlService

`MediaControlService.cs` is a thin wrapper around `LiveOutputWindow`. It applies volume before playback starts, forwards transport commands, and exposes progress/status queries to the ViewModel and controller boundary.

### LiveOutputWindow

`LiveOutputWindow.cs` is the projection window. It:

- Shows images through a WPF `Image`.
- Plays video/audio through LibVLC.
- Shows a 7px progress bar at the bottom of the live output window.
- Supports blank, stop, pause, resume, seek, and fullscreen toggle.
- Generates live preview snapshots for videos.
- Returns the current image source directly for image preview.
- Uses a media version token so stale async image loads cannot reappear after Stop or Blank.
- Disposes VLC media/player resources during shutdown.

Blanking hides projected content without clearing the current media path, so playback can be resumed. Stopping clears the active media path.

### PlaylistManager

`PlaylistManager.cs` owns `ObservableCollection<PlaylistItem>` and playlist persistence.

Current behavior:

- Adds supported media files only.
- Supports drag-and-drop insertion and reordering.
- Tracks dirty state for collection changes and per-item volume edits.
- Saves `.pls` playlists as JSON v2:

```json
{
  "version": 2,
  "items": [
    {
      "fullPath": "C:\\Media\\song.mp4",
      "volume": 1.0
    }
  ]
}
```

- Loads legacy playlists that are arrays of path strings.
- Parses playlists transactionally so a failed load preserves the current playlist.
- Validates that v2 `items` is an array.
- Clamps loaded volume values to `0.0..1.0`.

### Service Elements

`ServiceElementsWindow` is the service plan creator. It uses `AppSettings.ServiceSlots`, where each slot has:

- Stable `Id`
- Display name
- Optional file path
- Sticky/remember flag
- Last-used folder
- Optional per-slot default volume override

Non-sticky slots are cleared when the window opens. Sticky slots preserve their file path unless the file no longer exists. Sending items to the playlist uses the slot volume override when set, otherwise `AppSettings.DefaultServiceVolume`.

### AmenResolveService

`AmenResolveService.cs` plays a Plagal Cadence using MeltySynth and NAudio. It:

- Uses `Sounds/SalC5Light2.sf2`.
- Runs asynchronously.
- Uses a semaphore to prevent overlap.
- Supports cancellation during shutdown or repeated action.
- Locks synthesizer access so MIDI commands and render reads do not race.

Amen can be triggered from the operator UI or from `/api/amen`.

### RemoteControlServer

`RemoteControlServer.cs` hosts the static remote UI and JSON endpoints.

Ports:

- Preferred: `8088`
- Fallback: `8090`

Endpoints:

- `GET /`
- `GET /api/playlist`
- `GET /api/status`
- `POST /api/play`
- `POST /api/play/{index:int}`
- `POST /api/pause`
- `POST /api/stop`
- `POST /api/blank`
- `POST /api/amen`
- `POST /api/volume/up`
- `POST /api/volume/down`
- `POST /api/volume/{level:double}`

The controller boundary checks whether the media engine is ready before executing VLC-dependent actions. During shutdown, applicable endpoints return `503`.

## UI Layout

### Main Operator Window

Left sidebar:

- Add Media button
- Playlist font size slider
- Playlist list with file type icon, file name, and per-item volume percentage
- Context menu delete
- Drag-and-drop reordering and file insertion
- Playlist controls:
  - Load Playlist
  - Save Playlist
  - New Playlist
  - Close Playlist
  - Display

Right content area:

- Live output preview
- Current/duration time display
- Seek slider
- Transport buttons:
  - Play
  - Pause
  - Stop
  - Amen
- Current media title
- Single active media volume slider
- Remote QR code and URL

Keyboard bindings:

- Space: play/pause
- Right: next
- Left: previous
- B: blank
- F11: toggle fullscreen
- Escape: exit fullscreen

### Live Output Window

The projected display is borderless, black-backed, and positioned on the selected monitor. It shows the active image/video/audio state and a bottom progress bar for media with duration.

### Remote Web UI

The remote page shows:

- Playlist cards
- Active item highlight
- Fixed bottom player
- Play, pause, stop, Amen
- Volume down/up buttons
- Volume slider
- Mute/restore toggle
- Progress display

The remote UI is served from the app itself and is intended for devices on the same local network.

## Supported Media

Images:

- `.jpg`
- `.jpeg`
- `.png`
- `.bmp`
- `.gif`

Video:

- `.mp4`
- `.mov`
- `.wmv`
- `.mkv`

Audio:

- `.mp3`
- `.wav`
- `.flac`
- `.wma`
- `.m4a`

## Build And Deployment

Project file:

- `ChurchDisplayApp.csproj`
- Version: `1.3.0`
- Target: `net8.0-windows`

Build scripts:

- `build-release.bat`: release publish and installer build
- `publish.bat`: portable publish
- `sync-fcc.bat`: release publish, installer build, and optional sync to deployment folder

Both release scripts publish `ChurchDisplayApp.csproj` explicitly to `bin\Publish\win-x64` so the SDK does not treat the output path as a solution-level output.

Installer:

- `ChurchDisplayApp.iss`
- Builds `bin\Installer\ChurchDisplayApp-{version}-Setup.exe`
- Installs from `bin\Publish\win-x64`
- Checks for Visual C++ Redistributable
- Creates firewall rules for ports `8088` and `8090`
- Removes those rules on uninstall

## Current Reliability Notes

- VLC initialization can be cancelled during shutdown.
- LibVLC instances created during cancelled initialization are disposed.
- Settings changes use debounced persistence and forced shutdown persistence.
- Playlist load is transactional.
- Image display uses async file reads with stale-load cancellation.
- Resume operations report success back to the ViewModel so the UI does not claim playback is active when reload/resume fails.
- Remote server shutdown gates are reset on start.
- Remote action endpoints return `503` during shutdown.
- Remote volume input is clamped.
- Media errors reconcile the ViewModel to a stopped state.

---

Last updated: May 11, 2026
