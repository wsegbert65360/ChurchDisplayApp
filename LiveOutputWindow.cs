using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using ChurchDisplayApp.Models;

namespace ChurchDisplayApp;



/// <summary>
/// The primary display window that projects media (video, images, text) to the target monitor.
/// Uses LibVLC for high-performance media playback.
/// </summary>
public class LiveOutputWindow : Window, IDisposable
{
    private readonly TextBlock _filenameLabel;
    private readonly VideoView _videoView;
    private readonly LibVLC _libVLC;
    private readonly LibVLCSharp.Shared.MediaPlayer _mediaPlayer;
    private readonly Image _imageDisplay;
    private readonly ProgressBar _progressBar;
    private readonly Grid _contentGrid;
    private readonly DispatcherTimer _timer;
    private bool _isPlaying = false;
    private string? _currentMediaPath;
    private int _targetVolume = 100; // Stored volume to apply when VLC starts playing
    private Media? _currentMedia; // Track current media for proper disposal
    public bool IsDisposed { get; private set; }

    /// <summary>Occurs when a media file finishes playing.</summary>
    public event EventHandler? MediaEnded;

    /// <summary>
    /// Gets a snapshot of the current video frame as a BitmapSource, or null if no video is playing.
    /// </summary>
    public BitmapSource? GetVideoSnapshot()
    {
        if (_mediaPlayer == null || _mediaPlayer.NativeReference == IntPtr.Zero || !_mediaPlayer.IsPlaying)
            return null;

        string? tempPath = null;
        try
        {
            tempPath = Path.Combine(Path.GetTempPath(), $"{AppConstants.Media.SnapshotPrefix}{Guid.NewGuid()}{AppConstants.Media.SnapshotExtension}");

            if (_mediaPlayer.TakeSnapshot(0, tempPath, 0, 0))
            {
                int retries = 5;
                while (retries > 0 && !File.Exists(tempPath))
                {
                    System.Threading.Thread.Sleep(10);
                    retries--;
                }

                if (File.Exists(tempPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(tempPath);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Video snapshot failed");
        }
        finally
        {
            if (tempPath != null)
            {
                try { File.Delete(tempPath); } catch { }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the current display content as a BitmapSource (for images) or video snapshot (for videos).
    /// </summary>
    public BitmapSource? GetCurrentSnapshot()
    {
        // If displaying an image, return the image source
        if (_imageDisplay.IsVisible && _imageDisplay.Source is BitmapSource bitmapSource)
        {
            return bitmapSource;
        }

        // If playing video, get a VLC snapshot
        if (_videoView.IsVisible)
        {
            return GetVideoSnapshot();
        }

        return null;
    }

    public LiveOutputWindow(LibVLC libVLC)
    {
        _libVLC = libVLC ?? throw new ArgumentNullException(nameof(libVLC));
        
        Title = "Live Output";
        Width = AppConstants.UI.DefaultLiveWindowWidth;
        Height = AppConstants.UI.DefaultLiveWindowHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.Black;
        WindowStyle = WindowStyle.None;  // Remove title bar and borders
        ResizeMode = ResizeMode.NoResize;  // Prevent resizing

        _contentGrid = new Grid
        {
            Background = Brushes.Black
        };
        
        // Create outer grid with rows for content and progress bar
        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Initialize MediaPlayer
        try
        {
            _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to initialize MediaPlayer.");
            _mediaPlayer = null!;
        }

        // Create VideoView for media playback
        _videoView = new VideoView
        {
            MediaPlayer = _mediaPlayer,
            Visibility = Visibility.Collapsed
        };

        // Create Image for static images
        _imageDisplay = new Image
        {
            Stretch = Stretch.Uniform,
            Visibility = Visibility.Collapsed
        };

        // Create a filename label
        _filenameLabel = new TextBlock
        {
            Text = "",
            Foreground = Brushes.White,
            FontSize = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(20)
        };

        // Add elements to content grid in z-order
        _contentGrid.Children.Add(_videoView);
        _contentGrid.Children.Add(_imageDisplay);
        _contentGrid.Children.Add(_filenameLabel);

        // Progress bar
        _progressBar = new ProgressBar
        {
            Height = AppConstants.UI.ProgressBarHeight,
            Foreground = new SolidColorBrush(AppConstants.Colors.PulseLightBlue),
            Background = Brushes.DarkGray,
            Visibility = Visibility.Collapsed
        };

        // Add to main grid
        Grid.SetRow(_contentGrid, 0);
        Grid.SetRow(_progressBar, 1);
        mainGrid.Children.Add(_contentGrid);
        mainGrid.Children.Add(_progressBar);

        Content = mainGrid;

        // Set up timer for progress updates
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(AppConstants.UI.LiveWindowTimerIntervalMs)
        };
        _timer.Tick += Timer_Tick;

        // Subscribe to media player events
        if (_mediaPlayer != null)
        {
            _mediaPlayer.EndReached += (s, e) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    _isPlaying = false;
                    _progressBar.Visibility = Visibility.Collapsed;
                    _timer.Stop();
                    MediaEnded?.Invoke(this, EventArgs.Empty);
                });
            };

            // When VLC starts playing and audio output is ready,
            // re-apply the stored volume for reliable audio initialization
            _mediaPlayer.Playing += (s, e) =>
            {
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        // Set immediately (VLC thread-safe)
                        if (!IsDisposed && _mediaPlayer != null && _mediaPlayer.NativeReference != IntPtr.Zero)
                            _mediaPlayer.Volume = _targetVolume;
                        
                        // Re-apply after audio output initializes
                        await System.Threading.Tasks.Task.Delay(150);
                        if (!IsDisposed && _mediaPlayer != null && _mediaPlayer.NativeReference != IntPtr.Zero)
                            _mediaPlayer.Volume = _targetVolume;
                    }
                    catch (ObjectDisposedException) { /* Window closed during delay */ }
                    catch (Exception ex) { Serilog.Log.Debug(ex, "Delayed volume set failed"); }
                });
            };

            _mediaPlayer.EncounteredError += (s, e) =>
            {
                Serilog.Log.Error("MediaPlayer encountered an error playback of {Path}", _currentMediaPath);
                Dispatcher.BeginInvoke(() =>
                {
                    _isPlaying = false;
                    _timer.Stop();
                    _progressBar.Visibility = Visibility.Collapsed;
                });
            };
        }

        Closing += (s, e) =>
        {
            Dispose();
        };
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;

        _timer?.Stop();
        if (_mediaPlayer != null && _mediaPlayer.NativeReference != IntPtr.Zero)
        {
            _mediaPlayer.Stop();
            _mediaPlayer.Dispose();
        }
        _currentMedia?.Dispose();
        _currentMedia = null;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_mediaPlayer != null && _mediaPlayer.NativeReference != IntPtr.Zero && _mediaPlayer.Length > 0)
        {
            var progress = (double)_mediaPlayer.Time / _mediaPlayer.Length;
            _progressBar.Value = progress * 100;
        }
    }

    /// <summary>
    /// Displays the specified media file. Detects if it is an image or video based on file extension.
    /// </summary>
    /// <param name="filePath">The absolute path to the media file.</param>
    public void ShowMedia(string filePath)
    {
        _isPlaying = false;
        _timer.Stop();
        _currentMediaPath = filePath;

        if (MediaConstants.IsImage(filePath))
        {
            _ = ShowImageAsync(filePath);
        }
        else if (MediaConstants.IsSupported(filePath))
        {
            PlayVideo(filePath);
        }
    }

    private async Task ShowImageAsync(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            Stop();
            return;
        }

        try
        {
            // Ensure any currently playing video/audio is stopped
            if (_mediaPlayer != null && _mediaPlayer.NativeReference != IntPtr.Zero)
            {
                _mediaPlayer.Stop();
            }

            _videoView.Visibility = Visibility.Collapsed;
            _imageDisplay.Visibility = Visibility.Visible;
            _filenameLabel.Visibility = Visibility.Collapsed;
            _progressBar.Visibility = Visibility.Collapsed;

            // Decode image on background thread for large files (Issue #13 and #4)
            var bitmap = await Task.Run(() =>
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(imagePath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 1920;  // Limit decode size to prevent OOM
                bmp.EndInit();
                
                if (bmp.CanFreeze)
                    bmp.Freeze();
                    
                return bmp;
            });

            _imageDisplay.Source = bitmap;
            _isPlaying = false;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to load image at {Path}", imagePath);
            Stop();
        }
    }

    private void PlayVideo(string videoPath)
    {
        if (_libVLC == null || _mediaPlayer == null || _mediaPlayer.NativeReference == IntPtr.Zero)
        {
            MessageBox.Show("Video playback is unavailable because LibVLC failed to initialize. Please ensure VLC and its dependencies are correctly installed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _imageDisplay.Visibility = Visibility.Collapsed;
        _filenameLabel.Visibility = Visibility.Collapsed;
        _videoView.Visibility = Visibility.Visible;
        _progressBar.Visibility = Visibility.Visible;
        _progressBar.Value = 0;

        _currentMedia?.Dispose();
        _currentMedia = new Media(_libVLC, videoPath, FromType.FromPath);
        _mediaPlayer.Play(_currentMedia);
        _isPlaying = true;
        _timer.Start();
    }

    public void Pause()
    {
        if (_mediaPlayer != null && _mediaPlayer.NativeReference != IntPtr.Zero && _mediaPlayer.CanPause)
        {
            _mediaPlayer.Pause();
            _isPlaying = false;
        }
    }

    public void Resume()
    {
        if (_mediaPlayer != null && _mediaPlayer.NativeReference != IntPtr.Zero)
        {
            RestoreVisibility();
            _mediaPlayer.Play();
            _isPlaying = true;
            _timer.Start();
        }
    }

    /// <summary>
    /// Stops the currently playing media and clears the display.
    /// </summary>
    public void Stop()
    {
        if (_mediaPlayer != null && _mediaPlayer.NativeReference != IntPtr.Zero)
        {
            _mediaPlayer.Stop();
        }
        _isPlaying = false;
        _timer.Stop();
        _progressBar.Visibility = Visibility.Collapsed;
        
        _videoView.Visibility = Visibility.Collapsed;
        _imageDisplay.Visibility = Visibility.Collapsed;
        _imageDisplay.Source = null;
        _filenameLabel.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Blanks the display by hiding all content elements and pausing media if playing.
    /// </summary>
    public void Blank()
    {
        if (_mediaPlayer != null && _mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Pause();
        }
        
        _isPlaying = false;
        _timer.Stop();

        _videoView.Visibility = Visibility.Collapsed;
        _imageDisplay.Visibility = Visibility.Collapsed;
        _filenameLabel.Visibility = Visibility.Collapsed;
        _progressBar.Visibility = Visibility.Collapsed;
    }

    public void Seek(double position)
    {
        if (_mediaPlayer != null && _mediaPlayer.IsSeekable && _mediaPlayer.Length > 0)
        {
            try
            {
                _mediaPlayer.Time = (long)(position * _mediaPlayer.Length);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error seeking in LiveOutputWindow");
            }
        }
    }

    public ProgressInfo? GetProgress()
    {
        if (_mediaPlayer == null || _mediaPlayer.NativeReference == IntPtr.Zero)
            return null;

        try
        {
            var length = _mediaPlayer.Length;
            var time = _mediaPlayer.Time;

            if (length <= 0) return null;

            var totalSeconds = length / 1000.0;
            var currentSeconds = time / 1000.0;

            return new ProgressInfo
            {
                ProgressPercent = totalSeconds > 0 ? (currentSeconds / totalSeconds) * 100 : 0,
                CurrentTime = TimeSpan.FromSeconds(currentSeconds),
                Duration = TimeSpan.FromSeconds(totalSeconds)
            };
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error getting progress in LiveOutputWindow");
            return null;
        }
    }

    public void Play()
    {
        if (_currentMediaPath != null)
        {
            if (_mediaPlayer != null && _mediaPlayer.NativeReference != IntPtr.Zero)
            {
                Resume();
            }
            else
            {
                ShowMedia(_currentMediaPath);
            }
        }
    }

    private void RestoreVisibility()
    {
        if (_currentMediaPath == null) return;

        if (MediaConstants.IsImage(_currentMediaPath))
        {
            _imageDisplay.Visibility = Visibility.Visible;
            _videoView.Visibility = Visibility.Collapsed;
            _progressBar.Visibility = Visibility.Collapsed;
        }
        else
        {
            _imageDisplay.Visibility = Visibility.Collapsed;
            _videoView.Visibility = Visibility.Visible;
            _progressBar.Visibility = Visibility.Visible;
        }
    }

    public void ShowBlank()
    {
        Blank();
    }

    public void SetVolume(double volume)
    {
        _targetVolume = (int)(Math.Clamp(volume, 0.0, 1.0) * 100);
        if (_mediaPlayer != null && _mediaPlayer.NativeReference != IntPtr.Zero)
        {
            _mediaPlayer.Volume = _targetVolume;
        }
    }

    /// <summary>Gets a value indicating whether media is currently playing.</summary>
    public bool IsPlaying => _isPlaying;

    /// <summary>
    /// Toggles the window between maximized and normal state.
    /// </summary>
    public void ToggleFullscreen()
    {
        WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
    }

    /// <summary>
    /// Sets the window to maximized or normal state.
    /// </summary>
    public void SetFullscreen(bool fullscreen)
    {
        WindowState = fullscreen ? WindowState.Maximized : WindowState.Normal;
    }
}
