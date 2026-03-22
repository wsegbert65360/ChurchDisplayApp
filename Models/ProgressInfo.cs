using System;

namespace ChurchDisplayApp.Models;

/// <summary>
/// Represents information about the playback progress of a media file.
/// </summary>
public class ProgressInfo
{
    /// <summary>Gets or sets the progress as a percentage (0.0 to 100.0).</summary>
    public double ProgressPercent { get; set; }
    /// <summary>Gets or sets the current playback time.</summary>
    public TimeSpan CurrentTime { get; set; }
    /// <summary>Gets or sets the total duration of the media.</summary>
    public TimeSpan Duration { get; set; }
}
