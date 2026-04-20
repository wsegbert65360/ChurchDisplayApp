# Church Display App - AI Coding Agent Instructions

## Project Overview
ChurchDisplayApp is a WPF desktop application (C# .NET 10.0) that enables church operators to manage and display media content on a secondary display during services. The app consists of a control window for operators and a separate live output window for projection/display.

## Architecture

### Two-Window Pattern
- **MainWindow** (`MainWindow.xaml.cs`): Control interface for operators
  - Manages playlist of media files (images, videos, audio)
  - Per-playlist-item volume support
  - Dedicated Amen button for musical resolve
  - Buttons: "Add Media", "Play", "Pause", "Stop", "Amen", "Blank", "Display"
  - Single-select ListBox for media items with drag-and-drop reordering
- **LiveOutputWindow** (`LiveOutputWindow.cs`): Secondary display window
  - Displays media content via LibVLC
  - Can show blank screen
  - Programmatically created (not in XAML)

### Data Flow
1. User adds files via "ADD MEDIA" button or drag-and-drop
2. User selects item and double-clicks or clicks Play → passes to media player
3. Per-item volume is applied automatically on playback
4. "Blank" button hides display, "Amen" button stops media and plays resolve chord
5. Remote control accessible via web browser on same network

### Services
- **MediaControlService**: Single media playback path through LibVLC. Accepts per-item volume.
- **PlaylistManager**: Manages playlist items with save/load (JSON v2 format with per-item volume).
- **AmenResolveService**: Plays a Plagal Cadence using MeltySynth + NAudio.
- **RemoteControlServer**: Embedded HTTP server for web-based remote control.
- **MonitorService**: Detects and positions windows on multiple monitors.

### ViewModel
- **MainViewModel**: MVVM-lite ViewModel with ObservableCollection playlist, media transport commands, volume, progress tracking.

## Build & Development

### Build
```
dotnet build
```
- Target: `net10.0-windows` WPF application
- Implicit usings enabled (C# 11 style)
- Nullable reference types enabled

### Run
```
dotnet run
```
- Creates MainWindow first (entry point defined in App.xaml `StartupUri`)
- MainWindow constructor creates LiveOutputWindow automatically

### Key Technologies
- **WPF** (Windows Presentation Foundation) for UI
- **C# 14+** (file-scoped namespaces, init-only properties)
- **.NET 10.0** Windows-specific framework
- **LibVLCSharp** for media playback
- **MeltySynth** + **NAudio** for Amen resolve
- **Serilog** for structured logging

## Code Patterns & Conventions

### Namespace Usage
- Primary namespace: `ChurchDisplayApp` for main classes
- `ChurchDisplayApp.Services` for service classes
- `ChurchDisplayApp.ViewModels` for ViewModel classes
- `ChurchDisplayApp.Models` for data models
- `ChurchDisplayApp.Interfaces` for interfaces

### Event Handling (MainWindow)
- Button clicks wired in XAML: `Click="MethodName_Click"`
- Command binding for ViewModel commands: `Command="{Binding PlayCommand}"`
- Method naming: `[ElementName]_[EventName]` (e.g., `AddFiles_Click`)

### UI Construction
- MainWindow: XAML-defined (declarative)
- LiveOutputWindow: Programmatically created in C# (no XAML file)

### File Dialog Pattern
```csharp
var dlg = new OpenFileDialog { Multiselect = true, Filter = "..." };
if (dlg.ShowDialog() == true) { /* process dlg.FileNames */ }
```

## Key Files & Their Responsibilities

- `MainWindow.xaml` / `MainWindow.xaml.cs`: Operator control UI
- `LiveOutputWindow.cs`: Secondary display logic
- `MainViewModel.cs`: ViewModel for playlist, playback, and volume
- `MediaControlService.cs`: Main media playback service
- `PlaylistManager.cs`: Playlist CRUD with per-item volume
- `AmenResolveService.cs`: Musical Amen resolve via MeltySynth
- `RemoteControlServer.cs`: Web-based remote control HTTP server
- `IDisplayController.cs`: Interface for remote control operations
- `AppSettings.cs`: Thread-safe settings with debounced save
- `Models/PlaylistItem.cs`: Playlist item with INotifyPropertyChanged
- `Models/RemoteModels.cs`: Remote status and playlist item records

## Common Tasks

**Adding a new feature:**
1. If UI element → add to MainWindow.xaml layout
2. If button → wire Click event in XAML, implement handler in MainWindow.xaml.cs
3. If ViewModel command → add RelayCommand and handler in MainViewModel
4. If display logic → update LiveOutputWindow or MediaControlService methods

**Debugging:**
- LiveOutputWindow is owned by MainWindow (set via Owner property)
- Ensure window operations happen on UI thread (WPF requirement)
- Remote control uses dispatcher to marshal calls to UI thread
