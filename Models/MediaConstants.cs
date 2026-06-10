using System;
using System.Linq;

namespace ChurchDisplayApp.Models;

public static class MediaConstants
{
    public static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
    public static readonly string[] VideoExtensions = { ".mp4", ".mov", ".wmv", ".mkv" };
    public static readonly string[] AudioExtensions = { ".mp3", ".wav", ".flac", ".wma", ".m4a" };

    // ⚡ Bolt Performance: Uses zero-allocation ReadOnlySpan<char> and OrdinalIgnoreCase
    // instead of .ToLower() and LINQ .Contains() to prevent string allocations and reduce GC pressure.
    public static bool IsImage(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        var ext = System.IO.Path.GetExtension(filePath.AsSpan());
        foreach (var imageExt in ImageExtensions)
        {
            if (ext.Equals(imageExt, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    // ⚡ Bolt Performance: Uses zero-allocation ReadOnlySpan<char> and OrdinalIgnoreCase
    public static bool IsVideo(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        var ext = System.IO.Path.GetExtension(filePath.AsSpan());
        foreach (var videoExt in VideoExtensions)
        {
            if (ext.Equals(videoExt, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    // ⚡ Bolt Performance: Uses zero-allocation ReadOnlySpan<char> and OrdinalIgnoreCase
    public static bool IsAudio(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        var ext = System.IO.Path.GetExtension(filePath.AsSpan());
        foreach (var audioExt in AudioExtensions)
        {
            if (ext.Equals(audioExt, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public static bool IsSupported(string filePath)
    {
        return IsImage(filePath) || IsVideo(filePath) || IsAudio(filePath);
    }

    public static string GetImageFilter() => "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Files|*.*";
    public static string GetVideoFilter() => "Video Files|*.mp4;*.mov;*.wmv;*.mkv|All Files|*.*";
    public static string GetAudioFilter() => "Audio Files|*.mp3;*.wav;*.flac;*.wma;*.m4a|All Files|*.*";
    public static string GetPlaylistFilter() => "Playlist Files|*.pls|Text Files|*.txt|All Files|*.*";
    public static string GetAllMediaFilter() => "All Media|*.mp4;*.mov;*.wmv;*.mkv;*.mp3;*.wav;*.flac;*.wma;*.m4a;*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Files|*.*";
}
