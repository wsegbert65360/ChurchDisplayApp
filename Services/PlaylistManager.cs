using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using ChurchDisplayApp.Models;

namespace ChurchDisplayApp.Services;

/// <summary>
/// Manages playlist operations including add, remove, save, and load functionality.
/// </summary>
public class PlaylistManager
{

    public ObservableCollection<PlaylistItem> Items { get; } = new();

    /// <summary>
    /// Adds multiple files to the playlist.
    /// </summary>
    public void AddFiles(string[] filePaths)
    {
        if (filePaths == null || filePaths.Length == 0)
            return;

        foreach (var filePath in filePaths)
        {
            if (ValidateFile(filePath))
            {
                Items.Add(new PlaylistItem(filePath));
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
    /// Clears all items from the playlist.
    /// </summary>
    public void Clear()
    {
        Items.Clear();
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
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Saves the current playlist to a JSON file.
    /// </summary>
    public void SavePlaylist(string filePath)
    {
        try
        {
            var playlistData = new PlaylistData
            {
                Items = Items.Select(item => item.FullPath).ToList()
            };

            var json = JsonSerializer.Serialize(playlistData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save playlist: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads a playlist from a JSON file.
    /// </summary>
    public void LoadPlaylist(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Playlist file not found", filePath);

            var json = File.ReadAllText(filePath);
            var playlistData = JsonSerializer.Deserialize<PlaylistData>(json);

            if (playlistData?.Items == null)
                throw new InvalidDataException("Invalid playlist format");

            Items.Clear();
            foreach (var itemPath in playlistData.Items)
            {
                if (ValidateFile(itemPath))
                {
                    Items.Add(new PlaylistItem(itemPath));
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load playlist: {ex.Message}", ex);
        }
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

    public bool IsImageFormat(string extension) => MediaConstants.ImageExtensions.Contains(extension);
    public bool IsVideoFormat(string extension) => MediaConstants.VideoExtensions.Contains(extension);
    public bool IsAudioFormat(string extension) => MediaConstants.AudioExtensions.Contains(extension);
    public bool IsMediaFormat(string extension) => IsVideoFormat(extension) || IsAudioFormat(extension);

    private class PlaylistData
    {
        public List<string> Items { get; set; } = new();
    }
}
