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
            args.Handled = false;
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application exiting...");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}

