using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Input;

namespace ChurchDisplayApp;

// Class to hold playlist item information
public class PlaylistItem
{
    public string FullPath { get; set; }
    public string FileName { get; set; }
    public string Extension { get; set; }

    public PlaylistItem(string fullPath)
    {
        FullPath = fullPath;
        FileName = System.IO.Path.GetFileName(fullPath);
        Extension = System.IO.Path.GetExtension(fullPath).ToLower();
    }

    public override string ToString()
    {
        return FileName;
    }
}

public partial class MainWindow : Window
{
    private static AppSettings _settings = AppSettings.Load();
    private readonly MediaElement _backgroundMusicPlayer;
    private readonly DispatcherTimer _livePreviewTimer;
    private Point _dragStartPoint;
    // P/Invoke for monitor detection
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    private static List<(Rect bounds, bool isPrimary)> _monitorBounds = new();

    private static bool MonitorEnumCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData)
    {
        MonitorInfo mi = new() { cbSize = (uint)Marshal.SizeOf(typeof(MonitorInfo)) };
        GetMonitorInfo(hMonitor, ref mi);
        const uint MONITORINFOF_PRIMARY = 1;
        bool isPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;
        _monitorBounds.Add((mi.rcWork, isPrimary));
        return true;
    }

    private static List<(Rect bounds, bool isPrimary)> GetMonitorBounds()
    {
        _monitorBounds.Clear();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnumCallback, IntPtr.Zero);
        return _monitorBounds;
    }

    private LiveOutputWindow? _liveWindow;
    private bool _isDraggingProgressBar = false;
    private System.Windows.Threading.DispatcherTimer? _progressUpdateTimer;
    private bool _backgroundMusicAutoPaused = false;
    private System.Windows.Threading.DispatcherTimer? _pulseTimer;

    public MainWindow()
    {
        InitializeComponent();
        
        // Initialize background music player
        _backgroundMusicPlayer = new MediaElement
        {
            LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Manual,
            Volume = _settings.BackgroundMusicVolume,
            IsMuted = !_settings.BackgroundMusicEnabled
        };
        
        // Set up background music looping
        _backgroundMusicPlayer.MediaEnded += (s, e) =>
        {
            _backgroundMusicPlayer.Position = TimeSpan.Zero;
            _backgroundMusicPlayer.Play();
        };
        
        // Load background music if configured
        if (!string.IsNullOrEmpty(_settings.BackgroundMusicPath) && File.Exists(_settings.BackgroundMusicPath))
        {
            _backgroundMusicPlayer.Source = new Uri(_settings.BackgroundMusicPath);
            if (_settings.BackgroundMusicEnabled)
            {
                _backgroundMusicPlayer.Play();
                StartBackgroundMusicPulse();
            }
        }
        
        // Set up background music volume slider
        BackgroundMusicVolumeSlider.Value = _settings.BackgroundMusicVolume;
        BackgroundMusicVolumeSlider.ValueChanged += BackgroundMusicVolume_ValueChanged;
        
        // Set up live preview timer
        _livePreviewTimer = new DispatcherTimer 
        { 
            Interval = TimeSpan.FromMilliseconds(100) 
        };
        _livePreviewTimer.Tick += UpdateLivePreview;
        _livePreviewTimer.Start();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        // Create and show the live output window after MainWindow is shown
        _liveWindow = new LiveOutputWindow
        {
            Owner = this
        };
        
        // Subscribe to MediaEnded event to stop pulsing when media finishes
        _liveWindow.MediaEnded += (s, e) => StopMediaPulse();

        // Detect monitors
        var monitors = GetMonitorBounds();
        
        if (monitors.Count >= 2)
        {
            // Ask user which monitor to use
            var result = MessageBox.Show(
                $"Multiple monitors detected ({monitors.Count} total).\n\nWould you like to use the rightmost monitor (likely your projector/display) for the live output?\n\nClick YES for rightmost, NO for other monitor.",
                "Select Display",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            var liveMonitor = result == MessageBoxResult.Yes
                ? monitors.OrderByDescending(m => m.bounds.right).First()  // Rightmost
                : monitors.OrderBy(m => m.bounds.right).First();  // Leftmost

            var bounds = liveMonitor.bounds;
            // Show window first at default location, then reposition it
            _liveWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            _liveWindow.Show();
            
            // Now position it on the selected monitor AFTER showing
            _liveWindow.Left = bounds.left;
            _liveWindow.Top = bounds.top;
            _liveWindow.Width = bounds.right - bounds.left;
            _liveWindow.Height = bounds.bottom - bounds.top;
            _liveWindow.WindowState = WindowState.Maximized;  // Maximize to fill the monitor
            _liveWindow.Topmost = true;
        }
        else
        {
            _liveWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            _liveWindow.Show();
        }

        _liveWindow.ShowBlank();

        // Setup progress update timer
        _progressUpdateTimer = new System.Windows.Threading.DispatcherTimer 
        { 
            Interval = TimeSpan.FromMilliseconds(200) 
        };
        _progressUpdateTimer.Tick += (s, args) => UpdateProgressDisplay();
        _progressUpdateTimer.Start();
        
        // Check if first-time setup is needed for background music
        if (string.IsNullOrEmpty(_settings.BackgroundMusicPath))
        {
            var result = MessageBox.Show(
                "Would you like to set up background music for pre-service?\n\nYou can change this anytime later.",
                "Setup Background Music",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
                
            if (result == MessageBoxResult.Yes)
            {
                SelectBackgroundMusic_Click(this, new RoutedEventArgs());
            }
        }
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Media Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.mp4;*.mov;*.wmv;*.mkv;*.mp3;*.wav;*.flac;*.wma|All Files|*.*",
            Multiselect = true
        };

        if (dlg.ShowDialog() == true)
        {
            foreach (var file in dlg.FileNames)
            {
                PlaylistListBox.Items.Add(new PlaylistItem(file));
            }
        }
    }

    private bool ValidateFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show($"File not found: {System.IO.Path.GetFileName(filePath)}", "File Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Try to open the file briefly to ensure it's readable
            using (var stream = File.OpenRead(filePath))
            {
                // Just opening to test accessibility
            }
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Cannot access file: {System.IO.Path.GetFileName(filePath)}\n{ex.Message}", "File Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private void Blank_Click(object sender, RoutedEventArgs e)
    {
        _liveWindow?.ShowBlank();
        StopMediaPulse();
    }

    private void CreatePlaylist_Click(object sender, RoutedEventArgs e)
    {
        var serviceElementsWindow = new ServiceElementsWindow(this)
        {
            Owner = this
        };

        serviceElementsWindow.ShowDialog(); // The window will handle adding items directly to the playlist
    }

    // Drag and Drop Event Handlers for Playlist
    private void Playlist_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void Playlist_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (var file in files)
            {
                if (File.Exists(file))
                {
                    PlaylistListBox.Items.Add(new PlaylistItem(file));
                }
            }
        }
    }

    private void PlaylistListBox_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void PlaylistListBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Store the start point for drag detection
        _dragStartPoint = e.GetPosition(null);
    }

    private void PlaylistListBox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        // Check if we should start a drag operation
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var position = e.GetPosition(null);
            if (Math.Abs(position.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(position.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (PlaylistListBox.SelectedItem is PlaylistItem item)
                {
                    var dataObject = new DataObject(DataFormats.FileDrop, new[] { item.FullPath });
                    DragDrop.DoDragDrop(PlaylistListBox, dataObject, DragDropEffects.Move);
                }
            }
        }
    }

    private void PlaylistListBox_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        LaunchSelectedMedia();
    }

    private void GoLive_Click(object sender, RoutedEventArgs e)
    {
        LaunchSelectedMedia();
    }

    private void LaunchSelectedMedia()
    {
        if (PlaylistListBox.SelectedItem is PlaylistItem item && _liveWindow != null)
        {
            // Validate file still exists before attempting to play
            if (!File.Exists(item.FullPath))
            {
                MessageBox.Show($"File not found: {item.FileName}\n\nThe file may have been moved or deleted.",
                    "File Error", MessageBoxButton.OK, MessageBoxImage.Error);
                PlaylistListBox.Items.Remove(item);
                return;
            }

            // Auto-pause background music when playing media
            if (_backgroundMusicPlayer.Source != null && !_backgroundMusicPlayer.IsMuted && _backgroundMusicPlayer.CanPause)
            {
                _backgroundMusicPlayer.Pause();
                _backgroundMusicAutoPaused = true;
            }

            _liveWindow.ShowMedia(item.FullPath);
            UpdatePreview(item.FullPath);
            StartMediaPulse();
        }
        else
        {
            MessageBox.Show("Select an item in the list first.", "No selection",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void UpdateLivePreview(object? sender, EventArgs e)
    {
        if (_liveWindow != null)
        {
            try
            {
                // Capture the live output window content
                var bounds = new System.Windows.Rect(0, 0, _liveWindow.ActualWidth, _liveWindow.ActualHeight);
                var renderBitmap = new RenderTargetBitmap(
                    (int)bounds.Width, 
                    (int)bounds.Height, 
                    96, 96, 
                    PixelFormats.Pbgra32);

                renderBitmap.Render(_liveWindow);

                // Update the preview image
                PreviewImage.Source = renderBitmap;
                PreviewLabel.Text = "Live Output Preview";
            }
            catch
            {
                // If capture fails, show fallback
                PreviewImage.Source = null;
                PreviewLabel.Text = "Live Output (Preview unavailable)";
            }
        }
    }

    private void UpdatePreview(string filePath)
    {
        // Don't update preview if live preview is active
        // The live preview timer will handle showing the actual content
        string ext = System.IO.Path.GetExtension(filePath).ToLower();
        string filename = System.IO.Path.GetFileName(filePath);

        if (IsImageFormat(ext))
        {
            PreviewLabel.Text = $"Image: {filename}";
        }
        else if (IsMediaFormat(ext))
        {
            PreviewLabel.Text = $"Media: {filename}";
        }
        else
        {
            PreviewLabel.Text = filename;
        }
    }

    private System.Windows.Media.Imaging.BitmapImage? CreateVideoThumbnail(string videoPath)
    {
        try
        {
            var width = 320;
            var height = 240;
            var filename = System.IO.Path.GetFileNameWithoutExtension(videoPath);
            
            // Create a better placeholder with filename
            var drawingVisual = new DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                // Draw a gradient background
                var gradientBrush = new LinearGradientBrush();
                gradientBrush.StartPoint = new Point(0, 0);
                gradientBrush.EndPoint = new Point(0, 1);
                gradientBrush.GradientStops.Add(new GradientStop(Colors.DarkBlue, 0));
                gradientBrush.GradientStops.Add(new GradientStop(Colors.MediumBlue, 1));
                
                context.DrawRectangle(gradientBrush, null, new System.Windows.Rect(0, 0, width, height));
                
                // Draw a play icon in the center
                var playIconGeometry = new PathGeometry();
                var pathFigure = new PathFigure
                {
                    StartPoint = new Point(width * 0.4, height * 0.3)
                };
                pathFigure.Segments.Add(new LineSegment(new Point(width * 0.4, height * 0.7), true));
                pathFigure.Segments.Add(new LineSegment(new Point(width * 0.7, height * 0.5), true));
                pathFigure.Segments.Add(new LineSegment(new Point(width * 0.4, height * 0.3), true));
                playIconGeometry.Figures.Add(pathFigure);
                
                context.DrawGeometry(Brushes.White, null, playIconGeometry);
                
                // Draw filename at the bottom
                var formattedText = new FormattedText(
                    filename.Length > 25 ? filename.Substring(0, 22) + "..." : filename,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    12,
                    Brushes.White,
                    96);
                
                var textX = (width - formattedText.Width) / 2;
                var textY = height - 25;
                context.DrawText(formattedText, new Point(textX, textY));
            }
            
            var renderTargetBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(drawingVisual);
            
            var bitmapImage = new BitmapImage();
            using (var stream = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));
                encoder.Save(stream);
                
                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
            }
            
            return bitmapImage;
        }
        catch (Exception ex)
        {
            // Log the error for debugging
            System.Diagnostics.Debug.WriteLine($"Thumbnail creation failed: {ex.Message}");
            return null;
        }
    }

    private bool IsImageFormat(string ext) => new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" }.Contains(ext);
    private bool IsMediaFormat(string ext) => new[] { ".mp4", ".mov", ".wmv", ".mkv", ".mp3", ".wav", ".flac", ".wma" }.Contains(ext);

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Get the item that was right-clicked
        var menuItem = sender as MenuItem;
        var contextMenu = menuItem?.Parent as ContextMenu;
        var listBox = contextMenu?.PlacementTarget as ListBox;
        
        if (listBox?.SelectedItem is PlaylistItem item)
        {
            var result = MessageBox.Show($"Delete '{item.FileName}'?", "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                listBox.Items.Remove(item);
            }
        }
    }

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (PlaylistListBox.SelectedItem is PlaylistItem item)
        {
            var result = MessageBox.Show($"Delete '{item.FileName}'?", "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                PlaylistListBox.Items.Remove(item);
            }
        }
        else
        {
            MessageBox.Show("Select an item to delete.", "No Selection",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        _liveWindow?.Play();
        StartMediaPulse();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        _liveWindow?.Pause();
        StopMediaPulse();
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_liveWindow != null)
        {
            // Toggle play/pause based on current state
            if (_liveWindow.IsPlaying())
            {
                _liveWindow.Pause();
                StopMediaPulse();
            }
            else
            {
                _liveWindow.Play();
                StartMediaPulse();
            }
        }
    }

    private void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (PlaylistListBox.SelectedIndex > 0)
        {
            PlaylistListBox.SelectedIndex--;
            LaunchSelectedMedia();
        }
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (PlaylistListBox.SelectedIndex < PlaylistListBox.Items.Count - 1)
        {
            PlaylistListBox.SelectedIndex++;
            LaunchSelectedMedia();
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _liveWindow?.Stop();
        StopMediaPulse();
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _liveWindow?.SetVolume(e.NewValue);
    }

    private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isDraggingProgressBar = true;
    }

    private void ProgressSlider_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isDraggingProgressBar = false;
        if (_liveWindow != null && ProgressSlider.Maximum > 0)
        {
            double position = (ProgressSlider.Value / 100.0);
            _liveWindow.Seek(position);
            
            // Immediately update display after seeking
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                () => UpdateProgressDisplay(),
                System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private void UpdateProgressDisplay()
    {
        if (_liveWindow != null && !_isDraggingProgressBar)
        {
            var progress = _liveWindow.GetProgress();
            ProgressSlider.Value = progress.ProgressPercent;
            CurrentTimeLabel.Text = FormatTime(progress.CurrentTime);
            DurationLabel.Text = FormatTime(progress.Duration);
        }
    }

    private string FormatTime(TimeSpan time)
    {
        if (time.Hours > 0)
            return $"{time.Hours:D1}:{time.Minutes:D2}:{time.Seconds:D2}";
        else
            return $"{time.Minutes:D2}:{time.Seconds:D2}";
    }

    private void SavePlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (PlaylistListBox.Items.Count == 0)
        {
            MessageBox.Show("Playlist is empty. Add files before saving.", "Empty Playlist",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new SaveFileDialog
        {
            Filter = "Playlist Files|*.pls|Text Files|*.txt|All Files|*.*",
            DefaultExt = ".pls"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                using (var writer = new StreamWriter(dlg.FileName))
                {
                    foreach (var item in PlaylistListBox.Items)
                    {
                        if (item is PlaylistItem playlistItem)
                        {
                            writer.WriteLine(playlistItem.FullPath);
                        }
                    }
                }
                MessageBox.Show("Playlist saved successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving playlist: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void LoadPlaylist_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Playlist Files|*.pls|Text Files|*.txt|All Files|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                PlaylistListBox.Items.Clear();
                using (var reader = new StreamReader(dlg.FileName))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line) && File.Exists(line))
                        {
                            PlaylistListBox.Items.Add(new PlaylistItem(line));
                        }
                    }
                }
                MessageBox.Show($"Playlist loaded successfully. {PlaylistListBox.Items.Count} files added.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading playlist: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SelectBackgroundMusic_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Audio Files|*.mp3;*.wav;*.flac;*.wma;*.m4a|All Files|*.*",
            Title = "Select Background Music"
        };

        if (dlg.ShowDialog() == true)
        {
            _settings.BackgroundMusicPath = dlg.FileName;
            _settings.BackgroundMusicEnabled = true;
            _settings.Save();
            
            _backgroundMusicPlayer.Source = new Uri(_settings.BackgroundMusicPath);
            _backgroundMusicPlayer.Play();
            StartBackgroundMusicPulse();
            
            MessageBox.Show("Background music updated successfully!", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BackgroundMusicVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_settings != null && _backgroundMusicPlayer != null)
        {
            _settings.BackgroundMusicVolume = e.NewValue;
            _settings.Save();
            _backgroundMusicPlayer.Volume = e.NewValue;
        }
    }

    private void BackgroundMusicPlay_Click(object sender, RoutedEventArgs e)
    {
        if (_backgroundMusicPlayer.Source != null)
        {
            _backgroundMusicPlayer.Play();
            _settings.BackgroundMusicEnabled = true;
            _settings.Save();
            _backgroundMusicPlayer.IsMuted = false;
            _backgroundMusicAutoPaused = false; // Reset flag when manually played
            StartBackgroundMusicPulse();
        }
        else
        {
            MessageBox.Show("No background music selected. Please select music first.", "No Music",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BackgroundMusicPause_Click(object sender, RoutedEventArgs e)
    {
        if (_backgroundMusicPlayer.Source != null)
        {
            _backgroundMusicPlayer.Pause();
            _backgroundMusicAutoPaused = false; // Reset flag when manually paused
            StopBackgroundMusicPulse();
        }
    }

    private void StartBackgroundMusicPulse()
    {
        // Create a timer for smooth fading effect
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
            BackgroundMusicGroupBox.Background = brush;
        };
        
        _pulseTimer.Start();
    }

    private void StopBackgroundMusicPulse()
    {
        if (_pulseTimer != null)
        {
            _pulseTimer.Stop();
            _pulseTimer = null;
        }
        // Clear the background completely
        BackgroundMusicGroupBox.Background = null;
        BackgroundMusicGroupBox.InvalidateVisual(); // Force UI refresh
    }

    private void StartMediaPulse()
    {
        // Simple solid light blue background when active
        MediaControlsGroupBox.Background = new SolidColorBrush(Color.FromRgb(135, 206, 250)); // Light sky blue
    }

    private void StopMediaPulse()
    {
        // Clear the background completely
        MediaControlsGroupBox.Background = null;
        MediaControlsGroupBox.InvalidateVisual(); // Force UI refresh
    }

    private void BackgroundMusicMute_Click(object sender, RoutedEventArgs e)
    {
        if (_backgroundMusicPlayer.Source != null)
        {
            _backgroundMusicPlayer.IsMuted = !_backgroundMusicPlayer.IsMuted;
        }
    }

    private void BackgroundMusicStop_Click(object sender, RoutedEventArgs e)
    {
        if (_backgroundMusicPlayer.Source != null)
        {
            _backgroundMusicPlayer.Stop();
            _backgroundMusicPlayer.Position = TimeSpan.Zero;
            StopBackgroundMusicPulse();
        }
    }
}
