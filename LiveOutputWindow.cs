using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using System.Windows.Threading;

namespace ChurchDisplayApp;

public class ProgressInfo
{
    public double ProgressPercent { get; set; }
    public TimeSpan CurrentTime { get; set; }
    public TimeSpan Duration { get; set; }
}

public class LiveOutputWindow : Window
{
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
    private static readonly string[] MediaExtensions = { ".mp4", ".mov", ".wmv", ".mkv", ".mp3", ".wav", ".flac", ".wma" };
    private readonly TextBlock _filenameLabel;
    private readonly MediaElement _mediaElement;
    private readonly Image _imageDisplay;
    private readonly ProgressBar _progressBar;
    private readonly Grid _contentGrid;
    private readonly DispatcherTimer _timer;
    private bool _isPlaying = false;

    public event EventHandler? MediaEnded;

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

        // MediaElement for video/audio playback
        _mediaElement = new MediaElement
        {
            Stretch = Stretch.UniformToFill,
            StretchDirection = StretchDirection.Both,
            LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Manual
        };
        
        // Add MediaEnded event handler
        _mediaElement.MediaEnded += (s, e) =>
        {
            _isPlaying = false;
            _timer.Stop();
            _progressBar.Value = 0;
            MediaEnded?.Invoke(this, EventArgs.Empty);
        };
        _mediaElement.HorizontalAlignment = HorizontalAlignment.Center;
        _mediaElement.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetRow(_mediaElement, 0);
        mainGrid.Children.Add(_mediaElement);

        // Image display for static images
        _imageDisplay = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(_imageDisplay, 0);
        mainGrid.Children.Add(_imageDisplay);

        // Filename label overlay
        _filenameLabel = new TextBlock
        {
            Text = string.Empty,
            Foreground = Brushes.White,
            FontSize = 48,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(20)
        };
        Grid.SetRow(_filenameLabel, 0);
        mainGrid.Children.Add(_filenameLabel);

        // Progress bar for video/audio
        _progressBar = new ProgressBar
        {
            Height = 8,
            Foreground = Brushes.LimeGreen,
            Background = Brushes.DarkGray,
            Maximum = 100
        };
        Grid.SetRow(_progressBar, 1);
        mainGrid.Children.Add(_progressBar);

        Content = mainGrid;

        // Timer to update progress
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (s, e) => UpdateProgress();
    }

    public void ShowMedia(string filePath)
    {
        if (!File.Exists(filePath))
        {
            ShowText("File not found: " + Path.GetFileName(filePath));
            return;
        }

        string ext = Path.GetExtension(filePath).ToLower();
        ClearMedia();

        if (IsImageFormat(ext))
            DisplayImage(filePath);
        else if (IsMediaFormat(ext))
            PlayMedia(filePath);
        else
            ShowText("Unsupported format: " + Path.GetFileName(filePath));
    }

    private bool IsImageFormat(string ext) => ImageExtensions.Contains(ext);
    private bool IsMediaFormat(string ext) => MediaExtensions.Contains(ext);

    private void ClearMedia()
    {
        _mediaElement.Source = null;
        _mediaElement.Visibility = Visibility.Hidden;
        _imageDisplay.Source = null;
        _imageDisplay.Visibility = Visibility.Hidden;
        _filenameLabel.Text = string.Empty;
    }

    private void DisplayImage(string filePath)
    {
        try
        {
            var bitmap = new System.Windows.Media.Imaging.BitmapImage(new Uri(filePath));
            _imageDisplay.Source = bitmap;
            _mediaElement.Visibility = Visibility.Hidden;
            _imageDisplay.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ShowText("Error loading image: " + ex.Message);
        }
    }

    private void PlayMedia(string filePath)
    {
        try
        {
            _mediaElement.Source = new Uri(filePath);
            _mediaElement.Play();
            _isPlaying = true;
            _timer.Start();
            _imageDisplay.Visibility = Visibility.Hidden;
            _mediaElement.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ShowText("Error playing media: " + ex.Message);
            _isPlaying = false;
        }
    }

    public void ShowText(string text)
    {
        _mediaElement.Source = null;
        _mediaElement.Visibility = Visibility.Hidden;
        _imageDisplay.Source = null;
        _imageDisplay.Visibility = Visibility.Hidden;
        _filenameLabel.Text = text;
    }

    public void ShowBlank()
    {
        _mediaElement.Source = null;
        _mediaElement.Visibility = Visibility.Hidden;
        _imageDisplay.Source = null;
        _imageDisplay.Visibility = Visibility.Hidden;
        _filenameLabel.Text = string.Empty;
    }

    public void Play()
    {
        if (_mediaElement.Source != null)
        {
            _mediaElement.Play();
            _isPlaying = true;
        }
    }

    public void Pause()
    {
        if (_mediaElement.Source != null)
        {
            _mediaElement.Pause();
            _isPlaying = false;
        }
    }

    public void Stop()
    {
        if (_mediaElement.Source != null)
        {
            _mediaElement.Stop();
            _mediaElement.Source = null;
            _timer.Stop();
            _progressBar.Value = 0;
            _isPlaying = false;
        }
    }

    public void SetVolume(double volume)
    {
        _mediaElement.Volume = volume;
    }

    public bool IsPlaying()
    {
        return _isPlaying && _mediaElement.Source != null && _mediaElement.CanPause;
    }

    private void UpdateProgress()
    {
        if (_mediaElement.Source != null && _mediaElement.NaturalDuration.HasTimeSpan && _mediaElement.NaturalDuration.TimeSpan.TotalSeconds > 0)
        {
            double percent = (_mediaElement.Position.TotalSeconds / _mediaElement.NaturalDuration.TimeSpan.TotalSeconds) * 100;
            _progressBar.Value = percent;
        }
    }

    public void Seek(double positionRatio)
    {
        if (_mediaElement.Source != null && _mediaElement.NaturalDuration.HasTimeSpan && _mediaElement.NaturalDuration.TimeSpan.TotalSeconds > 0)
        {
            // Clamp ratio between 0 and 1
            positionRatio = Math.Max(0, Math.Min(1, positionRatio));
            
            TimeSpan targetPosition = TimeSpan.FromSeconds(_mediaElement.NaturalDuration.TimeSpan.TotalSeconds * positionRatio);
            _mediaElement.Position = targetPosition;
        }
    }

    public ProgressInfo GetProgress()
    {
        return new ProgressInfo
        {
            ProgressPercent = (_mediaElement.Source != null && _mediaElement.NaturalDuration.HasTimeSpan && _mediaElement.NaturalDuration.TimeSpan.TotalSeconds > 0)
                ? (_mediaElement.Position.TotalSeconds / _mediaElement.NaturalDuration.TimeSpan.TotalSeconds) * 100
                : 0,
            CurrentTime = _mediaElement.Position,
            Duration = _mediaElement.NaturalDuration.HasTimeSpan ? _mediaElement.NaturalDuration.TimeSpan : TimeSpan.Zero
        };
    }
}
