using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChurchDisplayApp.Models;

namespace ChurchDisplayApp.Services;

/// <summary>
/// Manages playlist operations including add, remove, save, and load functionality.
/// Tracks unsaved changes so the UI can prompt before closing or discarding.
/// </summary>
public class PlaylistManager
{
    private const int CurrentPlaylistVersion = 2;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Gets the items in the playlist, in play order.</summary>
    public ObservableCollection<PlaylistItem> Items { get; } = new();

    /// <summary>
    /// Gets or sets the file used for crash-recovery snapshots. When null, recovery snapshots are disabled.
    /// </summary>
    public string? RecoveryPath { get; set; }

    private bool _isDirty;
    /// <summary>
    /// Gets whether the playlist has unsaved changes since the last save/load.
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (_isDirty != value)
            {
                _isDirty = value;
                IsDirtyChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Raised when the IsDirty state changes.
    /// </summary>
    public event EventHandler? IsDirtyChanged;

    private string? _currentFilePath;
    /// <summary>
    /// Gets the file the playlist was last loaded from or saved to, or null for an unsaved playlist.
    /// </summary>
    public string? CurrentFilePath
    {
        get => _currentFilePath;
        private set
        {
            if (_currentFilePath != value)
            {
                _currentFilePath = value;
                CurrentFilePathChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Raised when <see cref="CurrentFilePath"/> changes.</summary>
    public event EventHandler? CurrentFilePathChanged;

    /// <summary>
    /// Gets a short name for the playlist: the file name, "Untitled playlist" for unsaved content,
    /// or null when there is nothing to name.
    /// </summary>
    public string? DisplayName =>
        CurrentFilePath != null
            ? Path.GetFileNameWithoutExtension(CurrentFilePath)
            : (Items.Count > 0 || IsDirty ? "Untitled playlist" : null);

    /// <summary>
    /// Default volume for newly created playlist items.
    /// </summary>
    public const double DefaultItemVolume = 1.0;

    /// <summary>
    /// Creates an empty playlist manager.
    /// </summary>
    public PlaylistManager()
    {
        Items.CollectionChanged += Items_CollectionChanged;
    }

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var oldItem in e.OldItems.OfType<PlaylistItem>())
            {
                UnhookItem(oldItem);
            }
        }

        if (e.NewItems != null)
        {
            foreach (var newItem in e.NewItems.OfType<PlaylistItem>())
            {
                HookItem(newItem);
            }
        }

        // Any collection mutation (add/remove/reorder/replace) marks playlist dirty.
        if (e.Action != NotifyCollectionChangedAction.Reset)
        {
            IsDirty = true;
        }
    }

    private void HookItem(PlaylistItem item)
    {
        item.PropertyChanged -= PlaylistItem_PropertyChanged;
        item.PropertyChanged += PlaylistItem_PropertyChanged;
    }

    private void UnhookItem(PlaylistItem item)
    {
        item.PropertyChanged -= PlaylistItem_PropertyChanged;
    }

    private void UnhookAllItems()
    {
        foreach (var item in Items)
        {
            UnhookItem(item);
        }
    }

    private void PlaylistItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaylistItem.Volume))
        {
            IsDirty = true;
        }
    }

    /// <summary>Adds files to the playlist using the default service volume.</summary>
    public void AddFiles(string[] filePaths)
    {
        AddFiles(filePaths, AppSettings.Current.DefaultServiceVolume);
    }

    /// <summary>
    /// Adds multiple files to the playlist with a specified default volume.
    /// </summary>
    public void AddFiles(string[] filePaths, double volume)
    {
        if (filePaths == null || filePaths.Length == 0)
            return;

        foreach (var filePath in filePaths)
        {
            if (ValidateFile(filePath))
            {
                Items.Add(new PlaylistItem(filePath, volume));
            }
        }
    }

    /// <summary>
    /// Removes an item from the playlist.
    /// </summary>
    public void RemoveItem(PlaylistItem item)
    {
        if (item != null)
        {
            Items.Remove(item);
        }
    }

    /// <summary>
    /// Removes the item at the specified index.
    /// </summary>
    public void RemoveAt(int index)
    {
        if (index >= 0 && index < Items.Count)
        {
            Items.RemoveAt(index);
        }
    }

    /// <summary>
    /// Clears all items from the playlist, forgets the current file, and resets the dirty flag.
    /// </summary>
    public void Clear()
    {
        UnhookAllItems();
        Items.Clear();
        CurrentFilePath = null;
        IsDirty = false;
        DeleteRecovery();
    }

    /// <summary>
    /// Validates that a file exists and is a supported media format.
    /// </summary>
    public bool ValidateFile(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            if (!File.Exists(filePath))
                return false;

            return MediaConstants.IsSupported(filePath);
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "File validation failed");
            return false;
        }
    }

    /// <summary>
    /// Re-checks every item against the disk (off the UI thread) and updates <see cref="PlaylistItem.IsMissing"/>,
    /// so a drive that was plugged in or removed since the last check shows correctly. Call from the UI thread.
    /// </summary>
    public async Task RefreshMissingAsync()
    {
        var snapshot = Items.ToList();
        if (snapshot.Count == 0)
            return;

        try
        {
            var missing = await Task.Run(() => snapshot.Select(i => !File.Exists(i.FullPath)).ToArray());
            for (int i = 0; i < snapshot.Count; i++)
            {
                snapshot[i].IsMissing = missing[i];
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "Could not refresh missing-file state");
        }
    }

    /// <summary>
    /// Saves the current playlist to a JSON file (written atomically, with a .bak of the previous version)
    /// and makes it the current file.
    /// New format: { "version": 2, "items": [{ "fullPath": "...", "relativePath": "...", "volume": 0.8 }] }
    /// </summary>
    public void SavePlaylist(string filePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            WritePlaylistFile(fullPath, Path.GetDirectoryName(fullPath));
            CurrentFilePath = fullPath;
            IsDirty = false;
            DeleteRecovery();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save playlist: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Writes a crash-recovery snapshot of the playlist to <see cref="RecoveryPath"/>.
    /// Does not change the dirty flag or the current file. Never throws.
    /// </summary>
    public void SaveRecovery()
    {
        if (string.IsNullOrEmpty(RecoveryPath) || Items.Count == 0)
            return;

        try
        {
            WritePlaylistFile(RecoveryPath, relativeTo: null);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Could not write playlist recovery file");
        }
    }

    /// <summary>Gets whether a crash-recovery snapshot exists.</summary>
    public bool HasRecovery => !string.IsNullOrEmpty(RecoveryPath) && File.Exists(RecoveryPath);

    /// <summary>Deletes the crash-recovery snapshot, if any. Never throws.</summary>
    public void DeleteRecovery()
    {
        if (string.IsNullOrEmpty(RecoveryPath))
            return;

        try
        {
            File.Delete(RecoveryPath);
            File.Delete(RecoveryPath + ".bak");
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "Could not delete playlist recovery file");
        }
    }

    /// <summary>
    /// Loads the crash-recovery snapshot as an unsaved (dirty, untitled) playlist.
    /// </summary>
    /// <returns>Paths that were in the snapshot but are missing or unusable.</returns>
    public IReadOnlyList<string> RestoreRecovery()
    {
        if (string.IsNullOrEmpty(RecoveryPath))
            throw new InvalidOperationException("No recovery file is configured.");

        return LoadCore(RecoveryPath, fromRecovery: true);
    }

    private void WritePlaylistFile(string filePath, string? relativeTo)
    {
        var playlistData = new PlaylistDataV2
        {
            Version = CurrentPlaylistVersion,
            Items = Items.Select(item => new PlaylistItemData
            {
                FullPath = item.FullPath,
                RelativePath = GetRelativePath(relativeTo, item.FullPath),
                Volume = item.Volume
            }).ToList()
        };

        SafeFile.WriteAllTextAtomic(filePath, JsonSerializer.Serialize(playlistData, JsonOptions));
    }

    private static string? GetRelativePath(string? baseDirectory, string fullPath)
    {
        if (string.IsNullOrEmpty(baseDirectory))
            return null;

        try
        {
            var relative = Path.GetRelativePath(baseDirectory, fullPath);
            // Different drive: no relative path exists.
            return Path.IsPathRooted(relative) ? null : relative;
        }
        catch (ArgumentException ex)
        {
            Serilog.Log.Debug(ex, "No relative path for {Path}", fullPath);
            return null;
        }
    }

    /// <summary>
    /// Loads a playlist from a JSON file using a transaction approach.
    /// Supports both old format (array of path strings) and new format (array of objects with volume).
    /// Files that cannot be found are kept in the list, flagged as missing, so saving does not lose them.
    /// On error, the original playlist is preserved.
    /// </summary>
    /// <returns>Paths that were in the file but are missing or unusable.</returns>
    public IReadOnlyList<string> LoadPlaylist(string filePath) => LoadCore(filePath, fromRecovery: false);

    private IReadOnlyList<string> LoadCore(string filePath, bool fromRecovery)
    {
        try
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Playlist file not found", filePath);

            var json = File.ReadAllText(filePath);
            var baseDirectory = fromRecovery ? null : Path.GetDirectoryName(Path.GetFullPath(filePath));

            var parsed = ParsePlaylist(json, baseDirectory);

            if (parsed.Items.Count == 0 && parsed.Invalid.Count > 0)
                throw new InvalidOperationException(
                    $"None of the {parsed.Invalid.Count} entries in this playlist could be used.");

            UnhookAllItems();
            Items.Clear();

            foreach (var item in parsed.Items)
            {
                Items.Add(item);
            }

            CurrentFilePath = fromRecovery ? null : Path.GetFullPath(filePath);

            // Dirty when the list no longer matches the file: entries were dropped, paths were re-resolved,
            // or this is a recovered unsaved playlist.
            IsDirty = fromRecovery || parsed.Invalid.Count > 0 || parsed.PathsUpdated;

            if (!fromRecovery)
                DeleteRecovery();

            return parsed.Invalid
                .Concat(parsed.Items.Where(i => i.IsMissing).Select(i => i.FullPath))
                .ToList();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse playlist: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to load playlist: {ex.Message}", ex);
        }
    }

    private sealed class ParsedPlaylist
    {
        public List<PlaylistItem> Items { get; } = new();
        public List<string> Invalid { get; } = new();
        public bool PathsUpdated { get; set; }
    }

    /// <summary>
    /// Parses playlist JSON without modifying the collection.
    /// Returns the parsed items or throws an exception.
    /// </summary>
    private ParsedPlaylist ParsePlaylist(string json, string? baseDirectory)
    {
        var result = new ParsedPlaylist();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in root.EnumerateArray())
            {
                AddEntry(entry, result, baseDirectory);
            }
        }
        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("items", out var itemsArray))
        {
            if (root.TryGetProperty("version", out var versionProp)
                && versionProp.ValueKind == JsonValueKind.Number
                && versionProp.TryGetInt32(out var version)
                && version > CurrentPlaylistVersion)
            {
                throw new InvalidDataException(
                    "This playlist was saved by a newer version of the app. Please update the app to open it.");
            }

            if (itemsArray.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Playlist items must be an array");
            }

            foreach (var entry in itemsArray.EnumerateArray())
            {
                AddEntry(entry, result, baseDirectory);
            }
        }
        else
        {
            throw new InvalidDataException("Unrecognized playlist format");
        }

        return result;
    }

    private static void AddEntry(JsonElement entry, ParsedPlaylist result, string? baseDirectory)
    {
        string? path = null;
        string? relative = null;
        double volume = DefaultItemVolume;

        if (entry.ValueKind == JsonValueKind.String)
        {
            path = entry.GetString();
        }
        else if (entry.ValueKind == JsonValueKind.Object)
        {
            if (entry.TryGetProperty("fullPath", out var fp) && fp.ValueKind == JsonValueKind.String)
                path = fp.GetString();

            if (entry.TryGetProperty("relativePath", out var rp) && rp.ValueKind == JsonValueKind.String)
                relative = rp.GetString();

            if (entry.TryGetProperty("volume", out var vp)
                && vp.ValueKind == JsonValueKind.Number
                && vp.TryGetDouble(out var parsedVolume)
                && double.IsFinite(parsedVolume))
            {
                volume = parsedVolume;
            }
        }

        if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(relative))
        {
            result.Invalid.Add("(empty path)");
            return;
        }

        // If the saved absolute path is gone (drive letter changed, folder moved together with the playlist),
        // try the path relative to the playlist file.
        var resolved = path;
        if ((string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
            && !string.IsNullOrWhiteSpace(relative) && baseDirectory != null)
        {
            try
            {
                var candidate = Path.GetFullPath(Path.Combine(baseDirectory, relative));
                if (File.Exists(candidate))
                {
                    resolved = candidate;
                    result.PathsUpdated = true;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                Serilog.Log.Debug(ex, "Relative path {Path} could not be resolved", relative);
            }
        }

        if (string.IsNullOrWhiteSpace(resolved) || !MediaConstants.IsSupported(resolved))
        {
            result.Invalid.Add(resolved ?? relative ?? "(empty path)");
            return;
        }

        result.Items.Add(new PlaylistItem(resolved, Math.Clamp(volume, 0.0, 1.0))
        {
            IsMissing = !File.Exists(resolved)
        });
    }

    /// <summary>
    /// Gets the item at the specified index, or null if index is invalid.
    /// </summary>
    public PlaylistItem? GetItem(int index)
    {
        if (index >= 0 && index < Items.Count)
            return Items[index];
        return null;
    }

    /// <summary>
    /// Playlist file format with per-item volume support.
    /// </summary>
    private class PlaylistDataV2
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = CurrentPlaylistVersion;

        [JsonPropertyName("items")]
        public List<PlaylistItemData> Items { get; set; } = new();
    }

    private class PlaylistItemData
    {
        [JsonPropertyName("fullPath")]
        public string FullPath { get; set; } = string.Empty;

        // Added after v2 shipped; older versions of the app ignore it.
        [JsonPropertyName("relativePath")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RelativePath { get; set; }

        [JsonPropertyName("volume")]
        public double Volume { get; set; } = DefaultItemVolume;
    }
}
