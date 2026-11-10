using System.IO;
using System.Windows;
using ChurchDisplayApp.Models;
using ChurchDisplayApp.Services;
using Serilog;

namespace ChurchDisplayApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    // PHASE 1: Hold splash reference for dismissal
    private SplashScreen? _splash;
    private SingleInstanceService? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Enforce single instance BEFORE showing anything. If another instance owns the
        // mutex, it is asked to surface and this process exits immediately without ever
        // creating a window or binding a port.
        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.TryAcquire())
        {
            Shutdown(0);
            return;
        }

        StartupProfiler.Instance.StartPhase("WpfStartup");
        // PHASE 1: Show splash screen FIRST (before any init)
        try
        {
            _splash = new SplashScreen("splash.png");
            _splash.Show(autoClose: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(
                $"Splash screen failed: {ex.Message}");
            _splash = null;
        }

        // Set up logging and exception handlers BEFORE base.OnStartup so they
        // are active when StartupUri (MainWindow) is constructed.
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ChurchDisplayApp",
            "logs",
            "log-.txt"
        );

        try
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }
        catch (Exception ex)
        {
            // Last-resort fallback — cannot use Serilog here
            System.Diagnostics.Trace.WriteLine($"Serilog init failed: {ex.Message}");
            Log.Logger = new LoggerConfiguration().CreateLogger(); // silent logger
        }

        Log.Information("Application starting...");

        CleanupOrphanedSnapshots();

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Log.Fatal(ex, "FATAL: Unhandled background exception (Terminating: {IsTerminating})", args.IsTerminating);

            if (args.IsTerminating)
            {
                MessageBox.Show(
                    "A fatal background error occurred and the application must close.\n\n" +
                    $"Error: {ex?.Message ?? "Unknown error"}\n\n" +
                    "Please check the logs for more details.",
                    "Fatal Application Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Stop);

                Log.CloseAndFlush();
            }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI Dispatcher exception");
            args.Handled = true; // Attempt to recover/continue
            ReportUiError(args.Exception);
        };

        // PHASE 1+2: Create MainWindow manually
        // (replaces the removed StartupUri)
        var mainWindow = new MainWindow();

        // PHASE 2: Close splash when VLC init completes (success or failure).
        // This is the PRIMARY signal — it provides the best visual transition.
        mainWindow.InitializationComplete += (s, args) =>
        {
            _splash?.Close(TimeSpan.FromSeconds(0.5));
            _splash = null;
        };

        // PHASE 1: Fallback — close splash on ContentRendered with a delay.
        // This handles the (unlikely) case where VLC init is extremely fast
        // or the event fires before the subscriber is attached.
        mainWindow.ContentRendered += (s, args) =>
        {
            Task.Delay(TimeSpan.FromSeconds(2)).ContinueWith(_ =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    _splash?.Close(TimeSpan.FromSeconds(0.5));
                    _splash = null;
                });
            });
        };

        // Surface the main window when a second launch signals this instance.
        _singleInstance.ActivationRequested += (s, args) =>
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var w = MainWindow;
                    if (w == null) return;
                    if (w.WindowState == WindowState.Minimized)
                        w.WindowState = WindowState.Normal;
                    w.Show();
                    w.Activate();
                    // Topmost toggle forces it above windows that otherwise resist Activate()
                    // (Windows blocks background focus stealing; the launcher granted us
                    // foreground rights via AllowSetForegroundWindow before signaling).
                    w.Topmost = true;
                    w.Topmost = false;
                    w.Focus();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to activate the main window for a second launch.");
                }
            }));
        };

        StartupProfiler.Instance.EndPhase("WpfStartup");
        mainWindow.Show();

        // PHASE 1: Safety timeout - close splash after 15s
        // even if ContentRendered never fires
        var splashTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        splashTimer.Tick += (s, args) =>
        {
            splashTimer.Stop();
            _splash?.Close(TimeSpan.FromSeconds(0.3));
            _splash = null;
        };
        splashTimer.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application exiting...");
        try { _singleInstance?.Dispose(); } catch { }
        Log.CloseAndFlush();
        base.OnExit(e);

        // Ensure the process actually terminates, even if background threads (VLC, ASP.NET Core) are lingering.
        Environment.Exit(e.ApplicationExitCode);
    }

    private DateTime _lastErrorDialogUtc = DateTime.MinValue;

    /// <summary>
    /// Tells the operator about an unexpected UI error without blocking the service: a status line in the main
    /// window when it is up, otherwise a dialog at most every 30 seconds (so a repeating error cannot stack dialogs).
    /// </summary>
    private void ReportUiError(Exception ex)
    {
        try
        {
            if (MainWindow is ChurchDisplayApp.MainWindow { IsLoaded: true } main)
            {
                main.ShowStatus($"Something went wrong: {ex.Message} (details are in the log). The app is still running.", isError: true);
                return;
            }

            if (DateTime.UtcNow - _lastErrorDialogUtc < TimeSpan.FromSeconds(30))
                return;

            _lastErrorDialogUtc = DateTime.UtcNow;
            MessageBox.Show(
                "An unexpected error occurred in the user interface:\n\n" +
                $"{ex.Message}\n\n" +
                "The application will attempt to continue. If you continue to see this error, please restart the application.",
                "Unexpected UI Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception reportEx)
        {
            Log.Warning(reportEx, "Could not show the UI error to the operator");
        }
    }

    private static void CleanupOrphanedSnapshots()
    {
        try
        {
            var tempPath = Path.GetTempPath();
            var pattern = $"{AppConstants.Media.SnapshotPrefix}*{AppConstants.Media.SnapshotExtension}";
            foreach (var file in Directory.GetFiles(tempPath, pattern))
            {
                try { File.Delete(file); }
                catch { /* File may be in use or locked */ }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to clean up orphaned snapshots");
        }
    }
}

