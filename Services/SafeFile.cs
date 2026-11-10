using System.IO;

namespace ChurchDisplayApp.Services;

/// <summary>
/// File helpers that avoid leaving a half-written file behind if the app or PC dies mid-write.
/// </summary>
public static class SafeFile
{
    /// <summary>
    /// Writes <paramref name="contents"/> to a temporary file next to <paramref name="path"/>, then swaps it
    /// into place. An existing file is kept as <c>path + ".bak"</c>. A crash during the write leaves the
    /// original file untouched.
    /// </summary>
    /// <param name="path">The destination file.</param>
    /// <param name="contents">The text to write (UTF-8, no BOM).</param>
    public static void WriteAllTextAtomic(string path, string contents)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = path + ".tmp";
        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(contents);
            writer.Flush();
            // Force the bytes onto the disk before the rename; otherwise a power cut right after the swap
            // can leave the new file empty even though the rename itself survived.
            stream.Flush(flushToDisk: true);
        }

        try
        {
            if (File.Exists(path))
                File.Replace(tempPath, path, path + ".bak");
            else
                File.Move(tempPath, path);
        }
        catch (Exception ex) when (ex is IOException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            // Some file systems (FAT/exFAT USB sticks, some network shares) cannot do an atomic replace.
            // Fall back to a plain overwrite - the temp file already proved the write itself succeeds.
            Serilog.Log.Debug(ex, "Atomic replace not available for {Path}; using copy fallback", path);
            File.Copy(tempPath, path, overwrite: true);
            File.Delete(tempPath);
        }
    }
}
