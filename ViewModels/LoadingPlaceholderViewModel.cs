using System.Collections.ObjectModel;
using System.Windows.Input;
using ChurchDisplayApp.Models;

namespace ChurchDisplayApp.ViewModels;

/// <summary>
/// PHASE 2: Minimal placeholder ViewModel used while VLC initializes.
/// Provides no-op implementations of all commands so XAML bindings don't crash.
/// </summary>
public class LoadingPlaceholderViewModel : BaseViewModel
{
    private static readonly RelayCommand NoOpCommand = new(_ => { /* no-op while loading */ });

    public ObservableCollection<PlaylistItem> PlaylistItems { get; } = new();

    public PlaylistItem? SelectedItem { get; set; }
    public double Volume { get; set; } = 0.5;
    public string VolumePercentage => "50%";
    public double Progress { get; set; }
    public double PlaylistFontSize { get; set; } = 13;
    public bool IsScrubbing { get; set; }
    public string CurrentMediaTitle { get; set; } = "Loading...";
    public bool IsPlaying { get; set; }
    public string CurrentTimeStr { get; set; } = "00:00";
    public string DurationStr { get; set; } = "00:00";

    // All commands are no-ops until real ViewModel is ready
    public ICommand PlayCommand => NoOpCommand;
    public ICommand StopCommand => NoOpCommand;
    public ICommand PauseCommand => NoOpCommand;
    public ICommand TogglePlayPauseCommand => NoOpCommand;
    public ICommand BlankCommand => NoOpCommand;
    public ICommand NextCommand => NoOpCommand;
    public ICommand PreviousCommand => NoOpCommand;
    public ICommand ToggleFullscreenCommand => NoOpCommand;
    public ICommand ExitFullscreenCommand => NoOpCommand;
}
