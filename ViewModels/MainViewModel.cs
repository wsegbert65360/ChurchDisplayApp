using System.Collections.ObjectModel;
using System.Windows.Input;
using ChurchDisplayApp.Services;
using ChurchDisplayApp.Models;

namespace ChurchDisplayApp.ViewModels;

/// <summary>
/// The main ViewModel for the application, managing the playlist, media playback, and background music.
/// </summary>
public class MainViewModel : BaseViewModel
{
    private readonly PlaylistManager _playlistManager;
    private readonly MediaControlService _mediaControlService;
    private readonly AppSettings _settings;
    private readonly BackgroundMusicService? _backgroundMusicService;
    private double _volume = 0.5;
    private double _progress = 0;
    private string _currentMediaTitle = "No Media Playing";
    private string _currentTimeStr = "00:00";
    private string _durationStr = "00:00";
    private bool _isPlaying = false;
    private bool _isScrubbing = false;
    private PlaylistItem? _selectedItem;
    private double _playlistFontSize = 13;
    private string _introBgmName = "None Selected";
    private string _kidsBgmName = "None Selected";

    public MainViewModel(PlaylistManager playlistManager, MediaControlService mediaControlService, AppSettings settings, BackgroundMusicService? backgroundMusicService = null)
    {
        _playlistManager = playlistManager ?? throw new ArgumentNullException(nameof(playlistManager));
        _mediaControlService = mediaControlService ?? throw new ArgumentNullException(nameof(mediaControlService));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _backgroundMusicService = backgroundMusicService;
        
        _volume = _settings.MainMediaVolume;
        _playlistFontSize = _settings.PlaylistFontSize;
        // Explicitly apply saved volume — field assignment above doesn't trigger the setter
        _mediaControlService.SetVolume(_volume);

        // Commands
        PlayCommand = new RelayCommand(_ => PlaySelected());
        StopCommand = new RelayCommand(_ => Stop());
        PauseCommand = new RelayCommand(_ => Pause());
        BlankCommand = new RelayCommand(_ => Blank());
        NextCommand = new RelayCommand(_ => Next());
        PreviousCommand = new RelayCommand(_ => Previous());
        ToggleFullscreenCommand = new RelayCommand(_ => ToggleFullscreen());
        ExitFullscreenCommand = new RelayCommand(_ => ExitFullscreen());
        
        // Listen to playlist changes to update background hint
        _playlistManager.Items.CollectionChanged += (s, e) => OnPropertyChanged(nameof(PlaylistBackground));

        UpdateBgmNames();
    }

    public ObservableCollection<PlaylistItem> PlaylistItems => _playlistManager.Items;

    public PlaylistItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public double Volume
    {
        get => _volume;
        set
        {
            if (SetProperty(ref _volume, value))
            {
                _mediaControlService.SetVolume(value);
                _settings.MainMediaVolume = value;
                _settings.Save();
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

    public System.Windows.Media.Brush MediaControlsBackground => IsPlaying 
        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(135, 206, 250)) 
        : System.Windows.Media.Brushes.Transparent;

    public System.Windows.Media.Brush PlaylistBackground => _playlistManager.Items.Count == 0
        ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 173, 216, 230))
        : System.Windows.Media.Brushes.White;

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

    public string IntroBGMName
    {
        get => _introBgmName;
        set => SetProperty(ref _introBgmName, value);
    }

    public string KidsBGMName
    {
        get => _kidsBgmName;
        set => SetProperty(ref _kidsBgmName, value);
    }

    // Commands
    public ICommand PlayCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand BlankCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand ToggleFullscreenCommand { get; }
    public ICommand ExitFullscreenCommand { get; }

    private string? _currentlyLoadedPath;

    private void PlaySelected()
    {
        if (SelectedItem != null)
        {
            // If already loaded and just paused, simply resume
            if (SelectedItem.FullPath == _currentlyLoadedPath && !IsPlaying)
            {
                _mediaControlService.Play();
                IsPlaying = true;
                return;
            }

            if (!System.IO.File.Exists(SelectedItem.FullPath))
            {
                _playlistManager.RemoveItem(SelectedItem);
                return;
            }

            _backgroundMusicService?.Stop();
            _backgroundMusicService?.StopPulseAnimation();

            _mediaControlService.PlayMedia(SelectedItem.FullPath);
            _currentlyLoadedPath = SelectedItem.FullPath;
            CurrentMediaTitle = SelectedItem.FileName;
            IsPlaying = true;
        }
    }

    private void Stop()
    {
        _mediaControlService.Stop();
        IsPlaying = false;
        _currentlyLoadedPath = null;
        CurrentMediaTitle = "None";
    }

    private void Pause()
    {
        _mediaControlService.Pause();
        IsPlaying = false;
    }

    private void Blank()
    {
        _mediaControlService.Blank();
        CurrentMediaTitle = "None";
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

            // If something is loaded, try to get progress
            if (_currentlyLoadedPath != null)
            {
                // If not playing, don't poll VLC constantly as it can be jittery or stale
                // We'll rely on the position set by the user during seek while paused
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
                // Truly nothing loaded, reset
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
        return $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}";
    }

    public void UpdateBgmNames()
    {
        IntroBGMName = !string.IsNullOrEmpty(_settings.BackgroundMusicPath) 
            ? System.IO.Path.GetFileName(_settings.BackgroundMusicPath) 
            : "None Selected";
            
        KidsBGMName = !string.IsNullOrEmpty(_settings.BackgroundMusicChildSermonPath) 
            ? System.IO.Path.GetFileName(_settings.BackgroundMusicChildSermonPath) 
            : "None Selected";
    }
}
