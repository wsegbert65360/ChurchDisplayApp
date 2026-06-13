using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;

namespace ChurchDisplayApp.Services;

/// <summary>
/// Enforces a single running instance of ChurchDisplayApp for the current user session.
/// The first instance owns a named mutex and listens on a named pipe; later launches
/// detect the mutex, ask the running instance to surface, and then exit. This avoids
/// two projectors, port-8088 conflicts, and duplicate media control.
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    // "Local\" scopes the mutex to the current user session, which is the right level
    // for a single-operator display app and avoids cross-user permission issues.
    private const string MutexName = @"Local\ChurchDisplayApp-SingleInstance-Mutex";
    private const string PipeName = "ChurchDisplayApp-SingleInstance-Pipe";
    private const string ActivateMessage = "ACTIVATE";
    private const int SignalRetryAttempts = 5;
    private const int SignalRetryDelayMs = 250;

    private Mutex? _mutex;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    /// <summary>Raised on the first instance when another launch asks it to come forward.</summary>
    public event EventHandler? ActivationRequested;

    /// <summary>
    /// Attempts to become the single running instance. Returns true when this process is the
    /// first/only instance (the caller should continue normally); false when another instance is
    /// already running (the caller should exit immediately). In the second case the running
    /// instance is asked to bring its window to the foreground before this method returns.
    /// </summary>
    /// <remarks>
    /// A crashed previous instance is recovered from automatically: <see cref="Mutex.WaitOne"/>
    /// throws <see cref="AbandonedMutexException"/> (and still grants ownership) when the prior
    /// holder died without releasing, so a hard crash can never wedge future launches.
    /// </remarks>
    public bool TryAcquire()
    {
        // Create-or-open the named mutex WITHOUT taking initial ownership, then try to grab it.
        // WaitOne(0) acquires a free mutex, throws (and still grants ownership) if the previous
        // holder crashed, and returns false ONLY when a live process still owns it.
        _mutex = new Mutex(initiallyOwned: false, name: MutexName);

        bool acquired;
        try
        {
            acquired = _mutex.WaitOne(TimeSpan.Zero, exitContext: false);
        }
        catch (AbandonedMutexException)
        {
            // Previous owner died without releasing — we now own the mutex.
            acquired = true;
            Serilog.Log.Warning("Single-instance mutex was abandoned by a previous run; taking over.");
        }

        if (!acquired)
        {
            Serilog.Log.Information("Another instance is already running; signaling it and exiting.");
            SignalExistingInstance();
            try { _mutex.Dispose(); } catch { }
            _mutex = null;
            return false;
        }

        StartPipeListener();
        return true;
    }

    /// <summary>Connects to the running instance's pipe and asks it to surface.</summary>
    private static void SignalExistingInstance()
    {
        // We currently hold the foreground (the user just launched us). Grant the existing
        // instance the right to steal foreground, otherwise Windows would only flash its taskbar.
        try { NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY); }
        catch { /* P/Invoke failure is non-fatal */ }

        // The first instance owns the mutex a few ms before its pipe listener starts, so retry
        // briefly to cover that startup ordering window.
        for (int attempt = 1; attempt <= SignalRetryAttempts; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(timeout: 1000);
                using var writer = new StreamWriter(client);
                writer.Write(ActivateMessage);
                writer.Flush();
                Serilog.Log.Information("Signaled existing instance to activate (attempt {Attempt}).", attempt);
                return;
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, "Activation signal attempt {Attempt} failed", attempt);
                Thread.Sleep(SignalRetryDelayMs);
            }
        }

        Serilog.Log.Warning(
            "Could not signal the existing instance after {Attempts} attempts. " +
            "If the app appears stuck, it can be force-closed from Task Manager.",
            SignalRetryAttempts);
    }

    /// <summary>Listens for activation signals from later launches until disposed.</summary>
    private void StartPipeListener()
    {
        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var server = new NamedPipeServerStream(
                        PipeName, PipeDirection.In, maxNumberOfServerInstances: 1);

                    try
                    {
                        await server.WaitForConnectionAsync(_cts.Token);
                        _cts.Token.ThrowIfCancellationRequested();

                        using (var reader = new StreamReader(server, leaveOpen: true))
                        {
                            var message = reader.ReadToEnd();
                            if (message.Contains(ActivateMessage, StringComparison.Ordinal))
                            {
                                Serilog.Log.Information("Activation requested by another launch.");
                                ActivationRequested?.Invoke(this, EventArgs.Empty);
                            }
                        }
                    }
                    finally
                    {
                        server.Dispose();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "Single-instance pipe listener error");
                    try { await Task.Delay(200, _cts.Token); } catch { break; }
                }
            }
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listenTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        try { _cts?.Dispose(); } catch { }
        try { if (_mutex != null && _mutex.WaitOne(0)) _mutex.ReleaseMutex(); } catch { }
        try { _mutex?.Dispose(); } catch { }
        _mutex = null;
    }

    private static class NativeMethods
    {
        public const uint ASFW_ANY = 0xFFFFFFFF;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool AllowSetForegroundWindow(uint dwProcessId);
    }
}
