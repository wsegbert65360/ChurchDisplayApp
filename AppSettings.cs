using System.IO;
using System.Text.Json;
using ChurchDisplayApp.Models;

namespace ChurchDisplayApp;

/// <summary>
/// Represents the application settings for the ChurchDisplayApp, including media paths,
/// volumes, and display layout proportions.
/// </summary>
public class AppSettings
{
    /// <summary>Gets or sets the file path for background music (Standard).</summary>
    public string? BackgroundMusicPath { get; set; }

    /// <summary>Gets or sets the file path for Children's Sermon music.</summary>
    public string? BackgroundMusicChildSermonPath { get; set; }

    /// <summary>Gets or sets the volume level for background music (0.0 to 1.0).</summary>
    public double BackgroundMusicVolume { get; set; } = 0.3;

    /// <summary>Gets or sets the volume level for the main media (0.0 to 1.0).</summary>
    public double MainMediaVolume { get; set; } = 0.5;

    /// <summary>Gets or sets whether background music is enabled.</summary>
    public bool BackgroundMusicEnabled { get; set; } = true;

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
                
                return settings;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Error loading settings: {ex.Message}");
        }
        return new AppSettings();
    }

    public void Validate()
    {
        // Volume validation (0.0 to 1.0)
        BackgroundMusicVolume = Math.Clamp(BackgroundMusicVolume, 0.0, 1.0);
        MainMediaVolume = Math.Clamp(MainMediaVolume, 0.0, 1.0);
        PlaylistFontSize = Math.Clamp(PlaylistFontSize, 6.0, 30.0);

        // Proportion validation (0.0 to 1.0)
        // Aggressive Migration: If user has a layout favoring the playlist (> 0.4 proportion)
        // or a layout where the top area is too small (< 0.75), reset to Preview-First defaults.
        if (!MainWindowLeftColumnProportion.HasValue || MainWindowLeftColumnProportion.Value > 0.4)
            MainWindowLeftColumnProportion = 0.25; // Favor Preview Wide
        else
            MainWindowLeftColumnProportion = Math.Clamp(MainWindowLeftColumnProportion.Value, 0.0, 1.0);

        if (!MainWindowTopRowProportion.HasValue || MainWindowTopRowProportion.Value < 0.75)
            MainWindowTopRowProportion = 0.8; // Favor Preview Tall
        else
            MainWindowTopRowProportion = Math.Clamp(MainWindowTopRowProportion.Value, 0.0, 1.0);

        // Ensure strings are not null (where applicable)
        BackgroundMusicPath ??= string.Empty;
        BackgroundMusicChildSermonPath ??= string.Empty;
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

    public void Save()
    {
        try
        {
            Validate(); // Final validation before save
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Error saving settings: {ex.Message}");
        }
    }
}
