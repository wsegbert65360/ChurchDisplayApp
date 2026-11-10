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
    private readonly LibVLCSharp.Shared.MediaPlayer? _mediaPlayer;
    private readonly Image _imageDisplay;
    private readonly ProgressBar _progressBar;
    private readonly Grid _contentGrid;
    private readonly DispatcherTimer _timer;
    private bool _isPlaying = false;
    private string? _currentMediaPath;
    private int _targetVolume = 100; // Stored volume to apply when VLC starts playing
    private Media? _currentMedia; // Track current media for proper disposal
    private readonly string[] _snapshotTempPaths;
    private int _snapshotSlot;
    private readonly object _playerLock = new();
    private int _mediaVersion;
    public bool IsDisposed { get; private set; }

    /// <summary>Occurs when a media file finishes playing.</summary>
    public event EventHandler? MediaEnded;

    /// <summary>Occurs when VLC encounters a media playback error.</summary>
    public event EventHandler? MediaError;

    /// <summary>
    /// Gets the current display content as a BitmapSource (for images) or video snapshot (for videos).
    /// Non-blocking async version for use from the UI thread.
    /// </summary>
    public async Task<BitmapSource?> GetCurrentSnapshotAsync()
    {
        // If displaying an image, return the image source (no I/O needed)
        if (_imageDisplay.IsVisible && _imageDisplay.Source is BitmapSource bitmapSource)
        {
            return bitmapSource;
        }

        // If playing video, get a VLC snapshot asynchronously
        if (_videoView.IsVisible)
        {
            return await GetVideoSnapshotAsync();
        }

        return null;
    }

    /// <summary>
    /// Gets a snapshot of the current video frame as a BitmapSource, or null if no video is playing.
    /// Uses async file-wait to avoid blocking the UI thread.
    /// </summary>
    private async Task<BitmapSource?> GetVideoSnapshotAsync()
    {
        var mp = _mediaPlayer;
        if (mp == null || mp.NativeReference == IntPtr.Zero || !mp.IsPlaying)
            return null;

        // Alternate between two files so we never read a file VLC is overwriting for the next frame.
        _snapshotSlot ^= 1;
        var path = _snapshotTempPaths[_snapshotSlot];

        try
        {
            // All file work and the VLC call run off the UI thread.
            return await Task.Run(async () =>
            {
                // Remove the old frame first, so that a file that exists afterwards is known to be the new one.
                try { File.Delete(path); }
                catch (IOException) { return null; } // Still locked from a previous read; skip this frame.

                // Dispose takes the same lock, so the player cannot be freed while VLC is taking the snapshot.
                lock (_playerLock)
                {
                    if (IsDisposed || mp.NativeReference == IntPtr.Zero)
                        return null;

                    // Small explicit size: a 640x360 PNG is ~50-100 KB versus several MB at 1080p.
                    if (!mp.TakeSnapshot(0, path, AppConstants.UI.PreviewSnapshotWidth, AppConstants.UI.PreviewSnapshotHeight))
                        return null;
                }

                // VLC may still be writing the file. Wait (briefly) until it holds a complete PNG.
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    var bytes = TryReadCompletePng(path);
                    if (bytes != null)
                        return DecodePreview(bytes);
                    await Task.Delay(10);
                }

                return null;
            });
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "Video snapshot failed");
            return null;
        }
    }

    /// <summary>
    /// Reads <paramref name="path"/> if it holds a finished PNG (ends with the IEND chunk), otherwise returns null.
    /// </summary>
    private static byte[]? TryReadCompletePng(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            byte[] bytes;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                if (fs.Length < 12)
                    return null;
                bytes = new byte[fs.Length];
                fs.ReadExactly(bytes);
            }

            // A PNG ends with: length(4) "IEND"(4) CRC(4).
            var tail = bytes.AsSpan(bytes.Length - 8, 4);
            return tail.SequenceEqual("IEND"u8) ? bytes : null;
        }
        catch (IOException)
        {
            return null; // still being written
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static BitmapImage DecodePreview(byte[] bytes)
    {
        var bmp = new BitmapImage();
        using (var ms = new MemoryStream(bytes))
        {
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.DecodePixelWidth = AppConstants.UI.PreviewSnapshotWidth;
            bmp.EndInit();
        }
        bmp.Freeze();
        return bmp;
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
        // Unique per process so a second window (after recreate) never fights over the same file.
        var pid = Environment.ProcessId;
        _snapshotTempPaths = new[]
        {
            Path.Combine(Path.GetTempPath(), $"{AppConstants.Media.SnapshotPrefix}{pid}_{GetHashCode()}_a{AppConstants.Media.SnapshotExtension}"),
            Path.Combine(Path.GetTempPath(), $"{AppConstants.Media.SnapshotPrefix}{pid}_{GetHashCode()}_b{AppConstants.Media.SnapshotExtension}")
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
            _mediaPlayer = null;
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

        if (_mediaPlayer != null)
        {
            _mediaPlayer.EndReached += MediaPlayer_EndReached;
            _mediaPlayer.Playing += MediaPlayer_Playing;
            _mediaPlayer.EncounteredError += MediaPlayer_EncounteredError;
        }

        Closing += (s, e) =>
        {
            // Only allow the window to close if it has been explicitly disposed
            // (e.g. when the main app is shutting down). This prevents the operator
            // from accidentally closing the projection display by clicking X.
            if (!IsDisposed)
            {
                e.Cancel = true;
                Hide();  // Hide instead of close — operator can bring it back via Toggle Display
                return;
            }
            Dispose();
        };
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;

        _timer.Stop();
        _timer.Tick -= Timer_Tick;

        if (_mediaPlayer != null)
        {
            _mediaPlayer.EndReached -= MediaPlayer_EndReached;
            _mediaPlayer.Playing -= MediaPlayer_Playing;
            _mediaPlayer.EncounteredError -= MediaPlayer_EncounteredError;
        }

        try { _videoView.MediaPlayer = null; } catch { }

        lock (_playerLock)
        {
            if (_mediaPlayer != null && _mediaPlayer.NativeReference != IntPtr.Zero)
            {
                try { _mediaPlayer.Stop(); } catch { }
                _mediaPlayer.Dispose();
            }
        }
        _currentMedia?.Dispose();
        _currentMedia = null;
        foreach (var path in _snapshotTempPaths)
        {
            try { File.Delete(path); } catch { /* cleaned up at next startup */ }
        }
        GC.SuppressFinalize(this);
    }

    private void MediaPlayer_EndReached(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _isPlaying = false;
            _progressBar.Visibility = Visibility.Collapsed;
            _timer.Stop();
            MediaEnded?.Invoke(this, EventArgs.Empty);
        });
    }

    private void MediaPlayer_Playing(object? sender, EventArgs e)
    {
        Task.Run(async () =>
        {
            try
            {
                if (!IsDisposed && _mediaPlayer != null && _mediaPlayer.NativeReference != IntPtr.Zero)
                    _mediaPlayer.Volume = _targetVolume;

                await Task.Delay(150);
                if (!IsDisposed && _mediaPlayer != null && _mediaPlayer.NativeReference != IntPtr.Zero)
                    _mediaPlayer.Volume = _targetVolume;
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex) { Serilog.Log.Debug(ex, "Delayed volume set failed"); }
        });
    }

    private void MediaPlayer_EncounteredError(object? sender, EventArgs e)
    {
        Serilog.Log.Error("MediaPlayer encountered an error playback of {Path}", _currentMediaPath);
        Dispatcher.BeginInvoke(() =>
        {
            _isPlaying = false;
            _timer.Stop();
            _progressBar.Visibility = Visibility.Collapsed;
            MediaError?.Invoke(this, EventArgs.Empty);
        });
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
    /// <returns>
    /// False when the media could not be started; the window is then cleared. Failures discovered later
    /// (for example an image that turns out to be corrupt) are reported through <see cref="MediaError"/>,
    /// which is always raised after this method has returned.
    /// </returns>
    public bool ShowMedia(string filePath)
    {
        _isPlaying = false;
        _timer.Stop();

        // Failures detectable right now are returned, not raised as MediaError: raising the event
        // synchronously would reach the ViewModel before it has marked the item as playing.
        if (string.IsNullOrWhiteSpace(filePath) || !MediaConstants.IsSupported(filePath) || !File.Exists(filePath))
        {
            Stop();
            return false;
        }

        _currentMediaPath = filePath;
        var mediaVersion = ++_mediaVersion;

        if (MediaConstants.IsImage(filePath))
        {
            _ = ShowImageAsync(filePath, mediaVersion);
            return true;
        }

        if (PlayVideo(filePath))
            return true;

        Stop();
        return false;
    }

    private async Task ShowImageAsync(string imagePath, int mediaVersion)
    {
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

            // Release the previous image source to free memory before loading a new one
            _imageDisplay.Source = null;

            // Prevent UI freeze from disk I/O by reading the file bytes asynchronously
            byte[] fileBytes = await File.ReadAllBytesAsync(imagePath);

            // Race-condition guard: if the operator loaded a different media item
            // while we were suspended at the await, discard this stale result.
            if (_currentMediaPath != imagePath || _mediaVersion != mediaVersion) return;

            // Decode at the projector's real pixel width (sharp on 4K) but cap it so huge photos cannot exhaust memory.
            var decodeWidth = GetImageDecodeWidth();
            var bitmap = await Task.Run(() =>
            {
                var bmp = new BitmapImage();
                using (var ms = new MemoryStream(fileBytes))
                {
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = decodeWidth;
                    bmp.EndInit();
                }
                bmp.Freeze();  // Required to hand the bitmap back to the UI thread.
                return bmp;
            });

            if (_currentMediaPath != imagePath || _mediaVersion != mediaVersion) return;

            _imageDisplay.Source = bitmap;
            _isPlaying = false;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to load image at {Path}", imagePath);

            // A failure for an image the operator has already moved past must not stop what is showing now.
            if (_mediaVersion != mediaVersion) return;

            Stop();

            // Post the event so it always arrives after ShowMedia's caller has finished updating its state,
            // and drop it if new media was started in the meantime.
            var stoppedVersion = _mediaVersion;
            _ = Dispatcher.BeginInvoke(() =>
            {
                if (_mediaVersion == stoppedVersion)
                    MediaError?.Invoke(this, EventArgs.Empty);
            });
        }
    }

    private int GetImageDecodeWidth()
    {
        const int minWidth = 1920, maxWidth = 7680;
        try
        {
            var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            var pixels = (int)Math.Round(ActualWidth * (scale > 0 ? scale : 1));
            return Math.Clamp(pixels, minWidth, maxWidth);
        }
        catch
        {
            return minWidth;
        }
    }

    private bool PlayVideo(string videoPath)
    {
        if (_libVLC == null || _mediaPlayer == null || _mediaPlayer.NativeReference == IntPtr.Zero)
        {
            Serilog.Log.Error("Video playback is unavailable because the VLC media player failed to initialize.");
            MessageBox.Show("Video playback is unavailable because LibVLC failed to initialize. Please ensure VLC and its dependencies are correctly installed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        _imageDisplay.Visibility = Visibility.Collapsed;
        _filenameLabel.Visibility = Visibility.Collapsed;
        _videoView.Visibility = Visibility.Visible;
        _progressBar.Visibility = Visibility.Visible;
        _progressBar.Value = 0;

        try { _mediaPlayer.Stop(); } catch { }
        _currentMedia?.Dispose();
        _currentMedia = new Media(_libVLC, videoPath, FromType.FromPath);
        _mediaPlayer.Play(_currentMedia);
        _isPlaying = true;
        _timer.Start();
        return true;
    }

    public void Pause()
    {
        // Check _mediaPlayer for null before accessing properties
        var mp = _mediaPlayer;
        if (mp != null && mp.NativeReference != IntPtr.Zero && mp.CanPause)
        {
            mp.Pause();
            _isPlaying = false;
        }
    }

    public bool Resume()
    {
        // Check _mediaPlayer for null before accessing properties
        var mp = _mediaPlayer;
        if (mp != null && mp.NativeReference != IntPtr.Zero)
        {
            RestoreVisibility();
            mp.Play();
            _isPlaying = true;
            _timer.Start();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Stops the currently playing media and clears the display.
    /// </summary>
    public void Stop()
    {
        // Capture reference to avoid race conditions
        var mp = _mediaPlayer;
        if (mp != null && mp.NativeReference != IntPtr.Zero)
        {
            mp.Stop();
        }
        _isPlaying = false;
        _mediaVersion++;
        _currentMediaPath = null;
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
        if (_mediaPlayer != null && _mediaPlayer.NativeReference != IntPtr.Zero
            && _mediaPlayer.CanPause && _mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Pause();
        }
        
        _isPlaying = false;
        _mediaVersion++;
        _timer.Stop();

        _videoView.Visibility = Visibility.Collapsed;
        _imageDisplay.Visibility = Visibility.Collapsed;
        _filenameLabel.Visibility = Visibility.Collapsed;
        _progressBar.Visibility = Visibility.Collapsed;
    }

    public void Seek(double position)
    {
        if (_mediaPlayer != null && _mediaPlayer.NativeReference != IntPtr.Zero
            && _mediaPlayer.IsSeekable && _mediaPlayer.Length > 0)
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

    public bool Play()
    {
        if (_currentMediaPath == null)
            return false;

        if (MediaConstants.IsImage(_currentMediaPath))
        {
            // Image not loaded (yet, or it was released): load it again.
            if (_imageDisplay.Source == null)
                return ShowMedia(_currentMediaPath);

            RestoreVisibility();
            _isPlaying = false;
            return true;
        }

        if (_mediaPlayer != null && _mediaPlayer.NativeReference != IntPtr.Zero)
        {
            return Resume();
        }

        return ShowMedia(_currentMediaPath);
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

    public string? CurrentMediaPath => _currentMediaPath;

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
