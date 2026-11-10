using System.IO;
using ChurchDisplayApp.Models;
using ChurchDisplayApp.Services;
using Xunit;

namespace ChurchDisplayApp.Tests;

public sealed class PlaylistManagerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cda-tests-" + Guid.NewGuid().ToString("N"));

    public PlaylistManagerTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private string MakeMedia(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, new byte[] { 0 });
        return path;
    }

    private string WritePlaylist(string json)
    {
        var path = Path.Combine(_dir, "list.cdplaylist");
        File.WriteAllText(path, json);
        return path;
    }

    private static string Json(string s) => System.Text.Json.JsonSerializer.Serialize(s);

    [Fact]
    public void Loads_legacy_array_of_paths()
    {
        var song = MakeMedia("song.mp3");
        var pm = new PlaylistManager();

        var problems = pm.LoadPlaylist(WritePlaylist($"[{Json(song)}]"));

        Assert.Empty(problems);
        Assert.Single(pm.Items);
        Assert.Equal(song, pm.Items[0].FullPath);
        Assert.Equal(1.0, pm.Items[0].Volume);
        Assert.False(pm.IsDirty);
    }

    [Fact]
    public void Loads_v2_and_clamps_volume()
    {
        var song = MakeMedia("song.mp3");
        var pm = new PlaylistManager();

        pm.LoadPlaylist(WritePlaylist($"{{\"version\":2,\"items\":[{{\"fullPath\":{Json(song)},\"volume\":4.5}}]}}"));

        Assert.Equal(1.0, pm.Items[0].Volume);
    }

    [Fact]
    public void Keeps_missing_files_flagged_instead_of_dropping_them()
    {
        var missing = Path.Combine(_dir, "gone.mp4");
        var pm = new PlaylistManager();

        var problems = pm.LoadPlaylist(WritePlaylist($"[{Json(missing)}]"));

        Assert.Single(pm.Items);
        Assert.True(pm.Items[0].IsMissing);
        Assert.Contains(missing, problems);
    }

    [Fact]
    public void Resolves_relative_path_when_absolute_path_moved()
    {
        var song = MakeMedia("song.mp3");
        var pm = new PlaylistManager();

        pm.LoadPlaylist(WritePlaylist(
            $"{{\"version\":2,\"items\":[{{\"fullPath\":\"Z:\\\\old\\\\song.mp3\",\"relativePath\":\"song.mp3\",\"volume\":0.5}}]}}"));

        Assert.Equal(song, pm.Items[0].FullPath);
        Assert.True(pm.IsDirty); // the path changed, so the file no longer matches
    }

    [Fact]
    public void Rejects_playlist_from_newer_version_and_keeps_current_list()
    {
        var song = MakeMedia("song.mp3");
        var pm = new PlaylistManager();
        pm.LoadPlaylist(WritePlaylist($"[{Json(song)}]"));

        Assert.Throws<InvalidOperationException>(() =>
            pm.LoadPlaylist(WritePlaylist("{\"version\":99,\"items\":[]}")));

        Assert.Single(pm.Items);
    }

    [Fact]
    public void Corrupt_file_keeps_current_list()
    {
        var song = MakeMedia("song.mp3");
        var pm = new PlaylistManager();
        pm.LoadPlaylist(WritePlaylist($"[{Json(song)}]"));

        Assert.Throws<InvalidOperationException>(() => pm.LoadPlaylist(WritePlaylist("{ not json")));

        Assert.Single(pm.Items);
    }

    [Fact]
    public void Save_then_load_round_trips_volume()
    {
        var song = MakeMedia("song.mp3");
        var pm = new PlaylistManager();
        pm.Items.Add(new PlaylistItem(song, 0.4));
        var path = Path.Combine(_dir, "saved.cdplaylist");

        pm.SavePlaylist(path);
        var reloaded = new PlaylistManager();
        reloaded.LoadPlaylist(path);

        Assert.False(pm.IsDirty);
        Assert.Equal(0.4, reloaded.Items[0].Volume, 3);
    }

    [Fact]
    public void Version_changes_on_every_list_change()
    {
        var pm = new PlaylistManager();
        var v0 = pm.Version;

        pm.Items.Add(new PlaylistItem(MakeMedia("a.mp3")));
        var v1 = pm.Version;
        pm.Items.Add(new PlaylistItem(MakeMedia("b.mp3")));
        pm.Items.Move(0, 1);
        var v2 = pm.Version;
        pm.Clear();

        Assert.True(v1 > v0);
        Assert.True(v2 > v1);
        Assert.True(pm.Version > v2);
    }
}
