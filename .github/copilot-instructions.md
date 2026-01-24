# Church Display App - AI Coding Agent Instructions

## Project Overview
ChurchDisplayApp is a WPF desktop application (C# .NET 10.0) that enables church operators to manage and display media content on a secondary display during services. The app consists of a control window for operators and a separate live output window for projection/display.

## Architecture

### Two-Window Pattern
- **MainWindow** (`MainWindow.xaml.cs`): Control interface for operators
  - Manages playlist of media files (images, videos, audio)
  - Buttons: "Add Files", "Go Live", "Blank"
  - Single-select ListBox for media items
- **LiveOutputWindow** (`LiveOutputWindow.cs`): Secondary display window
  - Displays selected media name as white text on black background
  - Can show blank screen
  - Programmatically created (not in XAML)
  - 800x450 size, centered on MainWindow

### Data Flow
1. User selects file → adds to PlaylistListBox
2. User selects item and clicks "Go Live" → passes filename to LiveOutputWindow
3. LiveOutputWindow.ShowText() displays the filename
4. "Blank" button clears display via ShowBlank()

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
- MainWindow constructor creates and shows LiveOutputWindow automatically

### Key Technologies
- **WPF** (Windows Presentation Foundation) for UI
- **C# 11+** (implicit usings, file-scoped namespaces)
- **.NET 10.0** Windows-specific framework

## Code Patterns & Conventions

### Namespace Usage
- Single namespace: `ChurchDisplayApp` for all classes
- File-scoped namespaces used (C# 11 style in App.xaml.cs)

### Event Handling (MainWindow)
- Button clicks wired in XAML: `Click="MethodName_Click"`
- Method naming: `[ElementName]_[EventName]` (e.g., `AddFiles_Click`)
- Handler signature: `private void MethodName(object sender, RoutedEventArgs e)`

### UI Construction
- MainWindow: XAML-defined (declarative)
- LiveOutputWindow: Programmatically created in C# (no XAML file)
  - Use Grid as root container
  - Apply Brushes.Black for background and text styling
  - TextBlock for display (wrap with TextWrapping.Wrap)

### File Dialog Pattern
```csharp
var dlg = new OpenFileDialog { Multiselect = true, Filter = "..." };
if (dlg.ShowDialog() == true) { /* process dlg.FileNames */ }
```

## Key Files & Their Responsibilities

- [MainWindow.xaml](../MainWindow.xaml) / [MainWindow.xaml.cs](../MainWindow.xaml.cs): Operator control UI
- [LiveOutputWindow.cs](../LiveOutputWindow.cs): Secondary display logic
- [ChurchDisplayApp.csproj](../ChurchDisplayApp.csproj): Project configuration, target framework
- [App.xaml](../App.xaml) / [App.xaml.cs](../App.xaml.cs): Application startup, resource definitions

## Common Tasks

**Adding a new feature:**
1. If UI element → add to MainWindow.xaml grid layout
2. If button → wire Click event in XAML, implement handler in MainWindow.xaml.cs
3. If display logic → update LiveOutputWindow methods (ShowText, ShowBlank variants)

**Debugging:**
- LiveOutputWindow is owned by MainWindow (set via Owner property)
- Default show position: centered on owner
- Ensure window operations happen on UI thread (WPF requirement)
