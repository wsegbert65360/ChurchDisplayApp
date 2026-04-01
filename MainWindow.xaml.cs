using Microsoft.Win32;
using QRCoder;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using Serilog;
using ChurchDisplayApp.Services;
using ChurchDisplayApp.ViewModels;
using ChurchDisplayApp.Models;
using ChurchDisplayApp.Interfaces;
using LibVLCSharp.Shared;

namespace ChurchDisplayApp;


public partial class MainWindow : Window, IDisplayController
{
    private AppSettings _settings = AppSettings.Current;
    private readonly DispatcherTimer _livePreviewTimer;
    private readonly RemoteControlServer _remoteControlServer = new();
    private const int RemoteControlPortPreferred = AppConstants.Network.RemoteControlPortPreferred;
    private const int RemoteControlPortFallback = AppConstants.Network.RemoteControlPortFallback;
    private readonly LibVLC _libVLC;
    private readonly PlaylistManager _playlistManager = new();
    public PlaylistManager PlaylistManager => _playlistManager;
    private MediaControlService _mediaControlService = null!;
    private LiveOutputWindow _liveWindow = null!;
    public MainViewModel ViewModel { get; private set; } = null!;
    private readonly MonitorService _monitorService = new();
    private System.Windows.Threading.DispatcherTimer? _progressUpdateTimer;
    private BackgroundMusicService? _backgroundMusicService;
    private AmenResolveService? _amenResolveService;
    private PlaylistDragDropManager? _playlistDragDropManager;
    private DispatcherTimer? _mediaPulseTimer;
    private double _mediaPulseOpacity = 0.3;
    private int _mediaPulseDirection = 1;
    private readonly SolidColorBrush _mediaPulseBrush = new SolidColorBrush(AppConstants.Colors.PulseLightBlue);

    public MainWindow()
    {
        InitializeComponent();

        // Bind playlist
        PlaylistListBox.ItemsSource = _playlistManager.Items;

        Closing += MainWindow_Closing;
        SizeChanged += MainWindow_SizeChanged;
        
        // Initialize services
        LibVLCSharp.Shared.Core.Initialize();
        var vlcOptions = new[] 
        { 
            "--quiet", 
            "--no-osd", 
            "--no-video-title-show", 
            "--no-snapshot-preview",
            "--aout=wasapi"
        };
        _libVLC = new LibVLC(vlcOptions);
        _backgroundMusicService = new BackgroundMusicService(_settings);
        _liveWindow = new LiveOutputWindow(_libVLC);
        _playlistDragDropManager = new PlaylistDragDropManager(PlaylistListBox, _playlistManager, () => { });
        
        // Load background music if configured
        if (!string.IsNullOrEmpty(_settings.BackgroundMusicPath) && File.Exists(_settings.BackgroundMusicPath))
        {
            _backgroundMusicService.Load(_settings.BackgroundMusicPath);
            if (_settings.BackgroundMusicEnabled)
            {
                _backgroundMusicService.Play();
                _backgroundMusicService.StartPulseAnimation(BackgroundMusicGroupBox);
            }
        }
        
        // Set up background music volume slider
        BackgroundMusicVolumeSlider.Value = _settings.BackgroundMusicVolume;
        BackgroundMusicVolumeText.Text = $"{(int)(_settings.BackgroundMusicVolume * 100)}%";
        BackgroundMusicVolumeSlider.ValueChanged += BackgroundMusicVolume_ValueChanged;
        
        // Set up live preview timer
        _livePreviewTimer = new DispatcherTimer 
        { 
            Interval = TimeSpan.FromMilliseconds(AppConstants.UI.LivePreviewIntervalMs) 
        };
        _livePreviewTimer.Tick += UpdateLivePreview;
        _livePreviewTimer.Start();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        
        // Restore saved sidebar width from settings
        ApplySavedSidebarWidth();
        
        _liveWindow.Owner = this;

        // Create and show the live output window after MainWindow is shown
        
        // Subscribe to MediaEnded event to stop media state
        _liveWindow.MediaEnded += (s, e) => ViewModel.StopCommand.Execute(null);

        // Initialize media control service and ViewModel
        _mediaControlService = new MediaControlService(_liveWindow, _settings, _backgroundMusicService);
        _mediaControlService.MediaStateChanged += (s, e) => {
            if (_mediaControlService.IsPlaying()) StartMediaPulseAnimation();
            else StopMediaPulseAnimation();
        };
        ViewModel = new MainViewModel(_playlistManager, _mediaControlService, _settings, _backgroundMusicService);
        DataContext = ViewModel;

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
            Interval = TimeSpan.FromMilliseconds(AppConstants.UI.ProgressUpdateIntervalMs) 
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
                SelectStandardMusic_Click(this, new RoutedEventArgs());
            }
        }
        
        // Initialize display controls background based on playlist content
        
        _ = StartRemoteControlAsync();

        // Initialize AmenResolveService
        var soundFontPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sounds", "SalC5Light2.sf2");
        if (System.IO.File.Exists(soundFontPath))
        {
            _amenResolveService = new AmenResolveService(soundFontPath);
        }
        else
        {
            Log.Warning("SoundFont file not found at {Path}. Amen resolve feature will be unavailable.", soundFontPath);
        }
    }


    private async Task StartRemoteControlAsync()
    {
        // Prefer port 80 so you can type only http://PC-IP/ on your phone.
        // If port 80 fails (requires admin / may be in use), fall back to 8088.
        try
        {
            await _remoteControlServer.StartAsync(Dispatcher, this, RemoteControlPortPreferred);
            UpdateRemoteQrCode(RemoteControlPortPreferred);
            return;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to start remote control server on preferred port {Port}", RemoteControlPortPreferred);
        }

        try
        {
            await _remoteControlServer.StartAsync(Dispatcher, this, RemoteControlPortFallback);
            UpdateRemoteQrCode(RemoteControlPortFallback);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start remote control server on fallback port {Port}. Remote control will be unavailable.", RemoteControlPortFallback);
        }
    }

    private void UpdateRemoteQrCode(int portInUse)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var url = GetRemoteUrl(portInUse);
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
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to generate QR code");
                RemoteQrImage.Source = null;
            }
        });
    }

    private string GetRemoteUrl(int portInUse)
    {
        var portSuffix = portInUse == 80 ? string.Empty : $":{portInUse}";

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
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to enumerate local IP addresses");
        }

        return null;
    }


    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Save sidebar width as a proportion of total window width
        try
        {
            if (ActualWidth > 0)
            {
                var sidebarWidth = RootGrid.ColumnDefinitions[0].ActualWidth;
                _settings.MainWindowLeftColumnProportion = sidebarWidth / ActualWidth;
                _settings.Save();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not save sidebar width");
        }

        Log.Information("Main window closing (Fast Shutdown initiated)...");

        // 1. Give the user instant feedback by hiding windows
        Hide();
        if (_liveWindow != null)
        {
            try { _liveWindow.Hide(); } catch { }
        }

        // 2. Stop timers immediately on UI thread
        _livePreviewTimer?.Stop();
        _progressUpdateTimer?.Stop();

        // 3. Perform the actual cleanup in a background task to avoid deadlocking the UI thread
        Task.Run(async () =>
        {
            try
            {
                Log.Information("Background cleanup started...");

                // Stop background music
                if (_backgroundMusicService != null)
                {
                    _backgroundMusicService.Stop();
                    _backgroundMusicService.Dispose();
                }

                // Stop remote control server (no .Wait() to avoid deadlock)
                if (_remoteControlServer != null && _remoteControlServer.IsRunning)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(AppConstants.Timeouts.ShutdownTimeoutSeconds));
                    await _remoteControlServer.StopAsync(cts.Token);
                }

                // Stop and Close display window
                if (_liveWindow != null)
                {
                    // Dispatcher.Invoke is safer for closing the window if it's still needed, 
                    // but we already called Hide() so we can just let it be disposed by VLC.
                }

                // Dispose VLC
                _libVLC?.Dispose();
                
                Log.Information("Background cleanup finished.");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error during background cleanup");
            }
            finally
            {
                // Final safety: ensure process exit
                Log.Information("Final process exit.");
                Environment.Exit(0);
            }
        });

        // We allow the window to "close" logically (hide), 
        // while the background task handles the heavy lifting and final Exit.
    }

    /// <summary>
    /// Sets the sidebar column width from the saved proportion in AppSettings.
    /// Must be called after the window has rendered (ActualWidth is valid).
    /// </summary>
    private void ApplySavedSidebarWidth()
    {
        try
        {
            var proportion = _settings.MainWindowLeftColumnProportion ?? 0.25;
            proportion = Math.Clamp(proportion, 0.05, 0.80);

            // Convert proportion to pixel width based on current window width
            var pixelWidth = ActualWidth * proportion;

            // Enforce column min/max in pixels
            var sidebarCol = RootGrid.ColumnDefinitions[0];
            pixelWidth = Math.Max(pixelWidth, sidebarCol.MinWidth);
            pixelWidth = Math.Min(pixelWidth, ActualWidth - 8 - 360); // leave room for splitter + main min

            sidebarCol.Width = new GridLength(pixelWidth, GridUnitType.Pixel);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not restore sidebar width — using default");
        }
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Keep sidebar proportional when window is resized
        // Only adjust if the change came from window resize, not splitter drag.
        // We detect this by comparing if column 0 width / old window width ≈ saved proportion.
        try
        {
            if (e.PreviousSize.Width <= 0 || ActualWidth <= 0) return;

            var sidebarCol = RootGrid.ColumnDefinitions[0];
            var currentProportion = sidebarCol.ActualWidth / e.PreviousSize.Width;
            var savedProportion   = _settings.MainWindowLeftColumnProportion ?? 0.25;

            // If current proportion is within 2% of saved, it's a window resize — scale it
            if (Math.Abs(currentProportion - savedProportion) < 0.02)
            {
                var newPixelWidth = ActualWidth * savedProportion;
                newPixelWidth = Math.Max(newPixelWidth, sidebarCol.MinWidth);
                newPixelWidth = Math.Min(newPixelWidth, ActualWidth - 8 - 360);
                sidebarCol.Width = new GridLength(newPixelWidth, GridUnitType.Pixel);
            }
            // else: user is dragging the splitter — don't interfere
        }
        catch
        {
            // Non-critical — silently ignore resize calculation errors
        }
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
        _liveWindow?.Dispose();
        
        _liveWindow = new LiveOutputWindow(_libVLC)
        {
            Owner = this
        };
        
        _liveWindow.MediaEnded += (s, e) => ViewModel.StopCommand.Execute(null);
        
        // Update existing services with new window
        _mediaControlService?.UpdateLiveWindow(_liveWindow);
        // Restore saved volume to the new window
        _mediaControlService?.SetVolume(ViewModel.Volume);
        
        PositionDisplayWindow();
        
        _liveWindow.Show();
        _liveWindow.ShowBlank();
    }

    private void CreatePlaylist_Click(object sender, RoutedEventArgs e)
    {
        var elementsWindow = new ServiceElementsWindow(_playlistManager);
        elementsWindow.Owner = this;
        elementsWindow.ShowDialog(); // The window will handle adding items directly to the playlist
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
                
                // Immediately update the time string in the VM if we're paused
                // since the timer update is now skipped when paused
                if (ViewModel != null && !ViewModel.IsPlaying)
                {
                    var info = _mediaControlService.GetProgress();
                    if (info != null)
                    {
                        ViewModel.CurrentTimeStr = ViewModel.FormatTime(info.CurrentTime);
                    }
                }

                // Wait for seek to settle 
                await Task.Delay(AppConstants.UI.LiveWindowSeekDelayMs);

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

        // CHANGED: prefer LastPlaylistSaveDirectory, fall back to LastMediaDirectory
        var startDir = _settings.LastPlaylistSaveDirectory ?? _settings.LastMediaDirectory;
        if (!string.IsNullOrEmpty(startDir) && Directory.Exists(startDir))
            dlg.InitialDirectory = startDir;

        if (dlg.ShowDialog() == true)
        {
            try
            {
                _playlistManager.SavePlaylist(dlg.FileName);

                // CHANGED: remember the folder for next time
                _settings.LastPlaylistSaveDirectory = Path.GetDirectoryName(dlg.FileName);
                _settings.Save();

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

    private void SelectStandardMusic_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = MediaConstants.GetAudioFilter(),
            Title = "Select Background Music"
        };

        if (dlg.ShowDialog() == true)
        {
            _settings.BackgroundMusicPath = dlg.FileName;
            _settings.Save();
            
            _backgroundMusicService?.Load(_settings.BackgroundMusicPath);
            ViewModel.UpdateBgmNames();
            MessageBox.Show("Background music updated successfully!", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void SelectChildSermonMusic_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = MediaConstants.GetAudioFilter(),
            Title = "Select Child Sermon Background Music"
        };

        if (dlg.ShowDialog() == true)
        {
            _settings.BackgroundMusicChildSermonPath = dlg.FileName;
            _settings.Save();
            
            // We don't load immediately to avoid overwriting the current media 
            // of the other mode. It will be loaded on Play.
            ViewModel.UpdateBgmNames();
            MessageBox.Show("Child sermon music updated successfully!", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BackgroundMusicVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _backgroundMusicService?.SetVolume(e.NewValue);
        if (BackgroundMusicVolumeText != null)
        {
            BackgroundMusicVolumeText.Text = $"{(int)(e.NewValue * 100)}%";
        }
    }

    private void BackgroundMusicPlayStandard_Click(object sender, RoutedEventArgs e)
    {
        PlayBackgroundMusic(_settings.BackgroundMusicPath, "Standard BGM", BackgroundMusicGroupBox);
    }

    private void BackgroundMusicPlayChildrensSermon_Click(object sender, RoutedEventArgs e)
    {
        PlayBackgroundMusic(_settings.BackgroundMusicChildSermonPath, "Children's Sermon BGM", BackgroundMusicGroupBox);
    }

    private void PlayBackgroundMusic(string? path, string context, Border pulseTarget)
    {
        if (string.IsNullOrEmpty(path))
        {
            MessageBox.Show($"No {context} file selected. Please select one in Settings.", "No Music",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_backgroundMusicService == null) return;

        if (!_backgroundMusicService.CanPlay && !File.Exists(path))
        {
            MessageBox.Show($"The selected {context} file could not be found.", "File Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Stop any active media playback to avoid overlapping audio
        ViewModel.StopCommand.Execute(null);

        _backgroundMusicService.StopPulseAnimation();
        _backgroundMusicService.Load(path);
        _backgroundMusicService.Play();
        _backgroundMusicService.StartPulseAnimation(pulseTarget);
    }

    private void BackgroundMusicPause_Click(object sender, RoutedEventArgs e)
    {
        _backgroundMusicService?.Pause();
        _backgroundMusicService?.StopPulseAnimation();
    }



    private async void BackgroundMusicStop_Click(object sender, RoutedEventArgs e)
    {
        _backgroundMusicService?.Stop();
        _backgroundMusicService?.StopPulseAnimation();
        if (_amenResolveService != null)
        {
            // Brief delay to let VLC stop complete before starting Amen resolve
            await Task.Delay(150);
            _ = Task.Run(async () =>
            {
                try
                {
                    await _amenResolveService!.ExecuteResolveAsync((float)_settings.BackgroundMusicVolume);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Amen resolve failed");
                }
            });
        }
    }

    #region IDisplayController Implementation

    public void Next() => ViewModel.NextCommand.Execute(null);
    public void Previous() => ViewModel.PreviousCommand.Execute(null);
    public void Play() => ViewModel.PlayCommand.Execute(null);
    public void Pause() => ViewModel.PauseCommand.Execute(null);
    public void Stop() => ViewModel.StopCommand.Execute(null);
    public void Blank() => ViewModel.BlankCommand.Execute(null);
    public void SetVolume(double volume) => ViewModel.Volume = volume;
    public void VolumeUp() => ViewModel.Volume = Math.Clamp(ViewModel.Volume + 0.02, 0.0, 1.0);
    public void VolumeDown() => ViewModel.Volume = Math.Clamp(ViewModel.Volume - 0.02, 0.0, 1.0);
    public void PlayIndex(int index)
    {
        if (index >= 0 && index < _playlistManager.Items.Count)
        {
            ViewModel.SelectedItem = _playlistManager.Items[index];
            ViewModel.PlayCommand.Execute(null);
        }
    }

    public RemoteStatus GetStatus()
    {
        var progress = _mediaControlService.GetProgress();
        var bgmTitle = _backgroundMusicService != null && _backgroundMusicService.IsPlaying && _backgroundMusicService.LoadedPath != null
            ? System.IO.Path.GetFileName(_backgroundMusicService.LoadedPath)
            : "None";
        var isBgmPlaying = _backgroundMusicService?.IsPlaying ?? false;

        if (progress == null)
        {
            var title = ViewModel.CurrentMediaTitle;
            if (title == "Idle" && isBgmPlaying)
            {
                title = $"BGM: {bgmTitle}";
            }

            return new RemoteStatus(
                title,
                0,
                "00:00",
                "00:00",
                ViewModel.Volume,
                bgmTitle,
                isBgmPlaying
            );
        }

        return new RemoteStatus(
            ViewModel.CurrentMediaTitle,
            progress.ProgressPercent,
            ViewModel.FormatTime(progress.CurrentTime),
            ViewModel.FormatTime(progress.Duration),
            ViewModel.Volume,
            bgmTitle,
            isBgmPlaying
        );
    }

    public void PlayStandardBgm() => PlayBackgroundMusic(_settings.BackgroundMusicPath, "Background Music", BackgroundMusicGroupBox);
    public void PlayKidsBgm() => PlayBackgroundMusic(_settings.BackgroundMusicChildSermonPath, "Children's Sermon Music", BackgroundMusicGroupBox);
    public void PauseBgm() => _backgroundMusicService?.Pause();
    public void StopBgm() => BackgroundMusicStop_Click(this, new System.Windows.RoutedEventArgs());
    public List<RemotePlaylistItem> GetPlaylistItems()
    {
        var result = new List<RemotePlaylistItem>();
        for (int i = 0; i < _playlistManager.Items.Count; i++)
        {
            var playlistItem = _playlistManager.Items[i];
            result.Add(new RemotePlaylistItem(i, playlistItem.FileName, playlistItem.FullPath));
        }
        return result;
    }

    #endregion

    private void ShowShortcuts_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Keyboard Shortcuts:\n\n" +
            "Space / Right Arrow - Next Media\n" +
            "Left Arrow - Previous Media\n" +
            "B - Blank Display\n" +
            "F11 - Toggle Fullscreen\n" +
            "Escape - Exit Fullscreen",
            "Keyboard Shortcuts",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
    private void StartMediaPulseAnimation()
    {
        if (_mediaPulseTimer != null) return;
        _mediaPulseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _mediaPulseTimer.Tick += (s, e) =>
        {
            _mediaPulseOpacity += _mediaPulseDirection * 0.02;
            if (_mediaPulseOpacity >= 1.0) { _mediaPulseOpacity = 1.0; _mediaPulseDirection = -1; }
            else if (_mediaPulseOpacity <= 0.3) { _mediaPulseOpacity = 0.3; _mediaPulseDirection = 1; }
            _mediaPulseBrush.Opacity = _mediaPulseOpacity;
            MediaControlsContainer.Background = _mediaPulseBrush;
        };
        _mediaPulseTimer.Start();
    }

    private void StopMediaPulseAnimation()
    {
        _mediaPulseTimer?.Stop();
        _mediaPulseTimer = null;
        if (MediaControlsContainer != null)
            MediaControlsContainer.Background = Brushes.White;
    }
}
