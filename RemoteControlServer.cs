using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.IO;
using ChurchDisplayApp.Services;

namespace ChurchDisplayApp;

public sealed class RemoteControlServer
{
    private IHost? _host;

    public bool IsRunning => _host != null;

    public async Task StartAsync(System.Windows.Threading.Dispatcher dispatcher, RemoteControlCoordinator coordinator, int port, CancellationToken cancellationToken = default)
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

        app.MapGet("/", async context =>
        {
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
            var items = dispatcher.Invoke(() => coordinator.GetPlaylistItems());
            return Results.Json(items);
        });

        app.MapPost("/api/play/{index:int}", (int index) =>
        {
            dispatcher.BeginInvoke(() => coordinator.PlayIndex(index));
            return Results.Ok(new { ok = true });
        });

        app.MapGet("/api/status", () =>
        {
            var status = dispatcher.Invoke(() => coordinator.GetStatus());
            return Results.Json(status);
        });

        app.MapPost("/api/stop", () =>
        {
            dispatcher.BeginInvoke(() => coordinator.Stop());
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/blank", () =>
        {
            dispatcher.BeginInvoke(() => coordinator.Blank());
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/volume/{level:double}", (double level) =>
        {
            dispatcher.BeginInvoke(() => coordinator.SetVolume(level));
            return Results.Ok(new { ok = true });
        });

        _host = app;

        await app.StartAsync(cancellationToken);
    }

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
