using System.IO;
using ChurchDisplayApp.Services;
using ChurchDisplayApp.Models;

namespace ChurchDisplayApp.Services;

/// <summary>
/// Manages media playback control for the main media player.
/// BGM (Background Music) features have been removed as part of the refactor.
/// </summary>
public class MediaControlService
{
    private LiveOutputWindow _liveWindow;
    private readonly AppSettings _settings;

    public event EventHandler? MediaStateChanged;

    public MediaControlService(LiveOutputWindow liveWindow, AppSettings settings)
    {
        _liveWindow = liveWindow ?? throw new ArgumentNullException(nameof(liveWindow));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
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
    /// </summary>
    /// <param name="filePath">The media file to play.</param>
    /// <param name="itemVolume">Per-item volume (0.0 to 1.0). Falls back to MainMediaVolume if not specified.</param>
    public void PlayMedia(string filePath, double? itemVolume = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Media file not found", filePath);

        // Set volume BEFORE starting playback so _targetVolume is correct
        // when VLC's Playing event fires. Use item volume if provided, else global setting.
        var mainVolume = Math.Clamp(itemVolume ?? _settings.MainMediaVolume, 0.0, 1.0);
        _liveWindow.SetVolume(mainVolume);

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
    /// </summary>
    public void Stop()
    {
        _liveWindow.Stop();
        MediaStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Blanks the display (shows black screen).
    /// </summary>
    public void Blank()
    {
        _liveWindow.ShowBlank();
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

    /// <summary>
    /// Toggles the live window fullscreen state.
    /// </summary>
    public void ToggleFullscreen()
    {
        _liveWindow.ToggleFullscreen();
    }

    /// <summary>
    /// Minimizes or restores the live window from fullscreen.
    /// </summary>
    public void SetFullscreen(bool fullscreen)
    {
        _liveWindow.SetFullscreen(fullscreen);
    }
}
