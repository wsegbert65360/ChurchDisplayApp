using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ChurchDisplayApp.Services;

/// <summary>
/// Manages media playback control including main media and background music.
/// </summary>
public class MediaControlService
{
    private LiveOutputWindow _liveWindow;
    private readonly AppSettings _settings;
    private readonly BackgroundMusicService? _bgmService;
    private bool _mainMediaAutoPausedBgm = false;

    public event EventHandler? MediaStateChanged;

    public MediaControlService(LiveOutputWindow liveWindow, AppSettings settings, BackgroundMusicService? bgmService = null)
    {
        _liveWindow = liveWindow ?? throw new ArgumentNullException(nameof(liveWindow));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _bgmService = bgmService;
    }

    /// <summary>
    /// Updates the live window reference used by the service.
    /// </summary>
    public void UpdateLiveWindow(LiveOutputWindow newWindow)
    {
        _liveWindow = newWindow ?? throw new ArgumentNullException(nameof(newWindow));
    }

    /// <summary>
    /// Plays the specified media file on the live output window.
    /// Automatically pauses background music if enabled.
    /// </summary>
    public void PlayMedia(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Media file not found", filePath);

        // Auto-pause background music when playing media
        if (_bgmService != null && _bgmService.IsPlaying)
        {
            _bgmService.AutoPause();
            _mainMediaAutoPausedBgm = true;
        }

        _liveWindow.ShowMedia(filePath);
        MediaStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Plays the current media.
    /// </summary>
    public void Play()
    {
        _liveWindow.Play();
        MediaStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Pauses the current media.
    /// </summary>
    public void Pause()
    {
        _liveWindow.Pause();
        MediaStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Stops the current media and clears the display.
    /// Resumes background music if it was auto-paused.
    /// </summary>
    public void Stop()
    {
        _liveWindow.Stop();
        
        // Resume background music if it was auto-paused
        if (_mainMediaAutoPausedBgm && _bgmService != null)
        {
            _bgmService.AutoResume();
            _mainMediaAutoPausedBgm = false;
        }
        
        MediaStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Blanks the display (shows black screen).
    /// </summary>
    public void Blank()
    {
        _liveWindow.ShowBlank();
        
        // Resume background music if it was auto-paused
        if (_mainMediaAutoPausedBgm && _bgmService != null)
        {
            _bgmService.AutoResume();
            _mainMediaAutoPausedBgm = false;
        }
        
        MediaStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Seeks to a specific position in the media (0.0 to 1.0).
    /// </summary>
    public void Seek(double positionRatio)
    {
        _liveWindow.Seek(positionRatio);
    }

    /// <summary>
    /// Sets the volume for the main media (0.0 to 1.0).
    /// </summary>
    public void SetVolume(double volume)
    {
        var clampedVolume = Math.Clamp(volume, 0.0, 1.0);
        _liveWindow.SetVolume(clampedVolume);
    }

    /// <summary>
    /// Gets the current playback progress information.
    /// </summary>
    public ProgressInfo? GetProgress()
    {
        return _liveWindow?.GetProgress();
    }

    /// <summary>
    /// Checks if media is currently playing.
    /// </summary>
    public bool IsPlaying()
    {
        return _liveWindow.IsPlaying;
    }
}
