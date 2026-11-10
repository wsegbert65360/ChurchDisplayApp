using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChurchDisplayApp.Models;
using ChurchDisplayApp.Services;

namespace ChurchDisplayApp;

/// <summary>
/// Represents the application settings for the ChurchDisplayApp, including media paths,
/// volumes, and display layout proportions.
/// </summary>
public class AppSettings
{
    /// <summary>The settings schema version written by this build.</summary>
    public const int CurrentSettingsVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Schema version of the settings file. 0 means a file written before versioning existed.
    /// Used so one-time migrations (such as seeding the default service slots) never run twice.
    /// </summary>
    public int SettingsVersion { get; set; }

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

    /// <summary>Gets or sets the default volume used by the Service Elements creator when adding items to the playlist (0.0 to 1.0).</summary>
    public double DefaultServiceVolume { get; set; } = 1.0;

    /// <summary>Gets or sets the font size for the playlist items.</summary>
    public double PlaylistFontSize { get; set; } = 13.0;

    /// <summary>Gets or sets the proportional width of the left column (0.0 to 1.0).</summary>
    public double? MainWindowLeftColumnProportion { get; set; }

    /// <summary>Gets or sets the proportional height of the top row (0.0 to 1.0).</summary>
    public double? MainWindowTopRowProportion { get; set; }

    // Legacy per-song file names. Only read so old settings files can be migrated into ServiceSlots.
    // They are cleared after migration and not written back out.

    /// <summary>Legacy setting; migrated into <see cref="ServiceSlots"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CallToWorshipFile { get; set; }

    /// <summary>Legacy setting; migrated into <see cref="ServiceSlots"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DoxologyFile { get; set; }

    /// <summary>Legacy setting; migrated into <see cref="ServiceSlots"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GloriaPatriFile { get; set; }

    /// <summary>Legacy setting; migrated into <see cref="ServiceSlots"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LordsPrayerFile { get; set; }

    /// <summary>Legacy setting; migrated into <see cref="ServiceSlots"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SongForBeginningFile { get; set; }

    /// <summary>Legacy setting; migrated into <see cref="ServiceSlots"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PraiseSongFile { get; set; }

    /// <summary>Legacy setting; migrated into <see cref="ServiceSlots"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrayerSongFile { get; set; }

    /// <summary>Legacy setting; migrated into <see cref="ServiceSlots"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CommunionSongFile { get; set; }

    /// <summary>Legacy setting; migrated into <see cref="ServiceSlots"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ChildrensMomentSongFile { get; set; }

    /// <summary>Legacy setting; migrated into <see cref="ServiceSlots"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InvitationSongFile { get; set; }

    /// <summary>Legacy setting; migrated into <see cref="ServiceSlots"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EndingSongFile { get; set; }

    /// <summary>Gets or sets the last directory used to search for media files.</summary>
    public string? LastMediaDirectory { get; set; }

    /// <summary>
    /// Optional VLC audio output device name to override auto-detection.
    /// When null or empty, VLC selects the default Windows audio device.
    /// Set this to the device name shown in the log file if VLC picks the
    /// wrong output on this machine (e.g. "Realtek HD Audio").
    /// </summary>
    public string? VlcAudioDevice { get; set; }

    /// <summary>
    /// Set when the settings file was damaged at startup. The UI shows this once so the operator knows
    /// why their settings may look different. Never written to disk.
    /// </summary>
    [JsonIgnore]
    public string? LoadNotice { get; private set; }

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        AppConstants.Storage.AppDataFolderName,
        AppConstants.Storage.SettingsFileName
    );

    /// <summary>
    /// Loads the settings from disk. If the main file is unreadable it is kept aside as
    /// <c>settings.json.corrupt-*</c> and the last good backup (<c>.bak</c>) is tried.
    /// Falls back to defaults only when nothing readable exists.
    /// </summary>
    public static AppSettings Load()
    {
        string? notice = null;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);

            foreach (var path in new[] { SettingsPath, SettingsPath + ".bak" })
            {
                if (!File.Exists(path))
                    continue;

                try
                {
                    var settings = ReadFile(path);
                    if (path != SettingsPath)
                    {
                        Serilog.Log.Warning("Settings restored from backup {Path}", path);
                        notice ??= "Your settings file was damaged, so the last backup was restored.";
                    }
                    settings.LoadNotice = notice;
                    return settings;
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "Settings file {Path} could not be read", path);
                    var kept = path == SettingsPath ? KeepCorruptFile(path) : null;
                    notice = kept != null
                        ? $"Your settings file was damaged and could not be read. The old file was kept as {Path.GetFileName(kept)}."
                        : "Your settings file was damaged and could not be read.";
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Error loading settings");
        }

        var defaults = new AppSettings();
        defaults.MigrateToServiceSlots();
        defaults.LoadNotice = notice;
        return defaults;
    }

    private static AppSettings ReadFile(string path)
    {
        var json = File.ReadAllText(path);
        var settings = JsonSerializer.Deserialize<AppSettings>(json)
            ?? throw new InvalidDataException("Settings file is empty.");

        // Validate and apply safe defaults
        settings.Validate();
        settings.MigrateToServiceSlots();
        return settings;
    }

    private static string? KeepCorruptFile(string path)
    {
        try
        {
            var target = $"{path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(path, target, overwrite: true);
            return target;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Could not set aside damaged settings file {Path}", path);
            return null;
        }
    }

    /// <summary>
    /// One-time migration to the current settings schema. On a first run or an upgrade from a version
    /// that used per-song properties, seeds the service slots. Does nothing once the file is up to date,
    /// so slots the user deleted never come back.
    /// </summary>
    public void MigrateToServiceSlots()
    {
        if (SettingsVersion >= CurrentSettingsVersion)
            return;

        if (ServiceSlots.Count == 0)
        {
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
                    FilePath    = !string.IsNullOrEmpty(fileName) ? fileName : null,
                    IsSticky    = false,
                    LastUsedFolder = LastMediaDirectory
                });
            }
        }

        // The legacy values now live in the slots; stop carrying them around.
        CallToWorshipFile = null;
        DoxologyFile = null;
        GloriaPatriFile = null;
        LordsPrayerFile = null;
        SongForBeginningFile = null;
        PraiseSongFile = null;
        PrayerSongFile = null;
        CommunionSongFile = null;
        ChildrensMomentSongFile = null;
        InvitationSongFile = null;
        EndingSongFile = null;

        SettingsVersion = CurrentSettingsVersion;
    }

    /// <summary>Clamps values to safe ranges and fills in defaults.</summary>
    public void Validate()
    {
        // Volume validation (0.0 to 1.0)
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

        ServiceSlots ??= new List<ServiceSlot>();
        LastMediaDirectory ??= string.Empty;

        // Note: LastPlaylistDirectory is intentionally allowed to be null so that
        // it can fall back to LastMediaDirectory on the first run of the app.
    }

    private CancellationTokenSource? _saveCts;
    private readonly object _saveLock = new();
    private bool _isDirty = false;
    private string? _pendingJson;

    /// <summary>
    /// Saves the settings with a short debounce delay to prevent excessive disk writes.
    /// The settings are serialized immediately on the calling thread, so later edits on the UI thread
    /// cannot corrupt the write that happens half a second later.
    /// </summary>
    public void Save()
    {
        lock (_saveLock)
        {
            try
            {
                Validate();
                _pendingJson = Serialize();
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Could not prepare settings for saving");
                return;
            }

            _isDirty = true;
            _saveCts?.Cancel();
            _saveCts?.Dispose();
            _saveCts = new CancellationTokenSource();
            var token = _saveCts.Token;

            Task.Delay(500, token).ContinueWith(
                _ => FlushPending(),
                token,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
        }
    }

    /// <summary>
    /// Saves the settings immediately to the JSON file.
    /// </summary>
    /// <param name="force">Write even if nothing changed (used at shutdown).</param>
    public void SaveImmediate(bool force = false)
    {
        lock (_saveLock)
        {
            if (!_isDirty && !force) return;

            try
            {
                Validate();
                if (WriteToDisk(Serialize()))
                {
                    _pendingJson = null;
                    _isDirty = false;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Error saving settings");
            }
        }
    }

    private void FlushPending()
    {
        lock (_saveLock)
        {
            if (_pendingJson == null) return;

            if (WriteToDisk(_pendingJson))
            {
                _pendingJson = null;
                _isDirty = false;
            }
        }
    }

    private string Serialize() => JsonSerializer.Serialize(this, JsonOptions);

    private static bool WriteToDisk(string json)
    {
        try
        {
            SafeFile.WriteAllTextAtomic(SettingsPath, json);
            Serilog.Log.Information("Settings saved successfully.");
            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Error saving settings");
            return false;
        }
    }

    private static readonly Lazy<AppSettings> _lazyInstance = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the shared application settings instance. Thread-safe via Lazy&lt;T&gt;.
    /// </summary>
    public static AppSettings Current => _lazyInstance.Value;
}
