using System;
using System.Linq;

namespace ChurchDisplayApp.Models;

public static class MediaConstants
{
    public static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
    public static readonly string[] VideoExtensions = { ".mp4", ".mov", ".wmv", ".mkv" };
    public static readonly string[] AudioExtensions = { ".mp3", ".wav", ".flac", ".wma", ".m4a" };

    // Helper to check extensions without allocations
    private static bool ContainsExt(ReadOnlySpan<char> ext, string[] extensions)
    {
        foreach (var e in extensions)
        {
            if (ext.Equals(e, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // Performance Optimization: Use ReadOnlySpan<char> and StringComparison.OrdinalIgnoreCase
    // to eliminate string allocations from .ToLower() and reduce GC pressure.
    public static bool IsImage(string filePath)
    {
        var ext = System.IO.Path.GetExtension(filePath.AsSpan());
        return ContainsExt(ext, ImageExtensions);
    }

    public static bool IsVideo(string filePath)
    {
        var ext = System.IO.Path.GetExtension(filePath.AsSpan());
        return ContainsExt(ext, VideoExtensions);
    }

    public static bool IsAudio(string filePath)
    {
        var ext = System.IO.Path.GetExtension(filePath.AsSpan());
        return ContainsExt(ext, AudioExtensions);
    }

    public static bool IsSupported(string filePath)
    {
        var ext = System.IO.Path.GetExtension(filePath.AsSpan());
        return ContainsExt(ext, ImageExtensions) ||
               ContainsExt(ext, VideoExtensions) ||
               ContainsExt(ext, AudioExtensions);
    }

    public static string GetImageFilter() => "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Files|*.*";
    public static string GetVideoFilter() => "Video Files|*.mp4;*.mov;*.wmv;*.mkv|All Files|*.*";
    public static string GetAudioFilter() => "Audio Files|*.mp3;*.wav;*.flac;*.wma;*.m4a|All Files|*.*";
    public static string GetPlaylistFilter() => "Playlist Files|*.pls|Text Files|*.txt|All Files|*.*";
    public static string GetAllMediaFilter() => "All Media|*.mp4;*.mov;*.wmv;*.mkv;*.mp3;*.wav;*.flac;*.wma;*.m4a;*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Files|*.*";
}
