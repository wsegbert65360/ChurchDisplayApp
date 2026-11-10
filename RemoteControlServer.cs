using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows.Threading;
using ChurchDisplayApp.Interfaces;
using ChurchDisplayApp.Models;
using Serilog;

namespace ChurchDisplayApp;

/// <summary>
/// A self-contained HTTP server that provides a web-based remote control interface
/// for the ChurchDisplayApp.
/// </summary>
public sealed class RemoteControlServer
{
    private IHost? _host;
    private volatile bool _isShuttingDown;

    /// <summary>Gets a value indicating whether the remote control server is currently running.</summary>
    public bool IsRunning => _host != null;

    /// <summary>
    /// Starts the remote control server on the specified port.
    /// </summary>
    /// <param name="dispatcher">The UI dispatcher for executing commands on the main thread.</param>
    /// <param name="controller">The controller that handles display and media actions.</param>
    /// <param name="port">The port number to listen on.</param>
    /// <param name="cancellationToken">An optional token to cancel the startup process.</param>
    public async Task StartAsync(Dispatcher dispatcher, IDisplayController controller, int port, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(controller);

        if (_host != null)
            return;

        // Reset shutdown gate for a fresh server instance.
        _isShuttingDown = false;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(RemoteControlServer).Assembly.FullName,
        });

        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

        var app = builder.Build();

        var remoteControlDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RemoteControl");
        var htmlPath = Path.Combine(remoteControlDir, "index.html");

        if (!Directory.Exists(remoteControlDir))
        {
            Log.Warning("RemoteControl directory not found at {Path}. Web remote control will not work.", remoteControlDir);
        }
        else
        {
            // Serve static files (lucide.min.js, etc.) from the RemoteControl folder.
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(remoteControlDir),
                RequestPath = ""
            });
        }

        app.MapGet("/", async (HttpContext context) =>
        {
            context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            context.Response.ContentType = "text/html; charset=utf-8";

            try
            {
                if (!File.Exists(htmlPath))
                {
                    Log.Error("index.html not found at {Path}", htmlPath);
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync(
                        "<html><body style='font-family:sans-serif;text-align:center;padding:40px'>" +
                        "<h1>Church Display Remote</h1>" +
                        "<p style='color:red'>Remote control files not found.</p>" +
                        "<p>Please reinstall the application.</p></body></html>");
                    return;
                }

                await context.Response.SendFileAsync(htmlPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to serve index.html from {Path}", htmlPath);
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync("<html><body><h1>Error loading remote control</h1></body></html>");
                }
            }
        });

        // --- Queries ---

        app.MapGet("/api/playlist", async () =>
        {
            if (_isShuttingDown) return Results.StatusCode(503);
            var playlist = await dispatcher.InvokeAsync(controller.GetPlaylist);
            return Results.Json(playlist);
        });

        app.MapGet("/api/status", async () =>
        {
            if (_isShuttingDown) return Results.StatusCode(503);
            var status = await dispatcher.InvokeAsync(controller.GetStatus);
            return Results.Json(status);
        });

        // --- Actions ---

        app.MapPost("/api/play/{index:int}", async (int index, int? v) =>
        {
            if (_isShuttingDown) return Results.StatusCode(503);
            Log.Information("Remote: Play index {Index} (playlist version {Version})", index, v);

            // Runs synchronously on the UI thread so a stale request is rejected before anything plays.
            var result = await dispatcher.InvokeAsync(() => controller.PlayIndex(index, v));
            return result switch
            {
                RemotePlayResult.Started => Results.Ok(new { ok = true }),
                RemotePlayResult.StalePlaylist => Results.Conflict(new { ok = false, reason = "playlist-changed" }),
                RemotePlayResult.NotFound => Results.NotFound(new { ok = false }),
                _ => Results.StatusCode(503)
            };
        });

        MapAction(app, dispatcher, "/api/play", "Play", controller.Play);
        MapAction(app, dispatcher, "/api/pause", "Pause", controller.Pause);
        MapAction(app, dispatcher, "/api/stop", "Stop", controller.Stop);
        MapAction(app, dispatcher, "/api/blank", "Blank", controller.Blank);
        MapAction(app, dispatcher, "/api/amen", "Amen", controller.Amen);
        MapAction(app, dispatcher, "/api/volume/up", "Volume Up", controller.VolumeUp);
        MapAction(app, dispatcher, "/api/volume/down", "Volume Down", controller.VolumeDown);

        app.MapPost("/api/volume/{level:double}", (double level) =>
        {
            if (_isShuttingDown) return Results.StatusCode(503);

            // The route constraint accepts "NaN" and "Infinity"; NaN would poison the item volume.
            if (!double.IsFinite(level))
            {
                Log.Warning("Remote: rejected non-numeric volume {Input}", level);
                return Results.BadRequest(new { ok = false, reason = "volume must be a number between 0 and 1" });
            }

            var clamped = Math.Clamp(level, 0.0, 1.0);
            if (Math.Abs(level - clamped) > 0.0001)
                Log.Warning("Remote: Set Volume out of range {Input}; clamped to {Clamped}", level, clamped);
            else
                Log.Information("Remote: Set Volume {Level}", level);

            dispatcher.BeginInvoke(() => controller.SetVolume(clamped));
            return Results.Ok(new { ok = true, applied = clamped });
        });

        try
        {
            _host = app;
            await app.StartAsync(cancellationToken);
        }
        catch
        {
            _host = null;
            throw;
        }
    }

    /// <summary>
    /// Maps a fire-and-forget POST endpoint that runs <paramref name="action"/> on the UI thread.
    /// </summary>
    private void MapAction(WebApplication app, Dispatcher dispatcher, string route, string name, Action action)
    {
        app.MapPost(route, () =>
        {
            if (_isShuttingDown) return Results.StatusCode(503);
            Log.Information("Remote: {Action}", name);
            dispatcher.BeginInvoke(() =>
            {
                try { action(); }
                catch (Exception ex) { Log.Error(ex, "Remote action {Action} failed", name); }
            });
            return Results.Ok(new { ok = true });
        });
    }

    /// <summary>
    /// Stops the remote control server.
    /// </summary>
    /// <param name="cancellationToken">An optional token to cancel the shutdown process.</param>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_host == null)
            return;

        _isShuttingDown = true;

        try
        {
            await _host.StopAsync(cancellationToken);
        }
        finally
        {
            _host.Dispose();
            _host = null;
        }
    }
}
