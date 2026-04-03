using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
    /// The last directory the user saved a playlist to.
    /// Used as the initial directory in the Save Playlist dialog.
    /// </summary>
    public string? LastPlaylistSaveDirectory { get; set; }

    /// <summary>Gets or sets the volume level for the main media (0.0 to 1.0).</summary>
    public double MainMediaVolume { get; set; } = 0.5;

    /// <summary>Gets or sets the default volume used by the Service Elements creator when adding items to the playlist (0.0 to 1.0).</summary>
    public double DefaultServiceVolume { get; set; } = 0.8;

    /// <summary>Gets or sets the font size for the playlist items.</summary>
    public double PlaylistFontSize { get; set; } = 13.0;

    /// <summary>Gets or sets the proportional width of the left column (0.0 to 1.0).</summary>
    public double? MainWindowLeftColumnProportion { get; set; }

    /// <summary>Gets or sets the proportional height of the top row (0.0 to 1.0).</summary>
    public double? MainWindowTopRowProportion { get; set; }
    
    // Song file names
    public string? CallToWorshipFile { get; set; }
    public string? DoxologyFile { get; set; }
    public string? GloriaPatriFile { get; set; }
    public string? LordsPrayerFile { get; set; }
    public string? SongForBeginningFile { get; set; }
    public string? PraiseSongFile { get; set; }
    public string? PrayerSongFile { get; set; }
    public string? CommunionSongFile { get; set; }
    public string? ChildrensMomentSongFile { get; set; }
    public string? InvitationSongFile { get; set; }
    public string? EndingSongFile { get; set; }
    
    /// <summary>Gets or sets the last directory used to search for media files.</summary>
    public string? LastMediaDirectory { get; set; }

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
    /// If ServiceSlots is empty (first run or upgrade from old version),
    /// seeds it from the legacy per-song properties. Called by Load().
    /// </summary>
    public void MigrateToServiceSlots()
    {
        if (ServiceSlots.Count > 0)
            return;

        var legacyMappings = new[]
        {
            ("Call to Worship",              CallToWorshipFile),
            ("Song for Beginning of Church", SongForBeginningFile),
            ("Doxology",                     DoxologyFile),
            ("Praise Song",                  PraiseSongFile),
            ("Gloria Patri",                 GloriaPatriFile),
            ("Lord's Prayer",                LordsPrayerFile),
            ("Prayer Song",                  PrayerSongFile),
            ("Communion Song",               CommunionSongFile),
            ("Children's Moment Song",       ChildrensMomentSongFile),
            ("Invitation Song",              InvitationSongFile),
            ("Ending Song",                  EndingSongFile),
        };

        foreach (var (name, fileName) in legacyMappings)
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
        CallToWorshipFile ??= string.Empty;
        DoxologyFile ??= string.Empty;
        GloriaPatriFile ??= string.Empty;
        LordsPrayerFile ??= string.Empty;
        SongForBeginningFile ??= string.Empty;
        PraiseSongFile ??= string.Empty;
        PrayerSongFile ??= string.Empty;
        CommunionSongFile ??= string.Empty;
        ChildrensMomentSongFile ??= string.Empty;
        InvitationSongFile ??= string.Empty;
        EndingSongFile ??= string.Empty;
        LastMediaDirectory ??= string.Empty;
    }

    private CancellationTokenSource? _saveCts;
    private readonly object _saveLock = new();

    /// <summary>
    /// Saves the settings to the JSON file with a debounce delay to prevent excessive disk writes.
    /// </summary>
    public void Save()
    {
        lock (_saveLock)
        {
            _saveCts?.Cancel();
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
            try
            {
                Validate();
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
                Serilog.Log.Information("Settings saved successfully.");
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Error saving settings");
            }
        }
    }

    public string? GetSongFile(string settingsProperty) => settingsProperty switch
    {
        nameof(CallToWorshipFile) => CallToWorshipFile,
        nameof(DoxologyFile) => DoxologyFile,
        nameof(GloriaPatriFile) => GloriaPatriFile,
        nameof(LordsPrayerFile) => LordsPrayerFile,
        nameof(SongForBeginningFile) => SongForBeginningFile,
        nameof(PraiseSongFile) => PraiseSongFile,
        nameof(PrayerSongFile) => PrayerSongFile,
        nameof(CommunionSongFile) => CommunionSongFile,
        nameof(ChildrensMomentSongFile) => ChildrensMomentSongFile,
        nameof(InvitationSongFile) => InvitationSongFile,
        nameof(EndingSongFile) => EndingSongFile,
        _ => null
    };

    public void SetSongFile(string settingsProperty, string? value)
    {
        switch (settingsProperty)
        {
            case nameof(CallToWorshipFile): CallToWorshipFile = value; break;
            case nameof(DoxologyFile): DoxologyFile = value; break;
            case nameof(GloriaPatriFile): GloriaPatriFile = value; break;
            case nameof(LordsPrayerFile): LordsPrayerFile = value; break;
            case nameof(SongForBeginningFile): SongForBeginningFile = value; break;
            case nameof(PraiseSongFile): PraiseSongFile = value; break;
            case nameof(PrayerSongFile): PrayerSongFile = value; break;
            case nameof(CommunionSongFile): CommunionSongFile = value; break;
            case nameof(ChildrensMomentSongFile): ChildrensMomentSongFile = value; break;
            case nameof(InvitationSongFile): InvitationSongFile = value; break;
            case nameof(EndingSongFile): EndingSongFile = value; break;
        }
    }

    private static AppSettings? _instance;

    /// <summary>
    /// Gets the shared application settings instance. Call Load() once at startup.
    /// </summary>
    public static AppSettings Current => _instance ??= Load();
}
