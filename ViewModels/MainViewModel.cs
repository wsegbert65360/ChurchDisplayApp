using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
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
        
        _playlistFontSize = _settings.PlaylistFontSize;

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
        _playlistManager.Items.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(PlaylistBackground));
            OnPropertyChanged(nameof(Volume));
            OnPropertyChanged(nameof(VolumePercentage));
            OnPropertyChanged(nameof(NowPlayingIndex));
            OnPropertyChanged(nameof(IsSelectedItemPlaying));

            if (_currentlyLoadedItem != null && !_playlistManager.Items.Contains(_currentlyLoadedItem))
                Stop();
        };
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
                OnPropertyChanged(nameof(Volume));
                OnPropertyChanged(nameof(VolumePercentage));
            }
        }
    }

    /// <summary>
    /// Gets the selected item's volume as a display string (e.g. "80%").
    /// </summary>
    public string SelectedItemVolumePercent => SelectedItem != null 
        ? $"{(int)(SelectedItem.Volume * 100)}%" 
        : $"{(int)(_settings.DefaultServiceVolume * 100)}%";

    private PlaylistItem? VolumeTarget
    {
        get
        {
            if (_currentlyLoadedItem != null && _playlistManager.Items.Contains(_currentlyLoadedItem))
                return _currentlyLoadedItem;
            return SelectedItem;
        }
    }

    public double Volume
    {
        get => VolumeTarget?.Volume ?? _settings.DefaultServiceVolume;
        set
        {
            if (!double.IsFinite(value))
                return;

            var clampedValue = Math.Clamp(value, 0.0, 1.0);
            var oldVolume = Volume;
            if (Math.Abs(oldVolume - clampedValue) <= 0.001)
                return;

            Serilog.Log.Debug(
                "Volume changed {Old:P0} -> {New:P0}; selected={Selected}; loaded={Loaded}",
                oldVolume,
                clampedValue,
                SelectedItem?.FileName ?? "<none>",
                _currentlyLoadedItem?.FileName ?? "<none>");

            _mediaControlService.SetVolume(clampedValue);
            var target = VolumeTarget;
            if (target != null)
                target.Volume = clampedValue;

            OnPropertyChanged(nameof(Volume));
            OnPropertyChanged(nameof(VolumePercentage));
            OnPropertyChanged(nameof(SelectedItemVolumePercent));
        }
    }

    public string VolumePercentage => $"{(int)(Volume * 100)}%";

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

    public int NowPlayingIndex =>
        _currentlyLoadedItem == null ? -1 : PlaylistItems.IndexOf(_currentlyLoadedItem);

    public bool IsBlanked
    {
        get => _isBlanked;
        private set => SetProperty(ref _isBlanked, value);
    }

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
    private bool _isBlanked;

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
        OnPropertyChanged(nameof(NowPlayingIndex));
        OnPropertyChanged(nameof(Volume));
        OnPropertyChanged(nameof(VolumePercentage));
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
        OnPropertyChanged(nameof(NowPlayingIndex));
        OnPropertyChanged(nameof(Volume));
        OnPropertyChanged(nameof(VolumePercentage));
    }

    private void PlaySelected()
    {
        if (SelectedItem == null)
        {
            if (_currentlyLoadedItem != null && _playlistManager.Items.Contains(_currentlyLoadedItem))
            {
                SelectedItem = _currentlyLoadedItem;
            }
            else
            {
                var firstPlayable = PlaylistItems.FirstOrDefault(item => File.Exists(item.FullPath));
                if (firstPlayable == null)
                    return;
                SelectedItem = firstPlayable;
            }
        }

        Serilog.Log.Debug(
            "PlaySelected invoked for {FileName} (item volume {Volume:P0})",
            SelectedItem.FileName,
            SelectedItem.Volume);

        if (SelectedItem == _currentlyLoadedItem && !IsPlaying)
        {
            _mediaControlService.SetVolume(SelectedItem.Volume);
            OnPropertyChanged(nameof(Volume));
            OnPropertyChanged(nameof(VolumePercentage));

            if (_mediaControlService.Play())
            {
                CurrentMediaTitle = SelectedItem.FileName;
                IsPlaying = true;
                IsBlanked = false;
            }
            else
            {
                ReconcilePlaybackStopped("Resume failed");
            }
            return;
        }

        if (!File.Exists(SelectedItem.FullPath))
        {
            MessageBox.Show(
                $"Cannot play '{SelectedItem.FileName}'. The file is missing or the drive is disconnected.",
                "File Not Found",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (!_mediaControlService.PlayMedia(SelectedItem.FullPath, SelectedItem.Volume))
            {
                ReconcilePlaybackStopped("PlayMedia failed");
                return;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to play selected item {FileName}", SelectedItem.FileName);
            ReconcilePlaybackStopped("PlayMedia exception");
            return;
        }

        SetNowPlayingItem(SelectedItem);
        CurrentMediaTitle = SelectedItem.FileName;
        IsPlaying = true;
        IsBlanked = false;
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
        IsBlanked = false;
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
        IsPlaying = false;
        IsBlanked = true;
    }

    private void Next()
    {
        PlayAdjacent(1);
    }

    private void Previous()
    {
        PlayAdjacent(-1);
    }

    private void PlayAdjacent(int direction)
    {
        if (PlaylistItems.Count == 0) return;

        int start = -1;
        if (_currentlyLoadedItem != null)
            start = PlaylistItems.IndexOf(_currentlyLoadedItem);
        if (start < 0 && SelectedItem != null)
            start = PlaylistItems.IndexOf(SelectedItem);
        if (start < 0)
            start = direction > 0 ? -1 : PlaylistItems.Count;

        for (int i = start + direction; i >= 0 && i < PlaylistItems.Count; i += direction)
        {
            if (!File.Exists(PlaylistItems[i].FullPath))
                continue;

            SelectedItem = PlaylistItems[i];
            PlaySelected();
            return;
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
