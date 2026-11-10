using System.Windows.Controls;
using System.Windows.Threading;
using ChurchDisplayApp.Models;
using Serilog;

namespace ChurchDisplayApp.Services;

/// <summary>
/// Keeps the operator's preview image in step with the projector output. The refresh rate adapts to what is
/// showing: a few frames a second for video, slow for still images, and very slow when idle.
/// </summary>
public sealed class LivePreviewController
{
    private enum PreviewKind { None, Image, Video }

    private readonly Func<LiveOutputWindow?> _getLiveWindow;
    private readonly Image _previewImage;
    private readonly TextBlock _previewLabel;
    private readonly DispatcherTimer _timer;
    private PreviewKind? _lastKind;
    private bool _updateInProgress;

    public LivePreviewController(Func<LiveOutputWindow?> getLiveWindow, Image previewImage, TextBlock previewLabel)
    {
        _getLiveWindow = getLiveWindow ?? throw new ArgumentNullException(nameof(getLiveWindow));
        _previewImage = previewImage ?? throw new ArgumentNullException(nameof(previewImage));
        _previewLabel = previewLabel ?? throw new ArgumentNullException(nameof(previewLabel));

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(AppConstants.UI.LivePreviewIdleIntervalMs)
        };
        _timer.Tick += Timer_Tick;
    }

    /// <summary>Gets how many refreshes were skipped because the previous one was still running.</summary>
    public long DroppedFrames { get; private set; }

    public void Start() => _timer.Start();

    public void Stop()
    {
        _timer.Stop();
        if (DroppedFrames > 0)
            Log.Information("Live preview dropped {Count} frames during this session", DroppedFrames);
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        // Drop this tick if the previous snapshot is still in flight.
        if (_updateInProgress)
        {
            DroppedFrames++;
            return;
        }

        _updateInProgress = true;
        try
        {
            await UpdateAsync();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Live preview update failed");
        }
        finally
        {
            _updateInProgress = false;
        }
    }

    private async Task UpdateAsync()
    {
        var liveWindow = _getLiveWindow();
        if (liveWindow == null || liveWindow.IsDisposed || !liveWindow.IsLoaded)
        {
            _previewImage.Source = null;
            _previewLabel.Text = "Live Output (Loading...)";
            return;
        }

        var currentPath = liveWindow.CurrentMediaPath;
        var kind = currentPath == null ? PreviewKind.None
            : MediaConstants.IsImage(currentPath) ? PreviewKind.Image
            : MediaConstants.IsVideo(currentPath) ? PreviewKind.Video
            : PreviewKind.None;

        if (kind != _lastKind)
        {
            _lastKind = kind;
            _timer.Interval = TimeSpan.FromMilliseconds(kind switch
            {
                PreviewKind.Video => AppConstants.UI.LivePreviewIntervalMs,
                PreviewKind.Image => AppConstants.UI.LivePreviewImageIntervalMs,
                _ => AppConstants.UI.LivePreviewIdleIntervalMs
            });
        }

        // Paused video: VLC returns no snapshot, so keep the last frame instead of polling.
        if (kind == PreviewKind.Video && _previewImage.Source != null && !liveWindow.IsPlaying)
            return;

        // For images this is just the displayed bitmap (no disk I/O); for video it is a VLC snapshot.
        var snapshot = await liveWindow.GetCurrentSnapshotAsync();

        if (snapshot != null)
        {
            if (!ReferenceEquals(snapshot, _previewImage.Source))
                _previewImage.Source = snapshot;
            _previewLabel.Text = "Live Output Preview";
        }
        else if (string.IsNullOrEmpty(currentPath))
        {
            // Keep the last frame while loading or blanked-with-media; clear only when nothing is loaded.
            _previewImage.Source = null;
            _previewLabel.Text = "Live Output (No Media)";
        }
    }
}
