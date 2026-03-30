namespace ChurchDisplayApp.Models;

/// <summary>
/// Centralized location for application-wide constants.
/// </summary>
public static class AppConstants
{
    public static class Network
    {
        public const int RemoteControlPortPreferred = 80;
        public const int RemoteControlPortFallback = 8088;
    }

    public static class UI
    {
        public const int LivePreviewIntervalMs = 250;
        public const int ProgressUpdateIntervalMs = 200;
        public const int LiveWindowSeekDelayMs = 300;
    }

    public static class Storage
    {
        public const string AppDataFolderName = "ChurchDisplayApp";
        public const string SettingsFileName = "settings.json";
        public const string ServicePlanFileName = "service_plan.json";
    }

    public static class Timeouts
    {
        public const int ShutdownTimeoutSeconds = 2;
    }
}
