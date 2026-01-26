using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace ChurchDisplayApp;

public sealed class RemoteControlServer
{
    private IHost? _host;

    public bool IsRunning => _host != null;

    public async Task StartAsync(MainWindow mainWindow, int port, CancellationToken cancellationToken = default)
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
            await context.Response.WriteAsync(GetHtml(port));
        });

        app.MapGet("/api/playlist", () =>
        {
            var items = mainWindow.GetPlaylistItemsForRemote();
            return Results.Json(items);
        });

        app.MapPost("/api/play/{index:int}", (int index) =>
        {
            mainWindow.RemotePlayIndex(index);
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/stop", () =>
        {
            mainWindow.RemoteStop();
            return Results.Ok(new { ok = true });
        });

        app.MapPost("/api/blank", () =>
        {
            mainWindow.RemoteBlank();
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

    private static string GetHtml(int port)
    {
        // No external assets: works offline on a local network.
        var portSuffix = port == 80 ? string.Empty : $":{port}";
        return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Church Display Remote</title>
  <style>
    :root { color-scheme: light dark; }
    body { font-family: system-ui, -apple-system, Segoe UI, Roboto, Arial, sans-serif; margin: 0; padding: 16px; }
    h1 { margin: 0 0 12px 0; font-size: 20px; }
    .row { display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 12px; }
    button { font-size: 18px; padding: 14px 14px; border-radius: 10px; border: 1px solid #3a3a3a; background: #1f6feb; color: white; flex: 1 1 140px; }
    button.secondary { background: #444; }
    button.danger { background: #b42318; }
    .list { display: grid; gap: 8px; }
    .item { width: 100%; text-align: left; background: #2b2b2b; display: flex; align-items: center; gap: 8px; }
    .item-icon { font-size: 20px; min-width: 24px; text-align: center; }
    .item-text { flex: 1; }
    .item small { display: none; }
    .footer { opacity: .7; font-size: 12px; margin-top: 14px; }
  </style>
</head>
<body>
  <h1>Church Display Remote</h1>

  <div class="row">
    <button class="danger" onclick="post('/api/blank')">Black Screen</button>
    <button class="secondary" onclick="post('/api/stop')">Stop</button>
    <button class="secondary" onclick="load()">Refresh</button>
  </div>

  <div id="status" class="footer"></div>
  <div id="list" class="list"></div>

  <div class="footer">Open on phone: http://&lt;PC-IP&gt;{{portSuffix}}/ (or http://&lt;PC-NAME&gt;{{portSuffix}}/)</div>

  <script>
    async function post(url) {
      try {
        await fetch(url, { method: 'POST' });
      } catch (e) {
        setStatus('Error: ' + e);
      }
    }

    function setStatus(msg) {
      document.getElementById('status').textContent = msg;
    }

    async function load() {
      setStatus('Loading playlist...');
      const listEl = document.getElementById('list');
      listEl.innerHTML = '';

      try {
        const res = await fetch('/api/playlist');
        const items = await res.json();

        if (!items || items.length === 0) {
          setStatus('Playlist is empty.');
          return;
        }

        setStatus('Tap an item to play.');

        for (const it of items) {
          const btn = document.createElement('button');
          btn.className = 'item';
          btn.onclick = () => post('/api/play/' + it.index);

          // Add icon based on file extension
          const icon = document.createElement('span');
          icon.className = 'item-icon';
          const ext = it.fileName.split('.').pop()?.toLowerCase();
          if (['jpg', 'jpeg', 'png', 'bmp', 'gif'].includes(ext)) {
            icon.textContent = '🖼️';
          } else if (['mp4', 'mov', 'wmv', 'mkv'].includes(ext)) {
            icon.textContent = '🎬';
          } else if (['mp3', 'wav', 'flac', 'wma'].includes(ext)) {
            icon.textContent = '🎵';
          } else {
            icon.textContent = '📄';
          }

          // Add file name
          const text = document.createElement('span');
          text.className = 'item-text';
          text.textContent = it.fileName;

          btn.appendChild(icon);
          btn.appendChild(text);

          // Hide full path (already hidden via CSS)
          if (it.fullPath) {
            const sm = document.createElement('small');
            sm.textContent = it.fullPath;
            btn.appendChild(sm);
          }

          listEl.appendChild(btn);
        }
      } catch (e) {
        setStatus('Error loading playlist: ' + e);
      }
    }

    load();
  </script>
</body>
</html>
""";
    }
}
