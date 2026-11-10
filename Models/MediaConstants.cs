using System;
using System.Linq;

namespace ChurchDisplayApp.Models;

public static class MediaConstants
{
    public static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
    public static readonly string[] VideoExtensions = { ".mp4", ".mov", ".wmv", ".mkv" };
    public static readonly string[] AudioExtensions = { ".mp3", ".wav", ".flac", ".wma", ".m4a" };

    public static bool IsImage(string filePath)
    {
        var ext = System.IO.Path.GetExtension(filePath).ToLower();
        return ImageExtensions.Contains(ext);
    }

    public static bool IsVideo(string filePath)
    {
        var ext = System.IO.Path.GetExtension(filePath).ToLower();
        return VideoExtensions.Contains(ext);
    }

    public static bool IsAudio(string filePath)
    {
        var ext = System.IO.Path.GetExtension(filePath).ToLower();
        return AudioExtensions.Contains(ext);
    }

    public static bool IsSupported(string filePath)
    {
        return IsImage(filePath) || IsVideo(filePath) || IsAudio(filePath);
    }

    public static string GetImageFilter() => "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Files|*.*";
    public static string GetVideoFilter() => "Video Files|*.mp4;*.mov;*.wmv;*.mkv|All Files|*.*";
    public static string GetAudioFilter() => "Audio Files|*.mp3;*.wav;*.flac;*.wma;*.m4a|All Files|*.*";
    /// <summary>The extension used for playlists saved by this app.</summary>
    public const string PlaylistExtension = ".cdplaylist";

    /// <summary>The older extension. Still loadable, but it clashes with the standard .pls media-player format.</summary>
    public const string LegacyPlaylistExtension = ".pls";

    /// <summary>File-dialog filter for opening a playlist (new and legacy formats).</summary>
    public static string GetPlaylistOpenFilter() =>
        "Playlists|*.cdplaylist;*.pls|Church Display Playlist|*.cdplaylist|Legacy Playlist|*.pls|All Files|*.*";

    /// <summary>File-dialog filter for saving a playlist.</summary>
    public static string GetPlaylistSaveFilter() =>
        "Church Display Playlist|*.cdplaylist|Legacy Playlist|*.pls";

    public static string GetPlaylistFilter() => "Playlist Files|*.pls|Text Files|*.txt|All Files|*.*";
    public static string GetAllMediaFilter() => "All Media|*.mp4;*.mov;*.wmv;*.mkv;*.mp3;*.wav;*.flac;*.wma;*.m4a;*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Files|*.*";
}
