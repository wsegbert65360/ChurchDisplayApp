using System.IO;
using System.Windows;
using Serilog;

namespace ChurchDisplayApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ChurchDisplayApp",
            "logs",
            "log-.txt"
        );

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information("Application starting...");

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
            
            MessageBox.Show(
                "An unexpected error occurred in the user interface:\n\n" +
                $"{args.Exception.Message}\n\n" +
                "The application will attempt to continue. If you continue to see this error, please restart the application.",
                "Unexpected UI Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            args.Handled = true; // Attempt to recover/continue
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application exiting...");
        Log.CloseAndFlush();
        base.OnExit(e);

        // Ensure the process actually terminates, even if background threads (VLC, ASP.NET Core) are lingering.
        Environment.Exit(e.ApplicationExitCode);
    }
}

