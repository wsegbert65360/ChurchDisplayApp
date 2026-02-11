using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ChurchDisplayApp.Services;

/// <summary>
/// Manages background music playback, volume control, and visual pulse animations.
/// </summary>
public class BackgroundMusicService
{
    private readonly MediaElement _mediaPlayer;
    private readonly AppSettings _settings;
    private DispatcherTimer? _pulseTimer;
    private Border? _pulseTarget;
    private bool _isAutoPaused;

    public bool IsPlaying => _mediaPlayer.Source != null && _mediaPlayer.CanPause;
    public bool IsMuted { get; private set; }
    public bool IsAutoPaused => _isAutoPaused;

    public BackgroundMusicService(MediaElement mediaPlayer, AppSettings settings)
    {
        _mediaPlayer = mediaPlayer;
        _settings = settings;
    }

    public void Play()
    {
        if (_mediaPlayer.Source != null)
        {
            _mediaPlayer.Play();
            _settings.BackgroundMusicEnabled = true;
            _settings.Save();
            _mediaPlayer.IsMuted = false;
            IsMuted = false;
            _isAutoPaused = false;
        }
        else
        {
            MessageBox.Show("No background music selected. Please select music first.", "No Music",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public void Pause()
    {
        _mediaPlayer.Pause();
        _settings.BackgroundMusicEnabled = false;
        _settings.Save();
    }

    public void Stop()
    {
        _mediaPlayer.Stop();
        _settings.BackgroundMusicEnabled = false;
        _settings.Save();
    }

    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        _mediaPlayer.IsMuted = IsMuted;
    }

    public void SetVolume(double volume)
    {
        _mediaPlayer.Volume = volume;
        _settings.BackgroundMusicVolume = volume;
        _settings.Save();
    }

    public void AutoPause()
    {
        if (IsPlaying)
        {
            _mediaPlayer.Pause();
            _isAutoPaused = true;
        }
    }

    public void AutoResume()
    {
        if (_isAutoPaused && _settings.BackgroundMusicEnabled)
        {
            _mediaPlayer.Play();
            _isAutoPaused = false;
        }
    }

    public void StartPulseAnimation(Border targetBorder)
    {
        _pulseTarget = targetBorder;
        StopPulseAnimation(); // Ensure no existing timer

        _pulseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50) // Update every 50ms for smooth fade
        };

        double fadeDirection = 1; // 1 = fading in, -1 = fading out
        double currentOpacity = 0.3; // Start at 30% opacity

        _pulseTimer.Tick += (s, e) =>
        {
            // Update opacity
            currentOpacity += fadeDirection * 0.02; // Change by 2% each tick

            // Reverse direction at limits
            if (currentOpacity >= 1.0)
            {
                currentOpacity = 1.0;
                fadeDirection = -1;
            }
            else if (currentOpacity <= 0.3)
            {
                currentOpacity = 0.3;
                fadeDirection = 1;
            }

            // Apply the opacity with light blue color
            var color = Color.FromRgb(173, 216, 230); // Light blue
            var brush = new SolidColorBrush(color);
            brush.Opacity = currentOpacity;
            
            if (_pulseTarget != null)
                _pulseTarget.Background = brush;
        };

        _pulseTimer.Start();
    }

    public void StopPulseAnimation()
    {
        if (_pulseTimer != null)
        {
            _pulseTimer.Stop();
            _pulseTimer = null;
        }

        // Clear the background completely
        if (_pulseTarget != null)
        {
            _pulseTarget.Background = null;
            _pulseTarget.InvalidateVisual(); // Force UI refresh
        }
    }
}
