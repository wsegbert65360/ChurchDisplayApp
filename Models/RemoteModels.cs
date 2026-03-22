using System.Collections.Generic;

namespace ChurchDisplayApp.Models;

/// <summary>
/// Status information sent to remote control clients.
/// </summary>
public record RemoteStatus(
    string Title, 
    double ProgressPercent, 
    string CurrentTime, 
    string Duration, 
    double Volume,
    string BgmTitle,
    bool IsBgmPlaying
);

/// <summary>
/// Playlist item information sent to remote control clients.
/// </summary>
public record RemotePlaylistItem(
    int Index, 
    string FileName, 
    string FullPath
);
