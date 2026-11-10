namespace ChurchDisplayApp.Models;

/// <summary>
/// Status information sent to remote control clients.
/// </summary>
/// <param name="PlaylistVersion">Changes whenever the playlist is edited, so the remote knows to reload it.</param>
public record RemoteStatus(
    string Title,
    double ProgressPercent,
    string CurrentTime,
    string Duration,
    double Volume,
    int PlayingIndex = -1,
    bool IsPlaying = false,
    bool IsBlanked = false,
    int PlaylistVersion = 0
);

/// <summary>
/// Playlist item information sent to remote control clients.
/// </summary>
public record RemotePlaylistItem(
    int Index,
    string FileName
);

/// <summary>
/// The playlist as the remote sees it. <see cref="Version"/> is sent back with play requests so a tap on
/// a stale list (the operator reordered or deleted items since it loaded) is rejected instead of playing
/// whatever now sits at that position.
/// </summary>
public record RemotePlaylist(
    int Version,
    IReadOnlyList<RemotePlaylistItem> Items
);

/// <summary>Result of a remote request to play a playlist item.</summary>
public enum RemotePlayResult
{
    /// <summary>Playback was started.</summary>
    Started,

    /// <summary>The remote's playlist is out of date; it should reload and let the user tap again.</summary>
    StalePlaylist,

    /// <summary>The index is outside the playlist.</summary>
    NotFound,

    /// <summary>The media engine is not ready yet.</summary>
    NotReady
}
