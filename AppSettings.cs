using System.IO;
using System.Text.Json;

namespace ChurchDisplayApp;

public class AppSettings
{
    public string? BackgroundMusicPath { get; set; }
    public double BackgroundMusicVolume { get; set; } = 0.3;
    public bool BackgroundMusicEnabled { get; set; } = true;
    
    // New filename-based properties
    public string? CallToWorshipFile { get; set; }
    public string? DoxologyFile { get; set; }
    public string? SongForBeginningFile { get; set; }
    public string? PraiseSongFile { get; set; }
    public string? PrayerSongFile { get; set; }
    public string? CommunionSongFile { get; set; }
    public string? ChildrensMomentSongFile { get; set; }
    public string? InvitationSongFile { get; set; }
    public string? EndingSongFile { get; set; }
    
    // Remember the last used directory for file searches
    public string? LastMediaDirectory { get; set; }
    
    // Legacy path-based properties for migration
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public string? CallToWorshipPath { get; set; }
    public string? DoxologyPath { get; set; }
    public string? SongForBeginningPath { get; set; }
    public string? PraiseSongPath { get; set; }
    public string? PrayerSongPath { get; set; }
    public string? CommunionSongPath { get; set; }
    public string? ChildrensMomentSongPath { get; set; }
    public string? InvitationSongPath { get; set; }
    public string? EndingSongPath { get; set; }

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ChurchDisplayApp",
        "settings.json"
    );

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
                
                return settings;
            }
        }
        catch
        {
            // If loading fails, return default settings
        }
        return new AppSettings();
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
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Silent fail for settings save
        }
    }
}
