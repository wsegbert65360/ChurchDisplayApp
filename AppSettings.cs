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
    /// <summary>Gets or sets the file path for background music.</summary>
    public string? BackgroundMusicPath { get; set; }

    /// <summary>Gets or sets the volume level for background music (0.0 to 1.0).</summary>
    public double BackgroundMusicVolume { get; set; } = 0.3;

    /// <summary>Gets or sets the volume level for the main media (0.0 to 1.0).</summary>
    public double MainMediaVolume { get; set; } = 0.5;

    /// <summary>Gets or sets whether background music is enabled.</summary>
    public bool BackgroundMusicEnabled { get; set; } = true;

    // Legacy pixel-based layout properties (kept for migration)
    [Obsolete("Use MainWindowLeftColumnProportion instead")]
    public double? MainWindowLeftColumnWidth { get; set; }
    [Obsolete("Use MainWindowLeftColumnProportion instead")]
    public double? MainWindowRightColumnWidth { get; set; }
    [Obsolete("Use MainWindowTopRowProportion instead")]
    public double? MainWindowTopRowHeight { get; set; }
    [Obsolete("Use MainWindowTopRowProportion instead")]
    public double? MainWindowMiddleRowHeight { get; set; }
    [Obsolete("Use MainWindowTopRowProportion instead")]
    public double? MainWindowBottomRowHeight { get; set; }
    
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
    
    // Legacy path-based properties for migration
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public string? CallToWorshipPath { get; set; }
    public string? DoxologyPath { get; set; }
    public string? GloriaPatriPath { get; set; }
    public string? SongForBeginningPath { get; set; }
    public string? PraiseSongPath { get; set; }
    public string? PrayerSongPath { get; set; }
    public string? CommunionSongPath { get; set; }
    public string? ChildrensMomentSongPath { get; set; }
    public string? InvitationSongPath { get; set; }
    public string? EndingSongPath { get; set; }

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
                
                // Migration: Check if old path-based properties exist and migrate to filename-based
                MigrateFromPathBased(settings);
                
                // Validate and apply safe defaults
                settings.Validate();
                
                return settings;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Error loading settings: {ex.Message}");
            // If loading fails, return default settings
        }
        return new AppSettings();
    }

    public void Validate()
    {
        // Volume validation (0.0 to 1.0)
        BackgroundMusicVolume = Math.Clamp(BackgroundMusicVolume, 0.0, 1.0);
        MainMediaVolume = Math.Clamp(MainMediaVolume, 0.0, 1.0);

        // Proportion validation (0.0 to 1.0)
        if (MainWindowLeftColumnProportion.HasValue)
            MainWindowLeftColumnProportion = Math.Clamp(MainWindowLeftColumnProportion.Value, 0.0, 1.0);
        else
            MainWindowLeftColumnProportion = 0.7; // Default

        if (MainWindowTopRowProportion.HasValue)
            MainWindowTopRowProportion = Math.Clamp(MainWindowTopRowProportion.Value, 0.0, 1.0);
        else
            MainWindowTopRowProportion = 0.7; // Default

        // Ensure strings are not null (where applicable)
        BackgroundMusicPath ??= string.Empty;
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

    private static void MigrateFromPathBased(AppSettings settings)
    {
        var needsSave = false;
        
        // Migrate Call to Worship
        if (string.IsNullOrEmpty(settings.CallToWorshipFile) && !string.IsNullOrEmpty(settings.CallToWorshipPath))
        {
            settings.CallToWorshipFile = System.IO.Path.GetFileName(settings.CallToWorshipPath);
            settings.LastMediaDirectory = System.IO.Path.GetDirectoryName(settings.CallToWorshipPath);
            needsSave = true;
        }
        else if (!string.IsNullOrEmpty(settings.CallToWorshipFile) && settings.CallToWorshipFile.Contains("\\"))
        {
            settings.CallToWorshipFile = System.IO.Path.GetFileName(settings.CallToWorshipFile);
            needsSave = true;
        }
        
        // Migrate Doxology
        if (string.IsNullOrEmpty(settings.DoxologyFile) && !string.IsNullOrEmpty(settings.DoxologyPath))
        {
            settings.DoxologyFile = System.IO.Path.GetFileName(settings.DoxologyPath);
            if (string.IsNullOrEmpty(settings.LastMediaDirectory))
                settings.LastMediaDirectory = System.IO.Path.GetDirectoryName(settings.DoxologyPath);
            needsSave = true;
        }
        else if (!string.IsNullOrEmpty(settings.DoxologyFile) && settings.DoxologyFile.Contains("\\"))
        {
            settings.DoxologyFile = System.IO.Path.GetFileName(settings.DoxologyFile);
            needsSave = true;
        }
        
        // Migrate Song for Beginning
        if (string.IsNullOrEmpty(settings.SongForBeginningFile) && !string.IsNullOrEmpty(settings.SongForBeginningPath))
        {
            settings.SongForBeginningFile = System.IO.Path.GetFileName(settings.SongForBeginningPath);
            if (string.IsNullOrEmpty(settings.LastMediaDirectory))
                settings.LastMediaDirectory = System.IO.Path.GetDirectoryName(settings.SongForBeginningPath);
            needsSave = true;
        }
        else if (!string.IsNullOrEmpty(settings.SongForBeginningFile) && settings.SongForBeginningFile.Contains("\\"))
        {
            settings.SongForBeginningFile = System.IO.Path.GetFileName(settings.SongForBeginningFile);
            needsSave = true;
        }
        
        // Migrate Praise Song
        if (string.IsNullOrEmpty(settings.PraiseSongFile) && !string.IsNullOrEmpty(settings.PraiseSongPath))
        {
            settings.PraiseSongFile = System.IO.Path.GetFileName(settings.PraiseSongPath);
            if (string.IsNullOrEmpty(settings.LastMediaDirectory))
                settings.LastMediaDirectory = System.IO.Path.GetDirectoryName(settings.PraiseSongPath);
            needsSave = true;
        }
        else if (!string.IsNullOrEmpty(settings.PraiseSongFile) && settings.PraiseSongFile.Contains("\\"))
        {
            settings.PraiseSongFile = System.IO.Path.GetFileName(settings.PraiseSongFile);
            needsSave = true;
        }
        
        // Migrate Prayer Song
        if (string.IsNullOrEmpty(settings.PrayerSongFile) && !string.IsNullOrEmpty(settings.PrayerSongPath))
        {
            settings.PrayerSongFile = System.IO.Path.GetFileName(settings.PrayerSongPath);
            if (string.IsNullOrEmpty(settings.LastMediaDirectory))
                settings.LastMediaDirectory = System.IO.Path.GetDirectoryName(settings.PrayerSongPath);
            needsSave = true;
        }
        else if (!string.IsNullOrEmpty(settings.PrayerSongFile) && settings.PrayerSongFile.Contains("\\"))
        {
            settings.PrayerSongFile = System.IO.Path.GetFileName(settings.PrayerSongFile);
            needsSave = true;
        }
        
        // Migrate Communion Song
        if (string.IsNullOrEmpty(settings.CommunionSongFile) && !string.IsNullOrEmpty(settings.CommunionSongPath))
        {
            settings.CommunionSongFile = System.IO.Path.GetFileName(settings.CommunionSongPath);
            if (string.IsNullOrEmpty(settings.LastMediaDirectory))
                settings.LastMediaDirectory = System.IO.Path.GetDirectoryName(settings.CommunionSongPath);
            needsSave = true;
        }
        else if (!string.IsNullOrEmpty(settings.CommunionSongFile) && settings.CommunionSongFile.Contains("\\"))
        {
            settings.CommunionSongFile = System.IO.Path.GetFileName(settings.CommunionSongFile);
            needsSave = true;
        }
        
        // Migrate Childrens Moment Song
        if (string.IsNullOrEmpty(settings.ChildrensMomentSongFile) && !string.IsNullOrEmpty(settings.ChildrensMomentSongPath))
        {
            settings.ChildrensMomentSongFile = System.IO.Path.GetFileName(settings.ChildrensMomentSongPath);
            if (string.IsNullOrEmpty(settings.LastMediaDirectory))
                settings.LastMediaDirectory = System.IO.Path.GetDirectoryName(settings.ChildrensMomentSongPath);
            needsSave = true;
        }
        else if (!string.IsNullOrEmpty(settings.ChildrensMomentSongFile) && settings.ChildrensMomentSongFile.Contains("\\"))
        {
            settings.ChildrensMomentSongFile = System.IO.Path.GetFileName(settings.ChildrensMomentSongFile);
            needsSave = true;
        }
        
        // Migrate Invitation Song
        if (string.IsNullOrEmpty(settings.InvitationSongFile) && !string.IsNullOrEmpty(settings.InvitationSongPath))
        {
            settings.InvitationSongFile = System.IO.Path.GetFileName(settings.InvitationSongPath);
            if (string.IsNullOrEmpty(settings.LastMediaDirectory))
                settings.LastMediaDirectory = System.IO.Path.GetDirectoryName(settings.InvitationSongPath);
            needsSave = true;
        }
        else if (!string.IsNullOrEmpty(settings.InvitationSongFile) && settings.InvitationSongFile.Contains("\\"))
        {
            settings.InvitationSongFile = System.IO.Path.GetFileName(settings.InvitationSongFile);
            needsSave = true;
        }
        
        // Migrate Ending Song
        if (string.IsNullOrEmpty(settings.EndingSongFile) && !string.IsNullOrEmpty(settings.EndingSongPath))
        {
            settings.EndingSongFile = System.IO.Path.GetFileName(settings.EndingSongPath);
            if (string.IsNullOrEmpty(settings.LastMediaDirectory))
                settings.LastMediaDirectory = System.IO.Path.GetDirectoryName(settings.EndingSongPath);
            needsSave = true;
        }
        else if (!string.IsNullOrEmpty(settings.EndingSongFile) && settings.EndingSongFile.Contains("\\"))
        {
            settings.EndingSongFile = System.IO.Path.GetFileName(settings.EndingSongFile);
            needsSave = true;
        }
        
        // Save the migrated settings if any changes were made
        if (needsSave)
        {
            settings.Save();
        }
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
