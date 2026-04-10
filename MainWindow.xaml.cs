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
    private AmenResolveService? _amenResolveService;
    private PlaylistDragDropManager? _playlistDragDropManager;
    private DispatcherTimer? _mediaPulseTimer;
    private double _mediaPulseOpacity = 0.3;
    private int _mediaPulseDirection = 1;
    private readonly SolidColorBrush _mediaPulseBrush = new SolidColorBrush(AppConstants.Colors.PulseLightBlue);
    private bool _isClosing = false;
    private Task? _amenTask;

    public MainWindow()
    {
        InitializeComponent();

        // Bind playlist
        PlaylistListBox.ItemsSource = _playlistManager.Items;

        Closing += MainWindow_Closing;
        SizeChanged += MainWindow_SizeChanged;
        
        // Initialize VLC media engine (all native init in one try-catch)
        try
        {
            LibVLCSharp.Shared.Core.Initialize();

            var vlcOptionsList = new List<string>
            {
                "--no-osd",
                "--no-video-title-show",
                "--no-snapshot-preview"
                // Remove --quiet so VLC audio device selection is visible in logs.
                // No --aout specified: VLC auto-selects the best audio output.
            };

            // If a specific audio device is configured, tell VLC to use it.
            // The device name comes from the log file (e.g. "[VLC/main] using device: X")
            if (!string.IsNullOrWhiteSpace(_settings.VlcAudioDevice))
            {
                vlcOptionsList.Add($"--mmdevice-audio-device={_settings.VlcAudioDevice}");
                Log.Information("VLC audio device override: {Device}", _settings.VlcAudioDevice);
            }

            _libVLC = new LibVLC(vlcOptionsList.ToArray());

            // Forward VLC's internal log to Serilog so audio device selection and
            // any playback errors are visible in %APPDATA%\ChurchDisplayApp\logs\.
            _libVLC.Log += (sender, e) =>
            {
                var message = $"[VLC/{e.Module}] {e.Message}";
                switch (e.Level)
                {
                    case LogLevel.Error:   Log.Error(message);   break;
                    case LogLevel.Warning: Log.Warning(message); break;
                    default:               Log.Debug(message);   break;
                }
            };

            _liveWindow = new LiveOutputWindow(_libVLC);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize VLC media engine");
            MessageBox.Show(
                $"Failed to initialize media engine.\n\nError: {ex.Message}\n\n" +
                "Please ensure the Microsoft Visual C++ Redistributable (x64) is installed\n" +
                "and restart the application.\n\n" +
                "Download: https://aka.ms/vs/17/release/vc_redist.x64.exe",
                "Church Display App - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            Application.Current.Shutdown();
            return;
        }

        _playlistDragDropManager = new PlaylistDragDropManager(PlaylistListBox, _playlistManager, () => { });
        
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

        // If VLC failed to initialize, the constructor returned early and
        // _liveWindow is null. Shutdown has already been requested — bail out
        // here to avoid a secondary NullReferenceException crash dialog.
        if (_liveWindow == null)
            return;

        // Restore saved sidebar width from settings
        ApplySavedSidebarWidth();

        _liveWindow.Owner = this;

        // Subscribe to MediaEnded event to stop media state
        _liveWindow.MediaEnded += (s, e) => ViewModel.StopCommand.Execute(null);

        // Initialize media control service and ViewModel
        _mediaControlService = new MediaControlService(_liveWindow, _settings);
        _mediaControlService.MediaStateChanged += (s, e) => {
            if (_mediaControlService.IsPlaying()) StartMediaPulseAnimation();
            else StopMediaPulseAnimation();
        };
        ViewModel = new MainViewModel(_playlistManager, _mediaControlService, _settings);
        DataContext = ViewModel;

        // Detect monitors using the service
        var monitors = _monitorService.GetMonitors();
        
        if (monitors.Count >= 2)
        {
            var liveMonitor = _monitorService.SelectDisplayMonitor(monitors);

            if (liveMonitor != null)
            {
                _liveWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                _liveWindow.Show();
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
        
        _ = StartRemoteControlAsync();

        // Initialize AmenResolveService
        var soundFontPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppConstants.Media.SoundsDirectory, AppConstants.Media.SoundFontFileName);
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
        int successfulPort = 0;

        try
        {
            await _remoteControlServer.StartAsync(Dispatcher, this, RemoteControlPortPreferred);
            successfulPort = RemoteControlPortPreferred;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to start remote control server on preferred port {Port}", RemoteControlPortPreferred);
        }

        if (successfulPort == 0)
        {
            try
            {
                await _remoteControlServer.StartAsync(Dispatcher, this, RemoteControlPortFallback);
                successfulPort = RemoteControlPortFallback;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start remote control server on fallback port {Port}. Remote control will be unavailable.", RemoteControlPortFallback);
            }
        }

        if (successfulPort > 0)
        {
            VerifyFirewallRule(successfulPort);
            UpdateRemoteQrCode(successfulPort);
            Log.Information("Remote control server running on port {Port}", successfulPort);
        }
        else
        {
            Dispatcher.BeginInvoke(() =>
            {
                RemoteUrlText.Text = "Remote control unavailable";
                RemoteUrlText.ToolTip = $"Ports {RemoteControlPortPreferred} and {RemoteControlPortFallback} are in use or blocked. " +
                    "Try running as administrator or closing other applications.";
                RemoteQrImage.Source = null;
            });
            Log.Error("Remote control server failed to start on any port");
        }
    }

    private static void VerifyFirewallRule(int port)
    {
        try
        {
            var ruleNames = new[] { "ChurchDisplayApp Remote", "ChurchDisplayApp Remote Fallback" };
            bool found = false;

            foreach (var ruleName in ruleNames)
            {
                var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = $"advfirewall firewall show rule name=\"{ruleName}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                proc?.WaitForExit(3000);
                var output = proc?.StandardOutput.ReadToEnd() ?? "";

                if (output.Contains(ruleName))
                {
                    found = true;
                    Log.Information("Firewall rule '{RuleName}' found — remote control accessible on port {Port}", ruleName, port);
                    break;
                }
            }

            if (!found)
            {
                Log.Warning("No firewall rule found for remote control port {Port}. " +
                    "The rule is normally created during installation. " +
                    "Users on other devices may not be able to connect. " +
                    "Reinstall the app or manually create a firewall rule for TCP port {Port}.", port);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not verify firewall rule for port {Port}", port);
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
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var props = ni.GetIPProperties();
                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(ua.Address)) continue;

                    var address = ua.Address.ToString();
                    if (address.StartsWith("169.254.")) continue;

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


    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isClosing)
        {
            e.Cancel = false;
            return;
        }

        // Prevent immediate close to allow for cleanup
        e.Cancel = true;
        _isClosing = true;

        try
        {
            Log.Information("Main window closing (Orderly Shutdown initiated)...");

            // 1. Update UI to show shutdown state if possible (optional polish)
            // if (ViewModel != null) ViewModel.StatusMessage = "Shutting down...";

            // 2. Save settings (window state, etc.)
            try
            {
                if (ActualWidth > 0)
                {
                    var sidebarWidth = RootGrid.ColumnDefinitions[0].ActualWidth;
                    _settings.MainWindowLeftColumnProportion = sidebarWidth / ActualWidth;
                }
                
                // Ensure any pending changes are saved immediately
                _settings.SaveImmediate();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not save settings during shutdown");
            }

            // 3. Stop background services
            _livePreviewTimer?.Stop();
            _progressUpdateTimer?.Stop();

            // 4. Cancel Amen Resolve if running
            if (_amenTask != null)
            {
                _amenResolveService?.Cancel();
                try { await Task.WhenAny(_amenTask, Task.Delay(1000)); } catch { }
            }
            _amenResolveService?.Dispose();

            // 5. Stop Remote Control Server
            if (_remoteControlServer != null && _remoteControlServer.IsRunning)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(AppConstants.Timeouts.ShutdownTimeoutSeconds));
                await _remoteControlServer.StopAsync(cts.Token);
            }

            // 6. Stop and dispose VLC resources
            if (_liveWindow != null)
            {
                _liveWindow.Stop();
                _liveWindow.Hide();
                _liveWindow.Dispose();
            }
            _libVLC?.Dispose();

            Log.Information("Shutdown cleanup finished.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during shutdown cleanup");
        }
        finally
        {
            Log.Information("Final process exit.");
            Application.Current.Shutdown();
        }
    }

    private void ApplySavedSidebarWidth()
    {
        try
        {
            var proportion = _settings.MainWindowLeftColumnProportion ?? 0.25;
            proportion = Math.Clamp(proportion, 0.05, 0.80);

            var pixelWidth = ActualWidth * proportion;

            var sidebarCol = RootGrid.ColumnDefinitions[0];
            pixelWidth = Math.Max(pixelWidth, sidebarCol.MinWidth);
            pixelWidth = Math.Min(pixelWidth, ActualWidth - 8 - 360);

            sidebarCol.Width = new GridLength(pixelWidth, GridUnitType.Pixel);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not restore sidebar width — using default");
        }
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        try
        {
            if (e.PreviousSize.Width <= 0 || ActualWidth <= 0) return;

            var sidebarCol = RootGrid.ColumnDefinitions[0];
            var currentProportion = sidebarCol.ActualWidth / e.PreviousSize.Width;
            var savedProportion   = _settings.MainWindowLeftColumnProportion ?? 0.25;

            if (Math.Abs(currentProportion - savedProportion) < 0.02)
            {
                var newPixelWidth = ActualWidth * savedProportion;
                newPixelWidth = Math.Max(newPixelWidth, sidebarCol.MinWidth);
                newPixelWidth = Math.Min(newPixelWidth, ActualWidth - 8 - 360);
                sidebarCol.Width = new GridLength(newPixelWidth, GridUnitType.Pixel);
            }
        }
        catch
        {
            // Non-critical
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
            
            if (_playlistManager.Items.Count > 0)
            {
                PlaylistListBox.SelectedIndex = _playlistManager.Items.Count - 1;
            }
        }
    }

    private void ToggleDisplay_Click(object sender, RoutedEventArgs e)
    {
        // If the window was closed by the user clicking X on it directly,
        // IsDisposed will be true even though _liveWindow is not null.
        // Recreate it rather than attempting Show() on a closed window.
        if (_liveWindow == null || _liveWindow.IsDisposed)
        {
            Log.Information("LiveOutputWindow is null or disposed, recreating it");
            RecreateLiveOutputWindow();
            return;
        }

        if (_liveWindow.IsVisible)
        {
            _liveWindow.Hide();
        }
        else
        {
            PositionDisplayWindow();
            _liveWindow.Show();
            _liveWindow.WindowState = WindowState.Maximized;
            _liveWindow.Topmost = true;
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
        
        _mediaControlService?.UpdateLiveWindow(_liveWindow);
        _mediaControlService?.SetVolume(ViewModel.Volume);
        
        // Reset playback state — old window was disposed, nothing is playing
        ViewModel.IsPlaying = false;
        ViewModel.CurrentMediaTitle = "Idle";
        
        PositionDisplayWindow();
        
        _liveWindow.Show();
        _liveWindow.ShowBlank();
    }

    private void CreatePlaylist_Click(object sender, RoutedEventArgs e)
    {
        var elementsWindow = new ServiceElementsWindow(_playlistManager);
        elementsWindow.Owner = this;
        elementsWindow.ShowDialog();
    }

    // Drag and Drop Event Handlers for Playlist
    private void Playlist_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
    }

    private void Playlist_Drop(object sender, DragEventArgs e) => _playlistDragDropManager?.HandlePlaylistDrop(sender, e);
    private void PlaylistListBox_DragOver(object sender, DragEventArgs e) => _playlistDragDropManager?.HandleListBoxDragOver(sender, e);
    private void PlaylistListBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) => _playlistDragDropManager?.HandleListBoxPreviewMouseLeftButtonDown(sender, e);
    private void PlaylistListBox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e) => _playlistDragDropManager?.HandleListBoxPreviewMouseMove(sender, e);
    private void PlaylistListBox_Drop(object sender, DragEventArgs e) => _playlistDragDropManager?.HandleListBoxDrop(sender, e);

    private void PlaylistListBox_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ViewModel.PlayCommand.Execute(null);
    }

    private bool _previewUpdateInProgress = false;

    private async void UpdateLivePreview(object? sender, EventArgs e)
    {
        if (_previewUpdateInProgress) return;
        _previewUpdateInProgress = true;
        try
        {
            if (_liveWindow != null && _liveWindow.IsLoaded)
            {
                var snapshot = await _liveWindow.GetCurrentSnapshotAsync();
                
                if (snapshot != null)
                {
                    PreviewImage.Source = snapshot;
                    PreviewLabel.Text = "Live Output Preview";
                }
                else
                {
                    PreviewImage.Source = null;
                    PreviewLabel.Text = "Live Output (No Media)";
                }
            }
            else
            {
                PreviewImage.Source = null;
                PreviewLabel.Text = "Live Output (Loading...)";
            }
        }
        catch (Exception ex)
        {
            PreviewImage.Source = null;
            PreviewLabel.Text = "Live Output (Preview Error)";
            Serilog.Log.Warning(ex, "Live preview snapshot failed");
        }
        finally
        {
            _previewUpdateInProgress = false;
        }
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
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
                
                if (ViewModel != null && !ViewModel.IsPlaying)
                {
                    var info = _mediaControlService.GetProgress();
                    if (info != null)
                    {
                        ViewModel.CurrentTimeStr = ViewModel.FormatTime(info.CurrentTime);
                    }
                }

                await Task.Delay(AppConstants.UI.LiveWindowSeekDelayMs);

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

    // --- Amen Button Handler (Task C) ---

    private async void AmenButton_Click(object sender, RoutedEventArgs e)
    {
        // Stop current media and clear all state (including _currentlyLoadedPath)
        ViewModel.StopCommand.Execute(null);

        // Play Amen resolve if available
        if (_amenResolveService != null)
        {
            await Task.Delay(150);
            _amenTask = Task.Run(async () =>
            {
                try
                {
                    // Use current volume or selected item volume
                    float amenVolume = (float)(ViewModel.SelectedItem?.Volume ?? ViewModel.Volume);
                    await _amenResolveService.ExecuteResolveAsync(amenVolume);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Amen resolve failed");
                }
            });
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

        var startDir = _settings.LastPlaylistSaveDirectory ?? _settings.LastMediaDirectory;
        if (!string.IsNullOrEmpty(startDir) && Directory.Exists(startDir))
            dlg.InitialDirectory = startDir;

        if (dlg.ShowDialog() == true)
        {
            try
            {
                _playlistManager.SavePlaylist(dlg.FileName);
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

    private void ClosePlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (_playlistManager.Items.Count == 0)
        {
            ViewModel.StopCommand.Execute(null);
            return;
        }

        if (!_playlistManager.IsDirty)
        {
            ViewModel.StopCommand.Execute(null);
            _playlistManager.Clear();
            PlaylistListBox.SelectedIndex = -1;
            return;
        }

        var result = MessageBox.Show(
            "The current playlist has unsaved changes.\n\nWould you like to save before closing?",
            "Save Playlist?",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel) return;

        if (result == MessageBoxResult.Yes)
        {
            var dlg = new SaveFileDialog
            {
                Filter = MediaConstants.GetPlaylistFilter(),
                DefaultExt = ".pls",
                Title = "Save Playlist Before Closing"
            };

            var startDir = _settings.LastPlaylistSaveDirectory ?? _settings.LastMediaDirectory;
            if (!string.IsNullOrEmpty(startDir) && Directory.Exists(startDir))
                dlg.InitialDirectory = startDir;

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    _playlistManager.SavePlaylist(dlg.FileName);
                    _settings.LastPlaylistSaveDirectory = Path.GetDirectoryName(dlg.FileName);
                    _settings.Save();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving playlist: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                return;
            }
        }

        ViewModel.StopCommand.Execute(null);
        _playlistManager.Clear();
        PlaylistListBox.SelectedIndex = -1;
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

    public void Amen()
    {
        Dispatcher.BeginInvoke(() => AmenButton_Click(this, new RoutedEventArgs()));
    }

    public RemoteStatus GetStatus()
    {
        var progress = _mediaControlService.GetProgress();

        if (progress == null)
        {
            return new RemoteStatus(
                ViewModel.CurrentMediaTitle,
                0,
                "00:00",
                "00:00",
                ViewModel.Volume
            );
        }

        return new RemoteStatus(
            ViewModel.CurrentMediaTitle,
            progress.ProgressPercent,
            ViewModel.FormatTime(progress.CurrentTime),
            ViewModel.FormatTime(progress.Duration),
            ViewModel.Volume
        );
    }

    public List<RemotePlaylistItem> GetPlaylistItems()
    {
        var result = new List<RemotePlaylistItem>();
        for (int i = 0; i < _playlistManager.Items.Count; i++)
        {
            var item = _playlistManager.Items[i];
            result.Add(new RemotePlaylistItem(i, item.FileName));
        }
        return result;
    }

    #endregion

    #region Media Pulse Animation

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
        if (_mediaPulseTimer != null)
        {
            _mediaPulseTimer.Stop();
            _mediaPulseTimer = null;
        }
        _mediaPulseOpacity = 0.3;
        _mediaPulseDirection = 1;
        MediaControlsContainer.Background = Brushes.White;
    }

    #endregion
}
