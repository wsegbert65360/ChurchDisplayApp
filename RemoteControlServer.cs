using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
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
    public async Task StartAsync(System.Windows.Threading.Dispatcher dispatcher, IDisplayController controller, int port, CancellationToken cancellationToken = default)
    {
        if (_host != null)
        {
            return;
        }

        // Reset shutdown gate for a fresh server instance.
        _isShuttingDown = false;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(RemoteControlServer).Assembly.FullName,
        });

        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

        var app = builder.Build();

        // Serve static files (lucide.min.js, etc.) from the RemoteControl folder
        var remoteControlDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RemoteControl");

        if (!Directory.Exists(remoteControlDir))
        {
            Log.Warning("RemoteControl directory not found at {Path}. Web remote control will not work.", remoteControlDir);
        }
        else
        {
            Log.Information("RemoteControl directory found at {Path}", remoteControlDir);

            // Serve static files (lucide.min.js, etc.) from the RemoteControl folder
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(remoteControlDir),
                RequestPath = "",
                ServeUnknownFileTypes = true
            });
        }

        app.MapGet("/", async (HttpContext context) =>
        {
            var htmlPath = Path.Combine(remoteControlDir, "index.html");
            context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            context.Response.ContentType = "text/html; charset=utf-8";

            if (!File.Exists(htmlPath))

            {
                Log.Error("index.html not found at {Path}", htmlPath);
                context.Response.StatusCode = 500;
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(
                    "<html><body style='font-family:sans-serif;text-align:center;padding:40px'>" +
                    "<h1>Church Display Remote</h1>" +
                    "<p style='color:red'>Remote control files not found.</p>" +
                    "<p>Please reinstall the application.</p></body></html>");
                return;
            }

            try
            {
                var html = await File.ReadAllTextAsync(htmlPath);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(html);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to read index.html from {Path}", htmlPath);
                context.Response.StatusCode = 500;
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync("<html><body><h1>Error loading remote control</h1></body></html>");
            }
        });

        // API endpoints
        app.MapGet("/api/playlist", async () =>
        {
            if (_isShuttingDown) return Results.StatusCode(503);
            if (controller == null) return Results.Problem(statusCode: 500, detail: "Controller not initialized");
            var items = await dispatcher.InvokeAsync(() => controller.GetPlaylistItems());
            return Results.Json(items);
        });

        app.MapPost("/api/play/{index:int}", (int index) =>
        {
            if (_isShuttingDown) return Results.StatusCode(503);
            Log.Information("Remote: Play index {Index}", index);
            if (controller == null) return Results.Problem(statusCode: 500, detail: "Controller not initialized");
            dispatcher.BeginInvoke(() => controller.PlayIndex(index));
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/play", () =>
        {
            if (_isShuttingDown) return Results.StatusCode(503);
            Log.Information("Remote: Play");
            if (controller == null) return Results.Problem(statusCode: 500, detail: "Controller not initialized");
            dispatcher.BeginInvoke(() => controller.Play());
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/pause", () =>
        {
            if (_isShuttingDown) return Results.StatusCode(503);
            Log.Information("Remote: Pause");
            if (controller == null) return Results.Problem(statusCode: 500, detail: "Controller not initialized");
            dispatcher.BeginInvoke(() => controller.Pause());
            return Results.Ok(new { ok = true });
        });

        app.MapGet("/api/status", async () =>
        {
            if (_isShuttingDown) return Results.StatusCode(503);
            if (controller == null) return Results.Problem(statusCode: 500, detail: "Controller not initialized");
            var status = await dispatcher.InvokeAsync(() => controller.GetStatus());
            return Results.Json(status);
        });

        app.MapPost("/api/stop", () =>
        {
            if (_isShuttingDown) return Results.StatusCode(503);
            Log.Information("Remote: Stop");
            if (controller == null) return Results.Problem(statusCode: 500, detail: "Controller not initialized");
            dispatcher.BeginInvoke(() => controller.Stop());
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/blank", () =>
        {
            if (_isShuttingDown) return Results.StatusCode(503);
            Log.Information("Remote: Blank");
            if (controller == null) return Results.Problem(statusCode: 500, detail: "Controller not initialized");
            dispatcher.BeginInvoke(() => controller.Blank());
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/volume/up", () =>
        {
            if (_isShuttingDown) return Results.StatusCode(503);
            if (controller == null) return Results.Problem(statusCode: 500, detail: "Controller not initialized");
            Log.Information("Remote: Volume Up");
            dispatcher.BeginInvoke(() => controller.VolumeUp());
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/volume/down", () =>
        {
            if (_isShuttingDown) return Results.StatusCode(503);
            if (controller == null) return Results.Problem(statusCode: 500, detail: "Controller not initialized");
            Log.Information("Remote: Volume Down");
            dispatcher.BeginInvoke(() => controller.VolumeDown());
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/volume/{level:double}", (double level) =>
        {
            if (_isShuttingDown) return Results.StatusCode(503);
            var clamped = Math.Clamp(level, 0.0, 1.0);
            if (Math.Abs(level - clamped) > 0.0001)
            {
                Log.Warning("Remote: Set Volume out of range {Input}; clamped to {Clamped}", level, clamped);
            }
            else
            {
                Log.Information("Remote: Set Volume {Level}", level);
            }

            if (controller == null) return Results.Problem(statusCode: 500, detail: "Controller not initialized");
            dispatcher.BeginInvoke(() => controller.SetVolume(clamped));
            return Results.Ok(new { ok = true, applied = clamped });
        });

        // Amen endpoint
        app.MapPost("/api/amen", () =>
        {
            if (_isShuttingDown) return Results.StatusCode(503);
            Log.Information("Remote: Amen");
            if (controller == null) return Results.Problem(statusCode: 500, detail: "Controller not initialized");
            dispatcher.BeginInvoke(() => controller.Amen());
            return Results.Ok(new { ok = true });
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
    /// Stops the remote control server.
    /// </summary>
    /// <param name="cancellationToken">An optional token to cancel the shutdown process.</param>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_host == null)
        {
            return;
        }

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
