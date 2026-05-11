using System.Collections.ObjectModel;
using System.Windows.Input;
using ChurchDisplayApp.Services;
using ChurchDisplayApp.Models;

namespace ChurchDisplayApp.ViewModels;

/// <summary>
/// The main ViewModel for the application, managing the playlist, media playback, and per-item volume.
/// </summary>
public class MainViewModel : BaseViewModel
{
    private readonly PlaylistManager _playlistManager;
    private readonly MediaControlService _mediaControlService;
    private readonly AppSettings _settings;
    private double _volume = 0.5;
    private double _progress = 0;
    private string _currentMediaTitle = "Idle";
    private string _currentTimeStr = "00:00";
    private string _durationStr = "00:00";
    private bool _isPlaying = false;
    private bool _isScrubbing = false;
    private PlaylistItem? _selectedItem;
    private double _playlistFontSize = 13;

    public MainViewModel(PlaylistManager playlistManager, MediaControlService mediaControlService, AppSettings settings)
    {
        _playlistManager = playlistManager ?? throw new ArgumentNullException(nameof(playlistManager));
        _mediaControlService = mediaControlService ?? throw new ArgumentNullException(nameof(mediaControlService));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        
        _volume = _settings.MainMediaVolume;
        _playlistFontSize = _settings.PlaylistFontSize;
        _mediaControlService.SetVolume(_volume);

        // Commands
        PlayCommand = new RelayCommand(_ => PlaySelected());
        StopCommand = new RelayCommand(_ => Stop());
        PauseCommand = new RelayCommand(_ => Pause());
        TogglePlayPauseCommand = new RelayCommand(_ => TogglePlayPause());
        BlankCommand = new RelayCommand(_ => Blank());
        NextCommand = new RelayCommand(_ => Next());
        PreviousCommand = new RelayCommand(_ => Previous());
        ToggleFullscreenCommand = new RelayCommand(_ => ToggleFullscreen());
        ExitFullscreenCommand = new RelayCommand(_ => ExitFullscreen());
        
        // Listen to playlist changes to update background hint
        _playlistManager.Items.CollectionChanged += (s, e) => OnPropertyChanged(nameof(PlaylistBackground));
    }

    public ObservableCollection<PlaylistItem> PlaylistItems => _playlistManager.Items;

    public PlaylistItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                Serilog.Log.Debug(
                    "SelectedItem changed to {FileName} (Volume={Volume:P0}); currently loaded item is {LoadedFile}",
                    value?.FileName ?? "<none>",
                    value?.Volume ?? 0,
                    _currentlyLoadedItem?.FileName ?? "<none>");
                OnPropertyChanged(nameof(SelectedItemVolumePercent));
                OnPropertyChanged(nameof(IsSelectedItemPlaying));
            }
        }
    }

    /// <summary>
    /// Gets the selected item's volume as a display string (e.g. "80%").
    /// </summary>
    public string SelectedItemVolumePercent => SelectedItem != null 
        ? $"{(int)(SelectedItem.Volume * 100)}%" 
        : $"{(int)(_settings.DefaultServiceVolume * 100)}%";

    public double Volume
    {
        get => _volume;
        set
        {
            var clampedValue = Math.Clamp(value, 0.0, 1.0);
            var oldVolume = _volume;
            if (SetProperty(ref _volume, clampedValue))
            {
                Serilog.Log.Debug(
                    "Main volume changed {Old:P0} -> {New:P0} (input {Input}); selected={Selected}; loaded={Loaded}",
                    oldVolume,
                    clampedValue,
                    value,
                    SelectedItem?.FileName ?? "<none>",
                    _currentlyLoadedItem?.FileName ?? "<none>");

                _mediaControlService.SetVolume(clampedValue);
                _settings.MainMediaVolume = clampedValue;
                _settings.Save();
                
                // Save it back to the currently loaded item so its volume persists!
                // Verify the item is still valid (not removed from playlist)
                if (_currentlyLoadedItem != null && _playlistManager.Items.Contains(_currentlyLoadedItem))
                {
                    _currentlyLoadedItem.Volume = clampedValue;
                    Serilog.Log.Debug(
                        "Applied volume {Volume:P0} to currently loaded item {FileName}",
                        clampedValue,
                        _currentlyLoadedItem.FileName);
                }
                
                OnPropertyChanged(nameof(VolumePercentage));
            }
        }
    }

    public string VolumePercentage => $"{(int)(_volume * 100)}%";

    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public double PlaylistFontSize
    {
        get => _playlistFontSize;
        set
        {
            if (SetProperty(ref _playlistFontSize, value))
            {
                _settings.PlaylistFontSize = value;
                _settings.Save();
            }
        }
    }

    public bool IsScrubbing
    {
        get => _isScrubbing;
        set => SetProperty(ref _isScrubbing, value);
    }

    public string CurrentMediaTitle
    {
        get => _currentMediaTitle;
        set => SetProperty(ref _currentMediaTitle, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set 
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertyChanged(nameof(MediaControlsBackground));
            }
        }
    }

    private static readonly System.Windows.Media.SolidColorBrush PlayingBackgroundBrush = 
        new(System.Windows.Media.Color.FromRgb(135, 206, 250));

    private static readonly System.Windows.Media.SolidColorBrush EmptyPlaylistBrush = 
        new(System.Windows.Media.Color.FromArgb(100, 173, 216, 230));

    public System.Windows.Media.Brush MediaControlsBackground => IsPlaying 
        ? PlayingBackgroundBrush 
        : System.Windows.Media.Brushes.Transparent;

    public System.Windows.Media.Brush PlaylistBackground => _playlistManager.Items.Count == 0
        ? EmptyPlaylistBrush
        : System.Windows.Media.Brushes.White;

    /// <summary>
    /// Gets a value indicating whether the currently selected item is also the explicit now-playing item.
    /// </summary>
    public bool IsSelectedItemPlaying => SelectedItem != null && _currentlyLoadedItem == SelectedItem;

    public string CurrentTimeStr
    {
        get => _currentTimeStr;
        set => SetProperty(ref _currentTimeStr, value);
    }

    public string DurationStr
    {
        get => _durationStr;
        set => SetProperty(ref _durationStr, value);
    }

    // Commands
    public ICommand PlayCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand TogglePlayPauseCommand { get; }
    public ICommand BlankCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand ToggleFullscreenCommand { get; }
    public ICommand ExitFullscreenCommand { get; }

    private PlaylistItem? _currentlyLoadedItem;

    /// <summary>
    /// Marks a playlist item as the authoritative now-playing item.
    /// Only called after playback is successfully handed off to the media service.
    /// </summary>
    private void SetNowPlayingItem(PlaylistItem item)
    {
        if (!ReferenceEquals(_currentlyLoadedItem, item))
        {
            Serilog.Log.Debug(
                "Now-playing item set to {FileName} (Volume={Volume:P0})",
                item.FileName,
                item.Volume);
        }

        _currentlyLoadedItem = item;
        OnPropertyChanged(nameof(IsSelectedItemPlaying));
    }

    /// <summary>
    /// Clears the authoritative now-playing item.
    /// </summary>
    private void ClearNowPlayingItem(string reason)
    {
        if (_currentlyLoadedItem != null)
        {
            Serilog.Log.Debug("Now-playing item cleared ({Reason}): {FileName}", reason, _currentlyLoadedItem.FileName);
        }

        _currentlyLoadedItem = null;
        OnPropertyChanged(nameof(IsSelectedItemPlaying));
    }

    private void PlaySelected()
    {
        if (SelectedItem != null)
        {
            Serilog.Log.Debug(
                "PlaySelected invoked for {FileName} (item volume {Volume:P0})",
                SelectedItem.FileName,
                SelectedItem.Volume);

            // If already loaded and just paused, simply resume
            if (SelectedItem == _currentlyLoadedItem && !IsPlaying)
            {
                // Re-apply the selected item's volume on resume
                _mediaControlService.SetVolume(SelectedItem.Volume);
                Volume = SelectedItem.Volume;

                _mediaControlService.Play();
                IsPlaying = true;
                return;
            }

            if (!System.IO.File.Exists(SelectedItem.FullPath))
            {
                _playlistManager.RemoveItem(SelectedItem);
                return;
            }

            try
            {
                _mediaControlService.PlayMedia(SelectedItem.FullPath, SelectedItem.Volume);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to play selected item {FileName}", SelectedItem.FileName);
                IsPlaying = false;
                return;
            }

            SetNowPlayingItem(SelectedItem);
            CurrentMediaTitle = SelectedItem.FileName;
            // Sync the global volume slider to reflect the item's volume
            Volume = SelectedItem.Volume;
            IsPlaying = true;
        }
    }

    private void Stop()
    {
        _mediaControlService.Stop();
        ReconcilePlaybackStopped("Stop command");
    }

    /// <summary>
    /// Reconciles the ViewModel to an idle state when playback has ended, failed, or the live output window was recreated.
    /// </summary>
    public void ReconcilePlaybackStopped(string reason)
    {
        IsPlaying = false;
        ClearNowPlayingItem(reason);
        CurrentMediaTitle = "Idle";
        CurrentTimeStr = "00:00";
        DurationStr = "00:00";
        Progress = 0;
    }

    private void Pause()
    {
        _mediaControlService.Pause();
        IsPlaying = false;
    }

    private void TogglePlayPause()
    {
        if (IsPlaying)
            Pause();
        else
            PlaySelected();
    }

    private void Blank()
    {
        _mediaControlService.Blank();
        CurrentMediaTitle = "Idle";
        IsPlaying = false;
    }

    private void Next()
    {
        if (PlaylistItems.Count == 0) return;
        
        int nextIndex = SelectedItem != null ? PlaylistItems.IndexOf(SelectedItem) + 1 : 0;
        if (nextIndex < PlaylistItems.Count)
        {
            SelectedItem = PlaylistItems[nextIndex];
            PlaySelected();
        }
    }

    private void Previous()
    {
        if (PlaylistItems.Count == 0) return;
        
        int prevIndex = SelectedItem != null ? PlaylistItems.IndexOf(SelectedItem) - 1 : PlaylistItems.Count - 1;
        if (prevIndex >= 0)
        {
            SelectedItem = PlaylistItems[prevIndex];
            PlaySelected();
        }
    }

    private void ToggleFullscreen()
    {
        _mediaControlService.ToggleFullscreen();
    }

    private void ExitFullscreen()
    {
        _mediaControlService.SetFullscreen(false);
    }

    public void UpdateProgress()
    {
        try
        {
            if (IsScrubbing) return;

            if (_currentlyLoadedItem != null)
            {
                if (!IsPlaying) return;

                var info = _mediaControlService.GetProgress();
                if (info != null && info.Duration.TotalSeconds > 0)
                {
                    Progress = info.ProgressPercent;
                    CurrentTimeStr = FormatTime(info.CurrentTime);
                    DurationStr = FormatTime(info.Duration);
                }
            }
            else
            {
                CurrentTimeStr = "00:00";
                DurationStr = "00:00";
                Progress = 0;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error updating progress in ViewModel");
        }
    }

    public string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
        {
            return $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
        }
        return $"{time.Minutes:D2}:{time.Seconds:D2}";
    }
}
