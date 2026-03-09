using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.Wave;

namespace ChurchDisplayApp.Services;

/// <summary>
/// Manages background music playback using NAudio for independent audio control.
/// Uses NAudio instead of LibVLC to avoid Windows per-process audio session
/// volume sharing with the main media player (which uses LibVLC).
/// </summary>
public class BackgroundMusicService : IDisposable
{
    private readonly AppSettings _settings;
    private DispatcherTimer? _pulseTimer;
    private Border? _pulseTarget;
    private bool _isAutoPaused;

    private WaveOutEvent? _waveOut;
    private AudioFileReader? _audioReader;
    public string? LoadedPath { get; private set; }
    private float _volume;
    private bool _shouldLoop = true;

    public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;
    public bool IsMuted { get; private set; }
    public bool IsAutoPaused => _isAutoPaused;

    public BackgroundMusicService(AppSettings settings)
    {
        _settings = settings;
        _volume = (float)Math.Clamp(settings.BackgroundMusicVolume, 0.0, 1.0);
        IsMuted = !_settings.BackgroundMusicEnabled;
    }

    public void Load(string path)
    {
        if (!File.Exists(path))
            return;

        // Stop and dispose any existing playback
        CleanupPlayback();

        LoadedPath = path;

        try
        {
            _audioReader = new AudioFileReader(path);
            _audioReader.Volume = IsMuted ? 0f : _volume;

            _waveOut = new WaveOutEvent();
            _waveOut.Init(_audioReader);

            // Setup looping: when playback stops naturally (end of file), restart
            _waveOut.PlaybackStopped += OnPlaybackStopped;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to load background music: {Path}", path);
            CleanupPlayback();
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_shouldLoop && _audioReader != null && _waveOut != null && e.Exception == null)
        {
            try
            {
                _audioReader.Position = 0;
                _waveOut.Play();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to loop background music");
            }
        }
    }

    public void Play()
    {
        // Ensure state is enabled first so initialization picks up the correct volume
        _settings.BackgroundMusicEnabled = true;
        _settings.Save();
        IsMuted = false;
        _isAutoPaused = false;
        _shouldLoop = true;

        if (_audioReader != null)
        {
            _audioReader.Volume = _volume;
        }

        if (_waveOut != null && _audioReader != null)
        {
            // Only reset to the beginning if we've reached the end
            if (_audioReader.Position >= _audioReader.Length)
            {
                _audioReader.Position = 0;
            }

            _waveOut.Play();
        }
        else if (LoadedPath != null)
        {
            // Try to reload and play
            Load(LoadedPath);
            _waveOut?.Play();
        }
        else
        {
            MessageBox.Show("No background music selected. Please select music first.", "No Music",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public void Pause()
    {
        if (_waveOut?.PlaybackState == PlaybackState.Playing)
        {
            _shouldLoop = false; // Prevent loop restart
            _waveOut.Pause();
        }
        _settings.BackgroundMusicEnabled = false;
        _settings.Save();
    }

    public void Stop()
    {
        if (_waveOut != null)
        {
            _shouldLoop = false; // Prevent loop restart
            _waveOut.Stop();
        }
        _settings.BackgroundMusicEnabled = false;
        _settings.Save();
    }

    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        if (_audioReader != null)
        {
            _audioReader.Volume = IsMuted ? 0f : _volume;
        }
    }

    public void SetVolume(double volume)
    {
        var level = Math.Clamp(volume, 0.0, 1.0);
        _volume = (float)level;

        // NAudio volume is purely software — no system mixer interaction
        if (_audioReader != null && !IsMuted)
        {
            _audioReader.Volume = _volume;
        }

        _settings.BackgroundMusicVolume = level;
        _settings.Save();
    }

    public void AutoPause()
    {
        if (IsPlaying)
        {
            _shouldLoop = false;
            _waveOut?.Pause();
            _isAutoPaused = true;
        }
    }

    public void AutoStop()
    {
        if (_waveOut != null)
        {
            _shouldLoop = false;
            _waveOut.Stop();
            _isAutoPaused = false;
        }
    }

    public void AutoResume()
    {
        if (_isAutoPaused && _settings.BackgroundMusicEnabled)
        {
            _shouldLoop = true;
            _waveOut?.Play();
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

    private void CleanupPlayback()
    {
        if (_waveOut != null)
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Stop();
            _waveOut.Dispose();
            _waveOut = null;
        }

        if (_audioReader != null)
        {
            _audioReader.Dispose();
            _audioReader = null;
        }
    }

    public void Dispose()
    {
        _shouldLoop = false;
        CleanupPlayback();
    }
}
