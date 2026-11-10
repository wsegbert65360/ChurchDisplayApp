using Microsoft.Win32;
using System.IO;
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
    private readonly AppSettings _settings;
    private readonly LivePreviewController _livePreview;
    private readonly RemoteControlCoordinator _remoteControl;
    // PHASE 2: Nullable — set asynchronously when VLC init completes
    private LibVLC? _libVLC;
    private LiveOutputWindow? _liveWindow;
    private MediaControlService? _mediaControlService;

    private readonly PlaylistManager _playlistManager = new();
    public PlaylistManager PlaylistManager => _playlistManager;
    public MainViewModel? ViewModel { get; private set; }
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

    private const string BaseTitle = "Church Display Control";
    private DispatcherTimer? _statusTimer;
    private readonly DispatcherTimer _autosaveTimer;
    private (PlaylistItem Item, int Index)? _lastRemoved;

    // PHASE 2: VLC readiness tracking
    private volatile bool _vlcReady = false;
    private bool _vlcInitFailed = false;
    private string? _vlcInitError;

    // PHASE 2: Cancellation support — prevents native resource leaks if app
    // is closed while VLC is still initializing on the background thread.
    private CancellationTokenSource? _vlcInitCts;
    private Task? _vlcInitTask;

    // PHASE 2: Event to signal App.xaml.cs that init is complete (success OR failure).
    // Named "InitializationComplete" (not "VlcReady") because it fires on both paths.
    public event EventHandler? InitializationComplete;

    public MainWindow()
    {
        StartupProfiler.Instance.StartPhase("MainWindowInit");
        StartupProfiler.Instance.StartPhase("AppSettingsLoad");
        _settings = AppSettings.Current;
        StartupProfiler.Instance.EndPhase("AppSettingsLoad");

        InitializeComponent();
        _remoteControl = new RemoteControlCoordinator(Dispatcher, this);
        LoadingStatusText.Text = "Initializing application...";

        // Bind playlist (works immediately — no VLC dependency)
        PlaylistListBox.ItemsSource = _playlistManager.Items;

        // Keep the window title / playlist header in step with the playlist's name and unsaved state.
        _playlistManager.IsDirtyChanged += (s, e) => UpdatePlaylistTitle();
        _playlistManager.CurrentFilePathChanged += (s, e) => UpdatePlaylistTitle();
        _playlistManager.Items.CollectionChanged += (s, e) => UpdatePlaylistTitle();

        // Crash recovery: snapshot an unsaved playlist every 30 seconds.
        _playlistManager.RecoveryPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppConstants.Storage.AppDataFolderName,
            AppConstants.Storage.RecoveryFileName);
        _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _autosaveTimer.Tick += (s, e) =>
        {
            if (_playlistManager.IsDirty)
                _playlistManager.SaveRecovery();
        };
        _autosaveTimer.Start();

        // A USB drive may have been plugged in or removed while another window had focus.
        Activated += async (s, e) => await _playlistManager.RefreshMissingAsync();

        Closing += MainWindow_Closing;
        SizeChanged += MainWindow_SizeChanged;

        // PHASE 2: Set a minimal DataContext so XAML bindings don't throw.
        // This is a temporary placeholder until the real ViewModel is created.
        DataContext = new LoadingPlaceholderViewModel();

        _playlistDragDropManager = new PlaylistDragDropManager(PlaylistListBox, _playlistManager, () => { });

        // Live preview (safe before VLC is ready — it shows "Loading..." until the live window exists)
        _livePreview = new LivePreviewController(() => _liveWindow, PreviewImage, PreviewLabel);
        _livePreview.Start();

        // PHASE 2: Start VLC initialization on a background thread.
        // Store the task and CTS so we can cancel it during shutdown.
        _vlcInitCts = new CancellationTokenSource();
        _vlcInitTask = InitializeVlcAsync(_vlcInitCts.Token);

        StartupProfiler.Instance.EndPhase("MainWindowInit");
    }

    /// <summary>
    /// PHASE 2: Initializes VLC on a background thread, then dispatches back
    /// to the UI thread to create the ViewModel and LiveOutputWindow.
    /// </summary>
    /// <param name="ct">Cancellation token — set when app is closing to prevent resource leaks.</param>
    private async Task InitializeVlcAsync(CancellationToken ct)
    {
        LibVLC? libVLC = null;
        try
        {
            // --- Step A: Heavy VLC init on background thread ---
            // Core.Initialize() loads native libraries and can take 1-3 seconds.
            // new LibVLC() creates the engine instance.
            // These MUST run on the same thread (VLC thread affinity requirement).
            libVLC = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                StartupProfiler.Instance.StartPhase("AssemblyLoad");
                try
                {
                    var vlcAssembly = System.Runtime.Loader.AssemblyLoadContext.Default
                        .LoadFromAssemblyName(new System.Reflection.AssemblyName("LibVLCSharp"));
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("[VLC] Explicit assembly load failed, using default");
                }
                StartupProfiler.Instance.EndPhase("AssemblyLoad");

                StartupProfiler.Instance.StartPhase("VlcInitialization");
                // BeginInvoke (non-blocking) to avoid deadlock: the UI thread may
                // be blocked by _vlcInitTask.Wait() during shutdown.  Invoke() would
                // deadlock because both threads would wait on each other.
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() => LoadingStatusText.Text = "Loading media engine...");

                LibVLCSharp.Shared.Core.Initialize();

                ct.ThrowIfCancellationRequested();

                var vlcOptionsList = new List<string>
                {
                    "--no-osd",
                    "--no-video-title-show",
                    "--no-snapshot-preview",
                    "--aout=directsound"
                };

                if (!string.IsNullOrWhiteSpace(_settings.VlcAudioDevice))
                {
                    vlcOptionsList.Add($"--directx-audio-device={_settings.VlcAudioDevice}");
                }

                var libVlc = new LibVLC(vlcOptionsList.ToArray());

                // Subscribe to VLC log on background thread (logging is thread-safe)
                libVlc.Log += (sender, e) =>
                {
                    var message = $"[VLC/{e.Module}] {e.Message}";
                    switch (e.Level)
                    {
                        case LogLevel.Error:   Log.Error(message);   break;
                        case LogLevel.Warning: Log.Warning(message); break;
                        default:               Log.Debug(message); break;
                    }
                };

                return libVlc;
            }, ct);

            // Check cancellation before dispatching to UI thread
            ct.ThrowIfCancellationRequested();

            // --- Step B: Dispatch back to UI thread for WPF object creation ---
            await Dispatcher.BeginInvoke(() =>
            {
                // RELIABILITY FIX #1: If the app is shutting down after
                // VLC was created on the background thread but before we got here,
                // we MUST dispose the newly-created LibVLC instance to prevent
                // a native resource leak.
                if (_isClosing || ct.IsCancellationRequested)
                {
                    libVLC.Dispose();
                    return;
                }

                _libVLC = libVLC;

                if (!string.IsNullOrWhiteSpace(_settings.VlcAudioDevice))
                {
                    Log.Information("VLC audio device override: {Device}", _settings.VlcAudioDevice);
                }

                // Create LiveOutputWindow (must be on UI thread — it's a WPF Window)
                StartupProfiler.Instance.StartPhase("LiveOutputWindow");
                LoadingStatusText.Text = "Configuring display output...";
                _liveWindow = new LiveOutputWindow(_libVLC);
                StartupProfiler.Instance.EndPhase("LiveOutputWindow");

                // Create MediaControlService and ViewModel
                _mediaControlService = new MediaControlService(_liveWindow, _settings);
                _mediaControlService.MediaStateChanged += (s, e) =>
                {
                    if (_mediaControlService.IsPlaying()) StartMediaPulseAnimation();
                    else StopMediaPulseAnimation();
                };

                ViewModel = new MainViewModel(_playlistManager, _mediaControlService, _settings);
                DataContext = ViewModel;

                StartupProfiler.Instance.EndPhase("VlcInitialization");

                // Subscribe to playback lifecycle events
                _liveWindow.MediaEnded += (s, e) => ViewModel.StopCommand.Execute(null);
                _liveWindow.MediaError += (s, e) =>
                {
                    Log.Warning("Live output media error detected. Forcing ViewModel stop-state reconciliation.");
                    ViewModel.StopCommand.Execute(null);
                };

                // Restore sidebar width
                ApplySavedSidebarWidth();

                // Detect monitors and show live output window
                var monitors = _monitorService.GetMonitors();
                if (monitors.Count >= 2)
                {
                    var liveMonitor = _monitorService.SelectDisplayMonitor(monitors, _settings);
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

                // Start progress update timer
                _progressUpdateTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(AppConstants.UI.ProgressUpdateIntervalMs)
                };
                _progressUpdateTimer.Tick += (s, args) => ViewModel.UpdateProgress();
                _progressUpdateTimer.Start();

                LoadingStatusText.Text = "Starting remote control...";
                // Start remote control server
                _ = StartRemoteControlAsync();

                // Initialize AmenResolveService
                var soundFontPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    AppConstants.Media.SoundsDirectory,
                    AppConstants.Media.SoundFontFileName);
                if (System.IO.File.Exists(soundFontPath))
                {
                    _amenResolveService = new AmenResolveService(soundFontPath);
                    _ = _amenResolveService.PreloadAsync(); // so the first Amen press has no delay
                }
                else
                {
                    Log.Warning("SoundFont file not found at {Path}. Amen resolve feature will be unavailable.", soundFontPath);
                }

                // RELIABILITY FIX #6: Set _vlcReady LAST, after ALL post-init work
                // is complete. This ensures no code path can observe "VLC is ready"
                // before the remote control server, amen service, timers, etc. are running.
                _vlcReady = true;
                LoadingStatusText.Text = "Ready!";
                LoadingOverlay.Visibility = Visibility.Collapsed;

                // Signal App.xaml.cs to close splash (if still visible)
                InitializationComplete?.Invoke(this, EventArgs.Empty);

                Log.Information("VLC initialization complete — media engine ready.");

                if (_settings.LoadNotice != null)
                    ShowStatus(_settings.LoadNotice, isError: true);

                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(OfferPlaylistRecovery));
            });
        }
        catch (OperationCanceledException)
        {
            // RELIABILITY FIX #1: App is shutting down during VLC init.
            // Dispose any LibVLC created on the background thread that wasn't
            // consumed by the UI thread dispatcher callback.
            libVLC?.Dispose();
            Log.Information("VLC initialization cancelled — app is shutting down.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize VLC media engine");

            await Dispatcher.BeginInvoke(() =>
            {
                // RELIABILITY FIX #5: Check _isClosing here too.
                if (_isClosing)
                {
                    InitializationComplete?.Invoke(this, EventArgs.Empty);
                    return;
                }

                _vlcInitFailed = true;
                _vlcInitError = ex.Message;

                // Show error state in the loading overlay
                LoadingStatusText.Text = $"Media engine failed to load.\n\n{ex.Message}\n\nCheck that the Microsoft Visual C++ Redistributable (x64) is installed.";

                // Hide progress bar, show the pre-declared retry button
                LoadingProgressBar.Visibility = Visibility.Collapsed;
                LoadingRetryButton.Visibility = Visibility.Visible;

                // Signal splash to close even on failure
                InitializationComplete?.Invoke(this, EventArgs.Empty);
            });
        }
    }

    /// <summary>
    /// PHASE 2: Handles the Retry button click when VLC init fails.
    /// Re-triggers VLC initialization with a fresh CancellationToken.
    /// </summary>
    private void LoadingRetryButton_Click(object sender, RoutedEventArgs e)
    {
        // RELIABILITY FIX #2: Cancel any previous init attempt before retrying.
        _vlcInitCts?.Cancel();
        _vlcInitCts?.Dispose();

        // Tear down anything the failed attempt left behind so the retry does not leak native handles
        // or leave a stale black window on the projector.
        _progressUpdateTimer?.Stop();
        _progressUpdateTimer = null;
        if (_liveWindow != null)
        {
            _liveWindow.Dispose();
            _liveWindow.Close(); // allowed now that it is disposed
            _liveWindow = null;
        }
        _mediaControlService = null;
        _libVLC?.Dispose();
        _libVLC = null;

        // Reset error state
        _vlcInitFailed = false;
        _vlcInitError = null;

        // Restore loading overlay to its initial visual state
        LoadingStatusText.Text = "Retrying...";
        LoadingProgressBar.Visibility = Visibility.Visible;
        LoadingRetryButton.Visibility = Visibility.Collapsed;

        // Start a fresh VLC init attempt with a new CancellationToken
        _vlcInitCts = new CancellationTokenSource();
        _vlcInitTask = InitializeVlcAsync(_vlcInitCts.Token);
    }


    private async Task StartRemoteControlAsync()
    {
        StartupProfiler.Instance.StartPhase("KestrelStart");

        var port = await _remoteControl.StartAsync();
        if (port is int activePort)
        {
            var url = RemoteControlCoordinator.GetRemoteUrl(activePort);
            RemoteUrlText.Text = url;
            RemoteQrImage.Source = RemoteControlCoordinator.CreateQrImage(url);
        }
        else
        {
            RemoteUrlText.Text = "Remote control unavailable";
            RemoteUrlText.ToolTip =
                $"Ports {AppConstants.Network.RemoteControlPortPreferred} and {AppConstants.Network.RemoteControlPortFallback} are in use or blocked. " +
                "Try running as administrator or closing other applications.";
            RemoteQrImage.Source = null;
        }

        StartupProfiler.Instance.EndPhase("KestrelStart");
        StartupProfiler.Instance.DumpSummary();
    }

    /// <summary>
    /// Configures a FileDialog's InitialDirectory based on user settings.
    /// Prefers the playlist directory for playlist operations, otherwise uses media directory.
    /// </summary>
    private void ConfigureDialogDirectory(Microsoft.Win32.FileDialog dlg, bool preferPlaylistDir = false)
    {
        if (preferPlaylistDir && !string.IsNullOrEmpty(_settings.LastPlaylistDirectory) && Directory.Exists(_settings.LastPlaylistDirectory))
        {
            dlg.InitialDirectory = _settings.LastPlaylistDirectory;
        }
        else if (!string.IsNullOrEmpty(_settings.LastMediaDirectory) && Directory.Exists(_settings.LastMediaDirectory))
        {
            dlg.InitialDirectory = _settings.LastMediaDirectory;
        }
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isClosing)
        {
            e.Cancel = false;
            return;
        }

        if (!ConfirmDiscardOrSavePlaylist())
        {
            e.Cancel = true;
            return;
        }

        // Prevent immediate close to allow for cleanup
        e.Cancel = true;
        _isClosing = true;

        // The operator has either saved or chosen to discard, so the crash-recovery snapshot is obsolete.
        _autosaveTimer.Stop();
        _playlistManager.DeleteRecovery();

        // PHASE 2 RELIABILITY FIX #1: Cancel VLC init if it's still running.
        // This prevents a race where VLC is created on the background thread
        // after the user closes the app, leaking native resources.
        _vlcInitCts?.Cancel();

        // Wait for VLC init to finish (or cancel).  Use await instead of
        // .Wait() to avoid deadlock: if VLC init is blocked on a
        // Dispatcher.BeginInvoke callback, the UI thread must remain free
        // to process that callback.
        try
        {
            if (_vlcInitTask != null)
                await Task.WhenAny(_vlcInitTask, Task.Delay(TimeSpan.FromSeconds(5)));
        }
        catch (Exception ex) when (ex is OperationCanceledException or AggregateException)
        {
            // Cancellation is expected during shutdown
        }

        try
        {
            Log.Information("Main window closing (Orderly Shutdown initiated)...");

            // 1. Save settings (window state, etc.)
            try
            {
                if (ActualWidth > 0)
                {
                    var sidebarWidth = RootGrid.ColumnDefinitions[0].ActualWidth;
                    _settings.MainWindowLeftColumnProportion = sidebarWidth / ActualWidth;
                }
                
                // Ensure any pending changes are saved immediately
                _settings.SaveImmediate(force: true);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not save settings during shutdown");
            }

            // 2. Stop background services
            _livePreview.Stop();
            _progressUpdateTimer?.Stop();

            // 3. Cancel Amen Resolve if running
            if (_amenTask != null)
            {
                _amenResolveService?.Cancel();
                try { await Task.WhenAny(_amenTask, Task.Delay(1000)); } catch { }
            }
            _amenResolveService?.Dispose();

            // 4. Stop Remote Control Server
            await _remoteControl.StopAsync(TimeSpan.FromSeconds(AppConstants.Timeouts.ShutdownTimeoutSeconds));

            // 5. Stop and dispose VLC resources
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
        catch (Exception ex)
        {
            // Non-critical: the sidebar just keeps its current width.
            Log.Debug(ex, "Sidebar resize adjustment skipped");
        }
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = MediaConstants.GetAllMediaFilter(),
            Multiselect = true
        };

        ConfigureDialogDirectory(dlg, preferPlaylistDir: false);

        if (dlg.ShowDialog() == true)
        {
            _playlistManager.AddFiles(dlg.FileNames);
            _settings.LastMediaDirectory = Path.GetDirectoryName(dlg.FileNames[0]);
            _settings.Save();
            
            if (_playlistManager.Items.Count > 0)
            {
                PlaylistListBox.SelectedIndex = _playlistManager.Items.Count - 1;
            }
        }
    }

    private void ToggleDisplay_Click(object sender, RoutedEventArgs e)
    {
        if (EnsureVlcReady() == null) return;

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

    /// <summary>Right-click menu on the Display button: let the operator pick the projector monitor again.</summary>
    private void ChooseDisplayMonitor_Click(object sender, RoutedEventArgs e)
    {
        if (EnsureVlcReady() == null) return;

        var monitors = _monitorService.GetMonitors();
        if (monitors.Count < 2)
        {
            ShowStatus("Only one monitor is connected, so there is nothing to choose.", isError: true);
            return;
        }

        if (_liveWindow == null || _liveWindow.IsDisposed)
        {
            RecreateLiveOutputWindow(forceAskMonitor: true);
            return;
        }

        PositionDisplayWindow(forceAskMonitor: true);
        if (!_liveWindow.IsVisible)
            _liveWindow.Show();
        _liveWindow.WindowState = WindowState.Maximized;
        _liveWindow.Topmost = true;
    }

    private void PositionDisplayWindow(bool forceAskMonitor = false)
    {
        if (_liveWindow == null) return;

        var monitors = _monitorService.GetMonitors();
        if (monitors.Count >= 2)
        {
            var liveMonitor = _monitorService.SelectDisplayMonitor(monitors, _settings, forceAskMonitor);
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

    private void RecreateLiveOutputWindow(bool forceAskMonitor = false)
    {
        _liveWindow?.Dispose();
        
        if (_libVLC == null) return; // PHASE 2: Safety guard

        _liveWindow = new LiveOutputWindow(_libVLC);
        
        if (ViewModel != null)
        {
            _liveWindow.MediaEnded += (s, e) => ViewModel.StopCommand.Execute(null);
            _liveWindow.MediaError += (s, e) =>
            {
                Log.Warning("Live output media error detected on recreated window. Forcing stop-state reconciliation.");
                ViewModel.StopCommand.Execute(null);
            };
            _mediaControlService?.UpdateLiveWindow(_liveWindow);
            _mediaControlService?.SetVolume(ViewModel.Volume);
            ViewModel.ReconcilePlaybackStopped("Live output window recreated");
        }
        
        PositionDisplayWindow(forceAskMonitor);
        
        _liveWindow.Show();
        _liveWindow.ShowBlank();
    }

    private void CreatePlaylist_Click(object sender, RoutedEventArgs e)
    {
        var elementsWindow = new ServiceElementsWindow(_playlistManager, ConfirmDiscardOrSavePlaylist)
        {
            Owner = this
        };
        elementsWindow.ShowDialog();

        if (elementsWindow.AddedCount > 0)
        {
            _lastRemoved = null;
            ShowStatus($"Added {elementsWindow.AddedCount} item(s) to the playlist");
        }
    }

    // Drag and Drop Event Handlers for Playlist
    private void PlaylistListBox_DragOver(object sender, DragEventArgs e) => _playlistDragDropManager?.HandleListBoxDragOver(sender, e);
    private void PlaylistListBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) => _playlistDragDropManager?.HandleListBoxPreviewMouseLeftButtonDown(sender, e);
    private void PlaylistListBox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e) => _playlistDragDropManager?.HandleListBoxPreviewMouseMove(sender, e);
    private void PlaylistListBox_Drop(object sender, DragEventArgs e) => _playlistDragDropManager?.HandleListBoxDrop(sender, e);

    private void PlaylistListBox_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var vm = EnsureVlcReady();
        if (vm == null) return;
        vm.PlayCommand.Execute(null);
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e) => RemoveSelectedItem();

    /// <summary>
    /// Removes the selected playlist item. No confirmation (Ctrl+Z brings it back), except for the item that
    /// is playing right now, because removing it stops the projection.
    /// </summary>
    private void RemoveSelectedItem()
    {
        if (PlaylistListBox.SelectedItem is not PlaylistItem item)
            return;

        var index = _playlistManager.Items.IndexOf(item);
        var isNowPlaying = ViewModel != null && ViewModel.NowPlayingIndex == index;

        if (isNowPlaying)
        {
            var answer = MessageBox.Show(
                $"'{item.FileName}' is playing now. Remove it and stop playback?",
                "Remove Playing Item",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
                return;
        }

        _playlistManager.RemoveItem(item);
        _lastRemoved = (item, index);

        if (_playlistManager.Items.Count > 0)
            PlaylistListBox.SelectedIndex = Math.Min(index, _playlistManager.Items.Count - 1);

        ShowStatus($"Removed '{item.FileName}'. Press Ctrl+Z to undo.");
    }

    private void UndoRemove()
    {
        if (_lastRemoved is not { } removed)
            return;

        _lastRemoved = null;
        var index = Math.Clamp(removed.Index, 0, _playlistManager.Items.Count);
        _playlistManager.Items.Insert(index, removed.Item);
        PlaylistListBox.SelectedItem = removed.Item;
        ShowStatus($"Restored '{removed.Item.FileName}'");
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
                    var info = _mediaControlService?.GetProgress();
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
        var vm = EnsureVlcReady();
        if (vm == null) return;

        // Capture the volume of what is playing now; Stop clears the now-playing item.
        float amenVolume = (float)vm.Volume;

        // Stop current media and clear all state (including _currentlyLoadedPath)
        vm.StopCommand.Execute(null);

        // Play Amen resolve if available
        if (_amenResolveService != null)
        {
            await Task.Delay(150);
            _amenTask = Task.Run(async () =>
            {
                try
                {
                    await _amenResolveService.ExecuteResolveAsync(amenVolume);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Amen resolve failed");
                }
            });
        }
    }

    private void SavePlaylist_Click(object sender, RoutedEventArgs e) => SavePlaylistInteractive(forceSaveAs: false);

    private void SavePlaylistAs_Click(object sender, RoutedEventArgs e) => SavePlaylistInteractive(forceSaveAs: true);

    private void LoadPlaylist_Click(object sender, RoutedEventArgs e) => OpenPlaylistFromDialog();

    /// <summary>
    /// Saves the playlist. Saves straight to the current file when there is one, otherwise (or for Save As)
    /// asks where to save. Shows a short status message instead of a pop-up on success.
    /// </summary>
    /// <returns>True when the playlist was saved.</returns>
    private bool SavePlaylistInteractive(bool forceSaveAs)
    {
        if (_playlistManager.Items.Count == 0)
        {
            ShowStatus("The playlist is empty. Add files before saving.", isError: true);
            return false;
        }

        var target = _playlistManager.CurrentFilePath;

        if (forceSaveAs || string.IsNullOrEmpty(target))
        {
            var dlg = new SaveFileDialog
            {
                Title = "Save Playlist",
                Filter = MediaConstants.GetPlaylistSaveFilter(),
                DefaultExt = MediaConstants.PlaylistExtension,
                AddExtension = true,
                OverwritePrompt = true
            };

            ConfigureDialogDirectory(dlg, preferPlaylistDir: true);

            if (!string.IsNullOrEmpty(target))
            {
                var currentDir = Path.GetDirectoryName(target);
                if (currentDir != null && Directory.Exists(currentDir))
                    dlg.InitialDirectory = currentDir;
                dlg.FileName = Path.GetFileNameWithoutExtension(target);
            }
            else
            {
                dlg.FileName = $"Service {DateTime.Now:yyyy-MM-dd}";
            }

            if (dlg.ShowDialog() != true)
                return false;

            target = dlg.FileName;
        }

        if (string.IsNullOrEmpty(target))
            return false;

        try
        {
            _playlistManager.SavePlaylist(target);
            _settings.LastPlaylistDirectory = Path.GetDirectoryName(target);
            _settings.Save();
            ShowStatus($"Saved {Path.GetFileName(target)}");
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not save playlist to {Path}", target);
            MessageBox.Show($"Error saving playlist: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void OpenPlaylistFromDialog()
    {
        if (!ConfirmDiscardOrSavePlaylist())
            return;

        var dlg = new OpenFileDialog
        {
            Title = "Open Playlist",
            Filter = MediaConstants.GetPlaylistOpenFilter()
        };

        ConfigureDialogDirectory(dlg, preferPlaylistDir: true);

        if (dlg.ShowDialog() == true)
            LoadPlaylistFile(dlg.FileName);
    }

    private void LoadPlaylistFile(string path)
    {
        try
        {
            var problems = _playlistManager.LoadPlaylist(path);
            _lastRemoved = null;
            _settings.LastPlaylistDirectory = Path.GetDirectoryName(path);
            _settings.Save();

            var name = Path.GetFileName(path);
            var count = _playlistManager.Items.Count;
            if (problems.Count > 0)
            {
                ShowStatus(
                    $"Opened {name}: {count} item(s), {problems.Count} could not be found. Missing files are shown in red and stay in the playlist.",
                    isError: true);
            }
            else
            {
                ShowStatus($"Opened {name} ({count} item(s))");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not open playlist {Path}", path);
            MessageBox.Show($"Error opening playlist: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClosePlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (_playlistManager.Items.Count == 0)
        {
            ViewModel?.StopCommand.Execute(null);
            return;
        }

        if (!ConfirmDiscardOrSavePlaylist())
            return;

        ViewModel?.StopCommand.Execute(null);
        _playlistManager.Clear();
        _lastRemoved = null;
        PlaylistListBox.SelectedIndex = -1;
    }

    /// <returns>False when the operator cancelled the action.</returns>
    private bool ConfirmDiscardOrSavePlaylist()
    {
        // Nothing to lose when the list is empty or unchanged.
        if (!_playlistManager.IsDirty || _playlistManager.Items.Count == 0)
            return true;

        var name = _playlistManager.DisplayName ?? "The current playlist";
        var result = MessageBox.Show(
            $"'{name}' has unsaved changes.\n\nWould you like to save before continuing?",
            "Save Playlist?",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel)
            return false;

        if (result != MessageBoxResult.Yes)
            return true;

        return SavePlaylistInteractive(forceSaveAs: false);
    }

    /// <summary>
    /// Offers to restore the playlist that was open when the app last closed unexpectedly.
    /// </summary>
    private void OfferPlaylistRecovery()
    {
        if (_isClosing || !_playlistManager.HasRecovery || _playlistManager.Items.Count > 0)
            return;

        var answer = MessageBox.Show(
            this,
            "The app closed unexpectedly last time with an unsaved playlist.\n\nWould you like to restore it?",
            "Restore Playlist?",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        try
        {
            if (answer == MessageBoxResult.Yes)
            {
                _playlistManager.RestoreRecovery();
                ShowStatus($"Restored your unsaved playlist ({_playlistManager.Items.Count} item(s)). Press Ctrl+S to save it.");
            }
            else
            {
                _playlistManager.DeleteRecovery();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not restore the recovery playlist");
            MessageBox.Show($"The recovered playlist could not be loaded: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            _playlistManager.DeleteRecovery();
        }
    }

    /// <summary>Shows the playlist name (and a dot when it has unsaved changes) in the title bar and sidebar header.</summary>
    private void UpdatePlaylistTitle()
    {
        var name = _playlistManager.DisplayName;
        var dirtyMark = _playlistManager.IsDirty ? " •" : string.Empty;

        Title = name == null ? BaseTitle : $"{name}{dirtyMark} — {BaseTitle}";
        PlaylistNameText.Text = name == null ? "📂 PLAYLIST" : $"📂 {name}{dirtyMark}";
    }

    /// <summary>Shows a short message under the playlist buttons that disappears by itself.</summary>
    internal void ShowStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError ? Brushes.Firebrick : Brushes.DarkGreen;
        StatusText.Visibility = Visibility.Visible;

        _statusTimer ??= new DispatcherTimer();
        _statusTimer.Stop();
        _statusTimer.Interval = TimeSpan.FromSeconds(isError ? 10 : 5);
        _statusTimer.Tick -= StatusTimer_Tick;
        _statusTimer.Tick += StatusTimer_Tick;
        _statusTimer.Start();
    }

    private void StatusTimer_Tick(object? sender, EventArgs e)
    {
        _statusTimer?.Stop();
        StatusText.Text = string.Empty;
        StatusText.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Handles the operator shortcuts at window level so they work whichever control has focus
    /// (a focused button or slider would otherwise swallow Space and the arrow keys).
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Handled || Keyboard.FocusedElement is TextBox)
            return;

        // A held key auto-repeats; during a service that would skip several songs or toggle play/pause rapidly.
        if (e.IsRepeat)
        {
            if (e.Key is Key.Space or Key.Left or Key.Right or Key.B or Key.Delete)
                e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;

        if (modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.S: SavePlaylistInteractive(forceSaveAs: false); e.Handled = true; break;
                case Key.O: OpenPlaylistFromDialog(); e.Handled = true; break;
                case Key.Z: UndoRemove(); e.Handled = true; break;
            }
            return;
        }

        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (e.Key == Key.S)
            {
                SavePlaylistInteractive(forceSaveAs: true);
                e.Handled = true;
            }
            return;
        }

        if (modifiers != ModifierKeys.None)
            return;

        var vm = _vlcReady ? ViewModel : null;

        switch (e.Key)
        {
            case Key.Space: vm?.TogglePlayPauseCommand.Execute(null); e.Handled = true; break;
            case Key.Right: vm?.NextCommand.Execute(null); e.Handled = true; break;
            case Key.Left: vm?.PreviousCommand.Execute(null); e.Handled = true; break;
            case Key.B: vm?.BlankCommand.Execute(null); e.Handled = true; break;
            case Key.F11: vm?.ToggleFullscreenCommand.Execute(null); e.Handled = true; break;
            case Key.Escape: vm?.ExitFullscreenCommand.Execute(null); e.Handled = true; break;
            case Key.Delete:
                if (PlaylistListBox.IsKeyboardFocusWithin)
                {
                    RemoveSelectedItem();
                    e.Handled = true;
                }
                break;
        }
    }

    #region IDisplayController Implementation

    public void Next()
    {
        var vm = EnsureVlcReady();
        if (vm == null) return;
        vm.NextCommand.Execute(null);
    }
    public void Previous()
    {
        var vm = EnsureVlcReady();
        if (vm == null) return;
        vm.PreviousCommand.Execute(null);
    }
    public void Play()
    {
        var vm = EnsureVlcReady();
        if (vm == null) return;
        vm.PlayCommand.Execute(null);
    }
    public void Pause()
    {
        var vm = EnsureVlcReady();
        if (vm == null) return;
        vm.PauseCommand.Execute(null);
    }
    public void Stop()
    {
        var vm = EnsureVlcReady();
        if (vm == null) return;
        vm.StopCommand.Execute(null);
    }
    public void Blank()
    {
        var vm = EnsureVlcReady();
        if (vm == null) return;
        vm.BlankCommand.Execute(null);
    }
    public void SetVolume(double volume)
    {
        var vm = EnsureVlcReady();
        if (vm == null || !double.IsFinite(volume)) return;
        var clamped = Math.Clamp(volume, 0.0, 1.0);
        Log.Debug("IDisplayController.SetVolume called with {Volume:P0} (clamped to {Clamped:P0})", volume, clamped);
        vm.Volume = clamped;
    }
    public void VolumeUp()
    {
        var vm = EnsureVlcReady();
        if (vm == null) return;
        vm.Volume = Math.Clamp(vm.Volume + 0.02, 0.0, 1.0);
    }
    public void VolumeDown()
    {
        var vm = EnsureVlcReady();
        if (vm == null) return;
        vm.Volume = Math.Clamp(vm.Volume - 0.02, 0.0, 1.0);
    }
    public RemotePlayResult PlayIndex(int index, int? expectedPlaylistVersion = null)
    {
        var vm = EnsureVlcReady();
        if (vm == null) return RemotePlayResult.NotReady;

        // The remote's list is out of date: the operator added, removed, or reordered items since it loaded.
        if (expectedPlaylistVersion is int expected && expected != _playlistManager.Version)
        {
            Log.Information("Remote play of index {Index} rejected: playlist version {Expected} is stale (now {Current})",
                index, expected, _playlistManager.Version);
            return RemotePlayResult.StalePlaylist;
        }

        if (index < 0 || index >= _playlistManager.Items.Count)
            return RemotePlayResult.NotFound;

        vm.SelectedItem = _playlistManager.Items[index];
        vm.PlayCommand.Execute(null);
        return RemotePlayResult.Started;
    }

    public void Amen()
    {
        // Already on the UI thread (the remote server dispatches here).
        if (EnsureVlcReady() == null) return;
        AmenButton_Click(this, new RoutedEventArgs());
    }

    public RemoteStatus GetStatus()
    {
        if (ViewModel != null && _mediaControlService != null)
        {
            var progress = _mediaControlService.GetProgress();

            return new RemoteStatus(
                ViewModel.CurrentMediaTitle,
                progress?.ProgressPercent ?? 0,
                progress != null ? ViewModel.FormatTime(progress.CurrentTime) : "00:00",
                progress != null ? ViewModel.FormatTime(progress.Duration) : "00:00",
                ViewModel.Volume,
                ViewModel.NowPlayingIndex,
                ViewModel.IsPlaying,
                ViewModel.IsBlanked,
                _playlistManager.Version
            );
        }

        return new RemoteStatus("Loading...", 0, "00:00", "00:00", 0.5, PlaylistVersion: _playlistManager.Version);
    }

    public RemotePlaylist GetPlaylist()
    {
        var items = _playlistManager.Items
            .Select((item, i) => new RemotePlaylistItem(i, item.FileName))
            .ToList();
        return new RemotePlaylist(_playlistManager.Version, items);
    }

    /// <summary>
    /// Guards VLC-dependent actions. Returns the ViewModel if VLC is ready,
    /// or null if VLC is still initializing or failed to initialize.
    /// Callers should check for null and return early.
    /// </summary>
    private MainViewModel? EnsureVlcReady()
    {
        if (_vlcReady && ViewModel != null)
            return ViewModel;

        if (_vlcInitFailed)
        {
            Log.Warning("VLC action blocked: media engine initialization failed.");
        }
        else
        {
            Log.Information("VLC action blocked: media engine still initializing.");
        }
        return null;
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
