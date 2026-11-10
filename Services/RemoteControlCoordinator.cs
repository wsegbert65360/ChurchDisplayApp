using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ChurchDisplayApp.Interfaces;
using ChurchDisplayApp.Models;
using QRCoder;
using Serilog;

namespace ChurchDisplayApp.Services;

/// <summary>
/// Starts and stops the web remote: tries the preferred port then the fallback, checks the firewall rule,
/// and builds the URL and QR code shown to the operator.
/// </summary>
public sealed class RemoteControlCoordinator
{
    private static readonly string[] FirewallRuleNames = { "ChurchDisplayApp Remote", "ChurchDisplayApp Remote Fallback" };

    private readonly RemoteControlServer _server = new();
    private readonly Dispatcher _dispatcher;
    private readonly IDisplayController _controller;

    public RemoteControlCoordinator(Dispatcher dispatcher, IDisplayController controller)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
    }

    /// <summary>Gets whether the web server is running.</summary>
    public bool IsRunning => _server.IsRunning;

    /// <summary>
    /// Starts the server on the preferred port, falling back to the second port.
    /// </summary>
    /// <returns>The port in use, or null when neither port could be opened.</returns>
    public async Task<int?> StartAsync()
    {
        foreach (var port in new[] { AppConstants.Network.RemoteControlPortPreferred, AppConstants.Network.RemoteControlPortFallback })
        {
            try
            {
                await _server.StartAsync(_dispatcher, _controller, port);
                Log.Information("Remote control server running on port {Port}", port);
                await VerifyFirewallRuleAsync(port);
                return port;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to start remote control server on port {Port}", port);
            }
        }

        Log.Error("Remote control server failed to start on any port");
        return null;
    }

    /// <summary>Stops the server, giving up after <paramref name="timeout"/>.</summary>
    public async Task StopAsync(TimeSpan timeout)
    {
        if (!_server.IsRunning)
            return;

        using var cts = new CancellationTokenSource(timeout);
        await _server.StopAsync(cts.Token);
    }

    /// <summary>Builds the address phones should open, preferring this PC's LAN IPv4 address.</summary>
    public static string GetRemoteUrl(int port)
    {
        var portSuffix = port == 80 ? string.Empty : $":{port}";
        var host = GetLocalIPv4Address() ?? Environment.MachineName;
        return $"http://{host}{portSuffix}/";
    }

    /// <summary>Renders <paramref name="text"/> as a frozen QR code image, or null on failure.</summary>
    public static BitmapImage? CreateQrImage(string text)
    {
        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            var pngBytes = new PngByteQRCode(data).GetGraphic(10);

            var image = new BitmapImage();
            using (var ms = new MemoryStream(pngBytes))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();
            }
            image.Freeze();
            return image;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to generate QR code");
            return null;
        }
    }

    private static string? GetLocalIPv4Address()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(ua.Address)) continue;

                    var address = ua.Address.ToString();
                    if (address.StartsWith("169.254.", StringComparison.Ordinal)) continue; // link-local

                    return address;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to enumerate local IP addresses");
        }

        return null;
    }

    /// <summary>
    /// Logs a warning when the installer's firewall rule is missing. Each netsh call is limited to 3 seconds
    /// so a hung netsh can never stall startup.
    /// </summary>
    private static async Task VerifyFirewallRuleAsync(int port)
    {
        try
        {
            foreach (var ruleName in FirewallRuleNames)
            {
                var output = await RunNetshAsync($"advfirewall firewall show rule name=\"{ruleName}\"", TimeSpan.FromSeconds(3));
                if (output == null)
                {
                    Log.Warning("Timed out while checking firewall rule '{RuleName}'", ruleName);
                    continue;
                }

                if (output.Contains(ruleName, StringComparison.Ordinal))
                {
                    Log.Information("Firewall rule '{RuleName}' found — remote control accessible on port {Port}", ruleName, port);
                    return;
                }
            }

            Log.Warning("No firewall rule found for remote control port {Port}. " +
                "The rule is normally created during installation. " +
                "Users on other devices may not be able to connect. " +
                "Reinstall the app or manually create a firewall rule for TCP port {Port}.", port);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not verify firewall rule for port {Port}", port);
        }
    }

    /// <summary>Runs netsh and returns its standard output, or null if it did not finish in time.</summary>
    private static async Task<string?> RunNetshAsync(string arguments, TimeSpan timeout)
    {
        using var proc = Process.Start(new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        if (proc == null)
            return string.Empty;

        var outputTask = proc.StandardOutput.ReadToEndAsync();
        var errorTask = proc.StandardError.ReadToEndAsync();

        using var timeoutCts = new CancellationTokenSource(timeout);
        try
        {
            await proc.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* already gone */ }
            // Observe the read tasks so a later failure does not surface as an unobserved exception.
            _ = outputTask.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            _ = errorTask.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            return null;
        }

        await errorTask;
        return await outputTask;
    }
}
