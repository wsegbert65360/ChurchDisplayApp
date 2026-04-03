using System;

namespace ChurchDisplayApp.Models;

/// <summary>
/// Represents a single configurable service element slot (e.g. "Call to Worship").
/// </summary>
public class ServiceSlot
{
    /// <summary>Unique identifier for this slot. Set once at creation, never changes.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>The display name shown in the UI (user-editable).</summary>
    public string DisplayName { get; set; } = "New Slot";

    /// <summary>
    /// Full path to the assigned media file.
    /// Null or empty means no file is assigned.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// If true, FilePath is preserved between sessions and not cleared after use.
    /// If false, FilePath is cleared when the window is opened next time.
    /// </summary>
    public bool IsSticky { get; set; } = false;

    /// <summary>
    /// The last folder the user browsed to when selecting a file for this slot.
    /// Used as the initial directory for the file picker.
    /// </summary>
    public string? LastUsedFolder { get; set; }

    /// <summary>
    /// Per-slot default volume override (0.0 – 1.0).
    /// When negative (default -1), the global DefaultServiceVolume slider is used instead.
    /// </summary>
    public double DefaultVolume { get; set; } = -1.0;
}
