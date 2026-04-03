using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.IO;
using ChurchDisplayApp.Services;
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

    /// <summary>Gets a value indicating whether the remote control server is currently running.</summary>
    public bool IsRunning => _host != null;

    /// <summary>
    /// Starts the remote control server on the specified port.
    /// </summary>
    /// <param name="dispatcher">The UI dispatcher for executing commands on the main thread.</param>
    /// <param name="controller">The controller that handles display and media actions.</param>
    /// <param name="port">The port number to listen on.</param>
    /// <param name="cancellationToken">An optional token to cancel the startup process.</param>
    public async Task StartAsync(System.Windows.Threading.Dispatcher dispatcher, IDisplayController controller, int port, CancellationToken cancellationToken = default)
    {
        if (_host != null)
        {
            return;
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(RemoteControlServer).Assembly.FullName,
        });

        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

        var app = builder.Build();

        app.UseSerilogRequestLogging();

        app.UseExceptionHandler("/error");
        app.MapGet("/error", (HttpContext context) =>
        {
            Log.Error("Unhandled exception in remote control API");
            return Results.Problem("An internal error occurred");
        });

        app.MapGet("/", async context =>
        {
            context.Response.Headers["Cache-Control"] = "public, max-age=3600";
            context.Response.ContentType = "text/html; charset=utf-8";
            var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RemoteControl", "index.html");
            if (File.Exists(htmlPath))
            {
                await context.Response.WriteAsync(await File.ReadAllTextAsync(htmlPath));
            }
            else
            {
                await context.Response.WriteAsync("<h1>Remote Control File Not Found</h1>");
            }
        });

        app.MapGet("/api/playlist", () =>
        {
            var items = dispatcher.Invoke(() => controller.GetPlaylistItems());
            return Results.Json(items);
        });

        app.MapPost("/api/play/{index:int}", (int index) =>
        {
            Log.Information("Remote: Play index {Index}", index);
            dispatcher.BeginInvoke(() => controller.PlayIndex(index));
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/play", () =>
        {
            Log.Information("Remote: Play");
            dispatcher.BeginInvoke(() => controller.Play());
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/pause", () =>
        {
            Log.Information("Remote: Pause");
            dispatcher.BeginInvoke(() => controller.Pause());
            return Results.Ok(new { ok = true });
        });

        app.MapGet("/api/status", () =>
        {
            var status = dispatcher.Invoke(() => controller.GetStatus());
            return Results.Json(status);
        });

        app.MapPost("/api/stop", () =>
        {
            Log.Information("Remote: Stop");
            dispatcher.BeginInvoke(() => controller.Stop());
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/blank", () =>
        {
            Log.Information("Remote: Blank");
            dispatcher.BeginInvoke(() => controller.Blank());
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/volume/up", () =>
        {
            Log.Information("Remote: Volume Up");
            dispatcher.BeginInvoke(() => controller.VolumeUp());
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/volume/down", () =>
        {
            Log.Information("Remote: Volume Down");
            dispatcher.BeginInvoke(() => controller.VolumeDown());
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/volume/{level:double}", (double level) =>
        {
            Log.Information("Remote: Set Volume {Level}", level);
            dispatcher.BeginInvoke(() => controller.SetVolume(level));
            return Results.Ok(new { ok = true });
        });

        // Amen endpoint
        app.MapPost("/api/amen", () =>
        {
            Log.Information("Remote: Amen");
            dispatcher.BeginInvoke(() => controller.Amen());
            return Results.Ok(new { ok = true });
        });

        _host = app;

        await app.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Stops the remote control server.
    /// </summary>
    /// <param name="cancellationToken">An optional token to cancel the shutdown process.</param>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_host == null)
        {
            return;
        }

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
