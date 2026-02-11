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

public class ProgressInfo
{
    public double ProgressPercent { get; set; }
    public TimeSpan CurrentTime { get; set; }
    public TimeSpan Duration { get; set; }
}

public class LiveOutputWindow : Window
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

    public event EventHandler? MediaEnded;

    /// <summary>
    /// Gets a snapshot of the current video frame as a BitmapSource, or null if no video is playing.
    /// </summary>
    public BitmapSource? GetVideoSnapshot()
    {
        if (_mediaPlayer == null || !_mediaPlayer.IsPlaying)
            return null;

        try
        {
            // Create a temporary file path for the snapshot
            var tempPath = Path.Combine(Path.GetTempPath(), $"vlc_snapshot_{Guid.NewGuid()}.png");

            // Take snapshot using VLC's API (this is synchronous)
            if (_mediaPlayer.TakeSnapshot(0, tempPath, 0, 0))
            {
                // Wait a brief moment for VLC to write the file
                // Wait a brief moment or check for file with short timeout
                int retries = 5;
                while (retries > 0 && !File.Exists(tempPath))
                {
                    System.Threading.Thread.Sleep(10);
                    retries--;
                }

                if (File.Exists(tempPath))
                {
                    // Load the image
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(tempPath);
                    bitmap.EndInit();
                    bitmap.Freeze(); // Make it cross-thread accessible

                    // Delete the temporary file
                    try { File.Delete(tempPath); } catch { }

                    return bitmap;
                }
            }
        }
        catch
        {
            // Snapshot failed, return null
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

    public LiveOutputWindow()
    {
        Title = "Live Output";
        Width = 800;
        Height = 450;
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

        // Initialize LibVLC
        Core.Initialize();
        _libVLC = new LibVLC("--quiet", "--no-osd", "--no-video-title-show", "--no-snapshot-preview");
        _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);

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
            Height = 5,
            Foreground = Brushes.LightBlue,
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
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _timer.Tick += Timer_Tick;

        // Subscribe to media player events
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

        Closing += (s, e) =>
        {
            _timer.Stop();
            _mediaPlayer?.Stop();
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
        };
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_mediaPlayer.Length > 0)
        {
            var progress = (double)_mediaPlayer.Time / _mediaPlayer.Length;
            _progressBar.Value = progress * 100;
        }
    }

    public void ShowMedia(string filePath)
    {
        _isPlaying = false;
        _timer.Stop();
        _currentMediaPath = filePath;

        if (MediaConstants.IsImage(filePath))
        {
            ShowImage(filePath);
        }
        else if (MediaConstants.IsSupported(filePath))
        {
            PlayVideo(filePath);
        }
    }

    private void ShowImage(string imagePath)
    {
        _videoView.Visibility = Visibility.Collapsed;
        _imageDisplay.Visibility = Visibility.Visible;
        _filenameLabel.Visibility = Visibility.Collapsed;
        _progressBar.Visibility = Visibility.Collapsed;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(imagePath);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();

        _imageDisplay.Source = bitmap;
        _isPlaying = false;
    }

    private void PlayVideo(string videoPath)
    {
        _imageDisplay.Visibility = Visibility.Collapsed;
        _filenameLabel.Visibility = Visibility.Collapsed;
        _videoView.Visibility = Visibility.Visible;
        _progressBar.Visibility = Visibility.Visible;
        _progressBar.Value = 0;

        var media = new Media(_libVLC, videoPath, FromType.FromPath);
        _mediaPlayer.Play(media);
        _isPlaying = true;
        _timer.Start();
    }

    public void Pause()
    {
        if (_mediaPlayer.CanPause)
        {
            _mediaPlayer.Pause();
            _isPlaying = false;
        }
    }

    public void Resume()
    {
        if (_mediaPlayer.CanPause)
        {
            _mediaPlayer.Play();
            _isPlaying = true;
        }
    }

    public void Stop()
    {
        _mediaPlayer?.Stop();
        _isPlaying = false;
        _timer.Stop();
        _progressBar.Visibility = Visibility.Collapsed;
        
        _videoView.Visibility = Visibility.Collapsed;
        _imageDisplay.Visibility = Visibility.Collapsed;
        _imageDisplay.Source = null;
        _filenameLabel.Visibility = Visibility.Collapsed;
    }

    public void Blank()
    {
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
            ShowMedia(_currentMediaPath);
        }
    }

    public void ShowBlank()
    {
        Blank();
    }

    public void SetVolume(double volume)
    {
        if (_mediaPlayer != null)
        {
            // VLC volume is 0-100
            _mediaPlayer.Volume = (int)(volume * 100);
        }
    }

    public bool IsPlaying => _isPlaying;
}
