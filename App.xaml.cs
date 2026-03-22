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
            .MinimumLevel.Debug()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information("Application starting...");

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Log.Fatal((Exception)args.ExceptionObject, "Unhandled AppDomain exception");
            Log.CloseAndFlush();
        };

        DispatcherUnhandledException += (s, args) =>
        {
            Log.Fatal(args.Exception, "Unhandled Dispatcher exception");
            Log.CloseAndFlush();
            
            MessageBox.Show(
                $"An unexpected error occurred and the application must close.\n\n" +
                $"Error: {args.Exception.Message}\n\n" +
                $"Details have been saved to the log file.",
                "Unexpected Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            args.Handled = true;
            Current.Shutdown(1);
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

