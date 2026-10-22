using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Text.Json.Serialization;
using ChurchDisplayApp.Models;

namespace ChurchDisplayApp;

/// <summary>
/// Represents the application settings for the ChurchDisplayApp, including media paths,
/// volumes, and display layout proportions.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// The list of user-configured service element slots.
    /// Replaces the old hardcoded per-song properties.
    /// </summary>
    public List<ServiceSlot> ServiceSlots { get; set; } = new();

    /// <summary>
    /// The last directory the user saved or loaded a playlist from.
    /// Used as the initial directory in the Save and Load Playlist dialogs.
    /// </summary>
    [JsonPropertyName("LastPlaylistSaveDirectory")]
    public string? LastPlaylistDirectory { get; set; }

    /// <summary>Gets or sets the volume level for the main media (0.0 to 1.0).</summary>
    public double MainMediaVolume { get; set; } = 0.5;

    /// <summary>Gets or sets the default volume used by the Service Elements creator when adding items to the playlist (0.0 to 1.0).</summary>
    public double DefaultServiceVolume { get; set; } = 1.0;

    /// <summary>Gets or sets the font size for the playlist items.</summary>
    public double PlaylistFontSize { get; set; } = 13.0;

    /// <summary>Gets or sets the proportional width of the left column (0.0 to 1.0).</summary>
    public double? MainWindowLeftColumnProportion { get; set; }

    /// <summary>Gets or sets the proportional height of the top row (0.0 to 1.0).</summary>
    public double? MainWindowTopRowProportion { get; set; }
    
    /// <summary>Gets or sets the last directory used to search for media files.</summary>
    public string? LastMediaDirectory { get; set; }

    /// <summary>
    /// Optional VLC audio output device name to override auto-detection.
    /// When null or empty, VLC selects the default Windows audio device.
    /// Set this to the device name shown in the log file if VLC picks the
    /// wrong output on this machine (e.g. "Realtek HD Audio").
    /// </summary>
    public string? VlcAudioDevice { get; set; }

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppConstants.Storage.AppDataFolderName,
        AppConstants.Storage.SettingsFileName
    );

    /// <summary>
    /// Loads the settings from the JSON file, or returns a default instance if the file does not exist.
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                
                // Validate and apply safe defaults
                settings.Validate();
                settings.MigrateToServiceSlots();
                
                return settings;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Error loading settings");
        }
        var defaults = new AppSettings();
        defaults.MigrateToServiceSlots();
        return defaults;
    }

    /// <summary>
    /// If ServiceSlots is empty (first run), seeds default service slot items. Called by Load().
    /// </summary>
    public void MigrateToServiceSlots()
    {
        if (ServiceSlots.Count > 0)
            return;

        var defaultSlotNames = new[]
        {
            "Call to Worship",
            "Song for Beginning of Church",
            "Doxology",
            "Praise Song",
            "Gloria Patri",
            "Lord's Prayer",
            "Prayer Song",
            "Communion Song",
            "Children's Moment Song",
            "Invitation Song",
            "Ending Song",
        };

        foreach (var name in defaultSlotNames)
        {
            ServiceSlots.Add(new ServiceSlot
            {
                DisplayName = name,
                FilePath    = null,
                IsSticky    = false,
                LastUsedFolder = LastMediaDirectory
            });
        }
    }

    public void Validate()
    {
        // Volume validation (0.0 to 1.0)
        MainMediaVolume = Math.Clamp(MainMediaVolume, 0.0, 1.0);
        DefaultServiceVolume = Math.Clamp(DefaultServiceVolume, 0.0, 1.0);
        PlaylistFontSize = Math.Clamp(PlaylistFontSize, 6.0, 30.0);

        // Proportion validation (0.0 to 1.0)
        if (!MainWindowLeftColumnProportion.HasValue)
            MainWindowLeftColumnProportion = 0.25;
        else
            MainWindowLeftColumnProportion = Math.Clamp(MainWindowLeftColumnProportion.Value, 0.05, 0.95);

        if (!MainWindowTopRowProportion.HasValue)
            MainWindowTopRowProportion = 0.8;
        else
            MainWindowTopRowProportion = Math.Clamp(MainWindowTopRowProportion.Value, 0.05, 0.95);

        // Ensure strings are not null
        LastMediaDirectory ??= string.Empty;

        // Note: LastPlaylistDirectory is intentionally allowed to be null so that
        // it can fall back to LastMediaDirectory on the first run of the app.
    }

    private CancellationTokenSource? _saveCts;
    private readonly object _saveLock = new();
    private bool _isDirty = false;

    /// <summary>
    /// Saves the settings to the JSON file with a debounce delay to prevent excessive disk writes.
    /// </summary>
    public void Save()
    {
        lock (_saveLock)
        {
            _isDirty = true;
            _saveCts?.Cancel();
            _saveCts?.Dispose();
            _saveCts = new CancellationTokenSource();
            var token = _saveCts.Token;

            Task.Delay(500, token).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    SaveImmediate();
                }
            }, token);
        }
    }

    /// <summary>
    /// Saves the settings immediately to the JSON file.
    /// </summary>
    public void SaveImmediate()
    {
        lock (_saveLock)
        {
            if (!_isDirty) return;

            try
            {
                Validate();
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
                _isDirty = false;
                Serilog.Log.Information("Settings saved successfully.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Error saving settings");
            }
        }
    }

    private static AppSettings? _instance;

    /// <summary>
    /// Gets the shared application settings instance. Call Load() once at startup.
    /// </summary>
    public static AppSettings Current => _instance ??= Load();
}
