namespace ChurchDisplayApp.Models;

/// <summary>
/// Defines a service element configuration.
/// </summary>
public class ServiceElementDefinition
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string SettingsProperty { get; init; }

    public static readonly ServiceElementDefinition[] AllElements = new[]
    {
        new ServiceElementDefinition { Key = "CallToWorship", DisplayName = "Call to Worship", SettingsProperty = nameof(AppSettings.CallToWorshipFile) },
        new ServiceElementDefinition { Key = "SongForBeginning", DisplayName = "Song for Beginning of Church", SettingsProperty = nameof(AppSettings.SongForBeginningFile) },
        new ServiceElementDefinition { Key = "Doxology", DisplayName = "Doxology", SettingsProperty = nameof(AppSettings.DoxologyFile) },
        new ServiceElementDefinition { Key = "PraiseSong", DisplayName = "Praise Song", SettingsProperty = nameof(AppSettings.PraiseSongFile) },
        new ServiceElementDefinition { Key = "GloriaPatri", DisplayName = "Gloria Patri", SettingsProperty = nameof(AppSettings.GloriaPatriFile) },
        new ServiceElementDefinition { Key = "LordsPrayer", DisplayName = "Lord's Prayer", SettingsProperty = nameof(AppSettings.LordsPrayerFile) },
        new ServiceElementDefinition { Key = "PrayerSong", DisplayName = "Prayer Song", SettingsProperty = nameof(AppSettings.PrayerSongFile) },
        new ServiceElementDefinition { Key = "CommunionSong", DisplayName = "Communion Song", SettingsProperty = nameof(AppSettings.CommunionSongFile) },
        new ServiceElementDefinition { Key = "ChildrensMomentSong", DisplayName = "Childrens Moment Song", SettingsProperty = nameof(AppSettings.ChildrensMomentSongFile) },
        new ServiceElementDefinition { Key = "InvitationSong", DisplayName = "Invitation Song", SettingsProperty = nameof(AppSettings.InvitationSongFile) },
        new ServiceElementDefinition { Key = "EndingSong", DisplayName = "Ending Song", SettingsProperty = nameof(AppSettings.EndingSongFile) }
    };
}
