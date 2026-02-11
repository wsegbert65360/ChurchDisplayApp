using Microsoft.Win32;
using QRCoder;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using Serilog;
using ChurchDisplayApp.Services;
using ChurchDisplayApp.ViewModels;
using ChurchDisplayApp.Models;

namespace ChurchDisplayApp;


public partial class MainWindow : Window
{
    private static AppSettings _settings = AppSettings.Load();
    private readonly MediaElement _backgroundMusicPlayer;
    private readonly DispatcherTimer _livePreviewTimer;
    private readonly RemoteControlServer _remoteControlServer = new();
    private const int RemoteControlPortPreferred = 80;
    private const int RemoteControlPortFallback = 8088;
    private int _remoteControlPortInUse = 0;
    private readonly PlaylistManager _playlistManager = new();
    public PlaylistManager PlaylistManager => _playlistManager;
    private readonly LayoutManager _layoutManager;
    private MediaControlService _mediaControlService = null!;
    private LiveOutputWindow? _liveWindow;
    public MainViewModel ViewModel { get; private set; } = null!;
    private readonly MonitorService _monitorService = new();
    private System.Windows.Threading.DispatcherTimer? _progressUpdateTimer;
    private BackgroundMusicService? _backgroundMusicService;
    private PlaylistDragDropManager? _playlistDragDropManager;
    private RemoteControlCoordinator? _remoteControlCoordinator;

    public MainWindow()
    {
        InitializeComponent();

        _layoutManager = new LayoutManager(MainLayoutGrid, _settings);
        _layoutManager.ApplyLayout();

        // Bind playlist
        PlaylistListBox.ItemsSource = _playlistManager.Items;

        Closing += MainWindow_Closing;
        
        // Initialize background music player
        _backgroundMusicPlayer = new MediaElement
        {
            LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Manual,
            Volume = _settings.BackgroundMusicVolume,
            IsMuted = !_settings.BackgroundMusicEnabled
        };
        
        // Initialize BackgroundMusicService
        _backgroundMusicService = new BackgroundMusicService(_backgroundMusicPlayer, _settings);
        _playlistDragDropManager = new PlaylistDragDropManager(PlaylistListBox, _playlistManager, () => { });
        
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
                _backgroundMusicService.Play();
                _backgroundMusicService.StartPulseAnimation(BackgroundMusicGroupBox);
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
        
        // Subscribe to MediaEnded event to stop media state
        _liveWindow.MediaEnded += (s, e) => ViewModel.StopCommand.Execute(null);

        // Initialize media control service and ViewModel
        _mediaControlService = new MediaControlService(_liveWindow, _settings, _backgroundMusicService);
        ViewModel = new MainViewModel(_playlistManager, _mediaControlService, _settings, _backgroundMusicService);
        DataContext = ViewModel;

        // Initialize RemoteControlCoordinator
        _remoteControlCoordinator = new RemoteControlCoordinator(
            _playlistManager, 
            _mediaControlService, 
            ViewModel
        );

        // Detect monitors using the service
        var monitors = _monitorService.GetMonitors();
        
        if (monitors.Count >= 2)
        {
            var liveMonitor = _monitorService.SelectDisplayMonitor(monitors);

            if (liveMonitor != null)
            {
                // Show window first at default location, then reposition it
                _liveWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                _liveWindow.Show();
                
                // Position it on the selected monitor
                _monitorService.PositionWindowOnMonitor(_liveWindow, liveMonitor);
                _liveWindow.Topmost = true;
            }
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
        _progressUpdateTimer.Tick += (s, args) => ViewModel.UpdateProgress();
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
        
        // Initialize display controls background based on playlist content
        
        _ = StartRemoteControlAsync();
    }


    private async Task StartRemoteControlAsync()
    {
        if (_remoteControlCoordinator == null) return;

        // Prefer port 80 so you can type only http://PC-IP/ on your phone.
        // If port 80 fails (requires admin / may be in use), fall back to 8088.
        try
        {
            await _remoteControlServer.StartAsync(Dispatcher, _remoteControlCoordinator, RemoteControlPortPreferred);
            _remoteControlPortInUse = RemoteControlPortPreferred;
            UpdateRemoteQrCode();
            return;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to start remote control server on preferred port {Port}", RemoteControlPortPreferred);
        }

        try
        {
            await _remoteControlServer.StartAsync(Dispatcher, _remoteControlCoordinator, RemoteControlPortFallback);
            _remoteControlPortInUse = RemoteControlPortFallback;
            UpdateRemoteQrCode();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start remote control server on fallback port {Port}. Remote control will be unavailable.", RemoteControlPortFallback);
        }
    }

    private void UpdateRemoteQrCode()
    {
        Dispatcher.BeginInvoke(() =>
        {
            var url = GetRemoteUrl();
            RemoteUrlText.Text = url;

            try
            {
                using var generator = new QRCodeGenerator();
                using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(data);
                var pngBytes = qrCode.GetGraphic(10);

                var image = new BitmapImage();
                using (var ms = new MemoryStream(pngBytes))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze();
                }

                RemoteQrImage.Source = image;
            }
            catch
            {
                RemoteQrImage.Source = null;
            }
        });
    }

    private string GetRemoteUrl()
    {
        var port = _remoteControlPortInUse != 0 ? _remoteControlPortInUse : RemoteControlPortFallback;
        var portSuffix = port == 80 ? string.Empty : $":{port}";

        var ip = GetLocalIPv4Address();
        if (!string.IsNullOrWhiteSpace(ip))
        {
            return $"http://{ip}{portSuffix}/";
        }

        return $"http://{Environment.MachineName}{portSuffix}/";
    }

    private static string? GetLocalIPv4Address()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var props = ni.GetIPProperties();
                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    if (IPAddress.IsLoopback(ua.Address))
                    {
                        continue;
                    }

                    // Ignore APIPA
                    var address = ua.Address.ToString();
                    if (address.StartsWith("169.254."))
                    {
                        continue;
                    }

                    return address;
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _layoutManager.SaveLayout();

        // Stop background music with timeout
        try
        {
            if (_backgroundMusicPlayer != null)
            {
                _backgroundMusicPlayer.Stop();
                _backgroundMusicPlayer.Close();
            }
        }
        catch
        {
        }

        // Stop remote server with timeout
        try
        {
            if (_remoteControlServer != null)
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                _remoteControlServer.StopAsync(cts.Token).Wait(TimeSpan.FromSeconds(2));
            }
        }
        catch
        {
        }
    }

    private void LayoutSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _layoutManager.OnSplitterDragCompleted(sender, e);
    }


    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = MediaConstants.GetAllMediaFilter(),
            Multiselect = true
        };

        if (dlg.ShowDialog() == true)
        {
            _playlistManager.AddFiles(dlg.FileNames);
            
            // Select the last inserted item
            if (_playlistManager.Items.Count > 0)
            {
                PlaylistListBox.SelectedIndex = _playlistManager.Items.Count - 1;
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
    }

    private void ToggleDisplay_Click(object sender, RoutedEventArgs e)
    {
        if (_liveWindow != null)
        {
            // Check if window has been closed (calling Show on a closed window throws exception)
            try
            {
                if (_liveWindow.IsVisible)
                {
                    // Hide the display window
                    _liveWindow.Hide();
                }
                else
                {
                    // Ensure it's on the correct monitor before showing
                    PositionDisplayWindow();
                    
                    // Show the display window
                    _liveWindow.Show();
                    _liveWindow.WindowState = WindowState.Maximized; 
                    _liveWindow.Topmost = true;
                }
            }
            catch (InvalidOperationException)
            {
                // Window was closed, need to recreate it
                Log.Information("LiveOutputWindow was closed, recreating it");
                RecreateLiveOutputWindow();
            }
        }
        else
        {
            // Window doesn't exist, create it
            RecreateLiveOutputWindow();
        }
    }

    private void PositionDisplayWindow()
    {
        if (_liveWindow == null) return;

        var monitors = _monitorService.GetMonitors();
        if (monitors.Count >= 2)
        {
            var liveMonitor = _monitorService.SelectDisplayMonitor(monitors);
            if (liveMonitor != null)
            {
                _liveWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                _monitorService.PositionWindowOnMonitor(_liveWindow, liveMonitor);
                _liveWindow.Topmost = true;
                return;
            }
        }
        
        // Fallback to centered on primary monitor
        _liveWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    private void RecreateLiveOutputWindow()
    {
        _liveWindow = new LiveOutputWindow
        {
            Owner = this
        };
        
        _liveWindow.MediaEnded += (s, e) => ViewModel.StopCommand.Execute(null);
        
        // Update existing services with new window instead of recreating everything
        _mediaControlService?.UpdateLiveWindow(_liveWindow);
        
        // Position on secondary monitor if available
        PositionDisplayWindow();
        
        _liveWindow.Show();
        _liveWindow.ShowBlank();
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

    private void Playlist_Drop(object sender, DragEventArgs e) => _playlistDragDropManager?.HandlePlaylistDrop(sender, e);
    private void PlaylistListBox_DragOver(object sender, DragEventArgs e) => _playlistDragDropManager?.HandleListBoxDragOver(sender, e);
    private void PlaylistListBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) => _playlistDragDropManager?.HandleListBoxPreviewMouseLeftButtonDown(sender, e);
    private void PlaylistListBox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e) => _playlistDragDropManager?.HandleListBoxPreviewMouseMove(sender, e);
    private void PlaylistListBox_Drop(object sender, DragEventArgs e) => _playlistDragDropManager?.HandleListBoxDrop(sender, e);



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
        ViewModel.PlayCommand.Execute(null);
    }

    private void UpdateLivePreview(object? sender, EventArgs e)
    {
        if (_liveWindow != null && _liveWindow.IsLoaded)
        {
            try
            {
                // Use the new snapshot-based approach
                var snapshot = _liveWindow.GetCurrentSnapshot();
                
                if (snapshot != null)
                {
                    PreviewImage.Source = snapshot;
                    PreviewLabel.Text = "Live Output Preview";
                }
                else
                {
                    // No content to show
                    PreviewImage.Source = null;
                    PreviewLabel.Text = "Live Output (No Media)";
                }
            }
            catch (Exception ex)
            {
                // If capture fails, show fallback
                PreviewImage.Source = null;
                PreviewLabel.Text = "Live Output (Preview Error)";
                Log.Warning(ex, "Live preview snapshot failed");
            }
        }
        else
        {
            // Window not ready yet
            PreviewImage.Source = null;
            PreviewLabel.Text = "Live Output (Loading...)";
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
            Log.Error(ex, "Thumbnail creation failed for {FilePath}", videoPath);
            return null;
        }
    }


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
                _playlistManager.RemoveItem(item);
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
                _playlistManager.RemoveItem(item);
            }
        }
        else
        {
            MessageBox.Show("Select an item to delete.", "No Selection",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }







    private void ProgressSlider_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ViewModel != null)
            ViewModel.IsScrubbing = true;
    }

    private async void ProgressSlider_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            if (_liveWindow != null && ProgressSlider.Maximum > 0)
            {
                double position = (ProgressSlider.Value / 100.0);
                _liveWindow.Seek(position);
                
                // Wait for seek to settle before resuming automatic updates
                await Task.Delay(300);

                // Re-check state after delay
                if (ViewModel != null && _liveWindow != null)
                {
                    ViewModel.IsScrubbing = false;
                    ViewModel.UpdateProgress();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during scrubbing slider release");
        }
        finally
        {
            if (ViewModel != null)
                ViewModel.IsScrubbing = false;
        }
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
            Filter = MediaConstants.GetPlaylistFilter(),
            DefaultExt = ".pls"
        };

        // Set initial directory to last used directory if available
        if (!string.IsNullOrEmpty(_settings.LastMediaDirectory))
        {
            dlg.InitialDirectory = _settings.LastMediaDirectory;
        }

        if (dlg.ShowDialog() == true)
        {
            try
            {
                _playlistManager.SavePlaylist(dlg.FileName);
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
            Filter = MediaConstants.GetPlaylistFilter()
        };

        // Set initial directory to last used directory if available
        if (!string.IsNullOrEmpty(_settings.LastMediaDirectory))
        {
            dlg.InitialDirectory = _settings.LastMediaDirectory;
        }

        if (dlg.ShowDialog() == true)
        {
            try
            {
                _playlistManager.LoadPlaylist(dlg.FileName);
                MessageBox.Show($"Playlist loaded successfully. {_playlistManager.Items.Count} files added.", "Success",
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
            Filter = MediaConstants.GetAudioFilter(),
            Title = "Select Background Music"
        };

        if (dlg.ShowDialog() == true)
        {
            _settings.BackgroundMusicPath = dlg.FileName;
            _settings.BackgroundMusicEnabled = true;
            _settings.Save();
            
            _backgroundMusicPlayer.Source = new Uri(_settings.BackgroundMusicPath);
            _backgroundMusicService?.Play();
            _backgroundMusicService?.StartPulseAnimation(BackgroundMusicGroupBox);
            
            MessageBox.Show("Background music updated successfully!", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BackgroundMusicVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _backgroundMusicService?.SetVolume(e.NewValue);
    }

    private void BackgroundMusicPlay_Click(object sender, RoutedEventArgs e)
    {
        _backgroundMusicService?.Play();
        _backgroundMusicService?.StartPulseAnimation(BackgroundMusicGroupBox);
    }

    private void BackgroundMusicPause_Click(object sender, RoutedEventArgs e)
    {
        _backgroundMusicService?.Pause();
        _backgroundMusicService?.StopPulseAnimation();
    }




    private void BackgroundMusicMute_Click(object sender, RoutedEventArgs e)
    {
        _backgroundMusicService?.ToggleMute();
    }

    private void BackgroundMusicStop_Click(object sender, RoutedEventArgs e)
    {
        _backgroundMusicService?.Stop();
        _backgroundMusicService?.StopPulseAnimation();
    }
}
