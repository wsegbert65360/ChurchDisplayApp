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

    public ObservableCollection<PlaylistItem> Items { get; } = new();

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

    /// <summary>
    /// Default volume for newly created playlist items.
    /// </summary>
    public const double DefaultItemVolume = 1.0;

    /// <summary>
    /// Adds multiple files to the playlist.
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
    /// Clears all items from the playlist and resets the dirty flag.
    /// </summary>
    public void Clear()
    {
        UnhookAllItems();
        Items.Clear();
        IsDirty = false;
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
    /// Saves the current playlist to a JSON file.
    /// New format: { "version": 2, "items": [{ "fullPath": "...", "volume": 0.8 }] }
    /// </summary>
    public void SavePlaylist(string filePath)
    {
        try
        {
            var playlistData = new PlaylistDataV2
            {
                Version = 2,
                Items = Items.Select(item => new PlaylistItemData
                {
                    FullPath = item.FullPath,
                    Volume = item.Volume
                }).ToList()
            };

            var json = JsonSerializer.Serialize(playlistData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            IsDirty = false;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save playlist: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads a playlist from a JSON file using a transaction approach.
    /// Supports both old format (array of path strings) and new format (array of objects with volume).
    /// On error, the original playlist is preserved.
    /// </summary>
    public void LoadPlaylist(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Playlist file not found", filePath);

            var json = File.ReadAllText(filePath);

            // Parse and validate first without modifying existing playlist
            var loadedItems = ParsePlaylist(json, filePath);

            // Atomically swap the collections only after successful parse
            UnhookAllItems();
            Items.Clear();

            foreach (var item in loadedItems)
            {
                Items.Add(item);
            }

            IsDirty = false;
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

    /// <summary>
    /// Parses playlist JSON without modifying the collection.
    /// Returns the parsed items or throws an exception.
    /// </summary>
    private List<PlaylistItem> ParsePlaylist(string json, string filePath)
    {
        var loadedItems = new List<PlaylistItem>();

        using (var doc = JsonDocument.Parse(json))
        {
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                // Legacy format: plain array of path strings
                foreach (var itemElement in root.EnumerateArray())
                {
                    var itemPath = itemElement.GetString();
                    if (!string.IsNullOrEmpty(itemPath) && ValidateFile(itemPath))
                    {
                        loadedItems.Add(new PlaylistItem(itemPath, DefaultItemVolume));
                    }
                }
            }
            else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("items", out var itemsArray))
            {
                if (itemsArray.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException("Playlist items must be an array");
                }

                // New format: { "version": 2, "items": [...] }
                foreach (var itemElement in itemsArray.EnumerateArray())
                {
                    string? itemPath = null;
                    double itemVolume = DefaultItemVolume;

                    if (itemElement.ValueKind == JsonValueKind.String)
                    {
                        // Items array contains plain strings (edge case)
                        itemPath = itemElement.GetString();
                    }
                    else if (itemElement.ValueKind == JsonValueKind.Object)
                    {
                        itemPath = itemElement.TryGetProperty("fullPath", out var fpProp) ? fpProp.GetString() : null;
                        if (itemElement.TryGetProperty("volume", out var volProp))
                        {
                            itemVolume = volProp.GetDouble();
                        }
                    }

                    itemVolume = Math.Clamp(itemVolume, 0.0, 1.0);

                    if (!string.IsNullOrEmpty(itemPath) && ValidateFile(itemPath))
                    {
                        loadedItems.Add(new PlaylistItem(itemPath, itemVolume));
                    }
                }
            }
            else
            {
                throw new InvalidDataException("Unrecognized playlist format");
            }
        }

        return loadedItems;
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
    /// New playlist format with per-item volume support.
    /// </summary>
    private class PlaylistDataV2
    {
        [JsonPropertyName("version")]
        public int Version { get; set; } = 2;

        [JsonPropertyName("items")]
        public List<PlaylistItemData> Items { get; set; } = new();
    }

    private class PlaylistItemData
    {
        [JsonPropertyName("fullPath")]
        public string FullPath { get; set; } = string.Empty;

        [JsonPropertyName("volume")]
        public double Volume { get; set; } = DefaultItemVolume;
    }
}
