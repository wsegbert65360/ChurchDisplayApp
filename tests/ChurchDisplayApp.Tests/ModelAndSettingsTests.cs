using ChurchDisplayApp.Models;
using ChurchDisplayApp.Services;
using Xunit;

namespace ChurchDisplayApp.Tests;

public class PlaylistItemTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Non_numeric_volume_is_ignored(double bad)
    {
        var item = new PlaylistItem(@"C:\x\song.mp3", 0.6);

        item.Volume = bad;

        Assert.Equal(0.6, item.Volume, 3);
    }

    [Fact]
    public void Constructor_with_NaN_volume_uses_full_volume()
    {
        var item = new PlaylistItem(@"C:\x\song.mp3", double.NaN);
        Assert.Equal(1.0, item.Volume);
    }

    [Fact]
    public void Volume_is_clamped()
    {
        var item = new PlaylistItem(@"C:\x\song.mp3") { Volume = 2 };
        Assert.Equal(1.0, item.Volume);
        item.Volume = -1;
        Assert.Equal(0.0, item.Volume);
    }

    [Fact]
    public void Extension_check_ignores_case()
    {
        Assert.True(MediaConstants.IsVideo(@"C:\x\CLIP.MP4"));
        Assert.True(MediaConstants.IsImage(@"C:\x\Photo.JPG"));
        Assert.False(MediaConstants.IsSupported(@"C:\x\notes.txt"));
    }
}

public class AppSettingsTests
{
    [Fact]
    public void Validate_clamps_and_rejects_unknown_monitor_side()
    {
        var s = new AppSettings
        {
            DefaultServiceVolume = 3,
            PlaylistFontSize = 100,
            MainWindowLeftColumnProportion = null,
            DisplayMonitorSide = "Middle"
        };

        s.Validate();

        Assert.Equal(1.0, s.DefaultServiceVolume);
        Assert.Equal(30.0, s.PlaylistFontSize);
        Assert.Equal(0.25, s.MainWindowLeftColumnProportion);
        Assert.Null(s.DisplayMonitorSide);
    }

    [Fact]
    public void Migration_seeds_slots_from_legacy_properties_once()
    {
        var s = new AppSettings { DoxologyFile = @"C:\x\doxology.mp4" };

        s.MigrateToServiceSlots();

        Assert.Equal(11, s.ServiceSlots.Count);
        Assert.Contains(s.ServiceSlots, slot => slot.DisplayName == "Doxology" && slot.FilePath == @"C:\x\doxology.mp4");
        Assert.Null(s.DoxologyFile);
        Assert.Equal(AppSettings.CurrentSettingsVersion, s.SettingsVersion);

        // Slots the user deleted must not come back on the next start.
        s.ServiceSlots.Clear();
        s.MigrateToServiceSlots();
        Assert.Empty(s.ServiceSlots);
    }
}

public class MonitorSelectionTests
{
    private static MonitorService.MonitorBounds M(int left, int width) =>
        new() { Left = left, Right = left + width, Top = 0, Bottom = 1080 };

    [Fact]
    public void Picks_leftmost_and_rightmost_by_position()
    {
        var monitors = new[] { M(0, 1920), M(-1920, 1920), M(1920, 1920) };

        Assert.Equal(-1920, MonitorService.PickBySide(monitors, "Left").Left);
        Assert.Equal(1920, MonitorService.PickBySide(monitors, "Right").Left);
    }
}
