using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Serilog;

namespace ChurchDisplayApp.Services;

public class MonitorService
{
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

    // CharSet must match MonitorInfoEx: a Unicode-sized struct passed to the ANSI entry point is rejected,
    // which made every monitor look invalid and enumeration return nothing.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public uint cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    public class MonitorBounds
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
        public bool IsPrimary { get; set; }
        public IntPtr Handle { get; set; }

        public int Width => Right - Left;
        public int Height => Bottom - Top;

        public override string ToString()
        {
            return $"{Width}x{Height} at ({Left},{Top}){(IsPrimary ? " [Primary]" : "")}";
        }
    }

    public List<MonitorBounds> GetMonitors()
    {
        var results = new List<MonitorBounds>();

        // Primary method: Win32 EnumDisplayMonitors
        bool EnumCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData)
        {
            MonitorInfoEx mi = new() { cbSize = (uint)Marshal.SizeOf(typeof(MonitorInfoEx)) };
            if (!GetMonitorInfo(hMonitor, ref mi))
                return true; // Skip invalid monitor handles (e.g. disconnected during enumeration)

            const uint MONITORINFOF_PRIMARY = 1;
            bool isPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;

            results.Add(new MonitorBounds
            {
                Left = mi.rcMonitor.left,
                Top = mi.rcMonitor.top,
                Right = mi.rcMonitor.right,
                Bottom = mi.rcMonitor.bottom,
                IsPrimary = isPrimary,
                Handle = hMonitor
            });

            return true;
        }

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, EnumCallback, IntPtr.Zero);

        Log.Information("EnumDisplayMonitors found {Count} monitor(s): {Monitors}",
            results.Count,
            string.Join(", ", results.Select(m => m.ToString())));

        // Fallback: If Win32 found fewer than 2, try System.Windows.Forms.Screen.AllScreens
        // which uses a different enumeration method and can detect displays in "Duplicate" mode
        if (results.Count < 2)
        {
            try
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                Log.Information("Screen.AllScreens found {Count} screen(s): {Screens}",
                    screens.Length,
                    string.Join(", ", screens.Select(s => $"{s.DeviceName} {s.Bounds} Primary={s.Primary}")));

                if (screens.Length > results.Count)
                {
                    results.Clear();
                    foreach (var screen in screens)
                    {
                        results.Add(new MonitorBounds
                        {
                            Left = screen.Bounds.Left,
                            Top = screen.Bounds.Top,
                            Right = screen.Bounds.Right,
                            Bottom = screen.Bounds.Bottom,
                            IsPrimary = screen.Primary
                        });
                    }
                    Log.Information("Using Screen.AllScreens fallback: {Count} monitor(s)", results.Count);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Screen.AllScreens fallback failed");
            }
        }

        return results;
    }

    public void PositionWindowOnMonitor(Window window, MonitorBounds monitor)
    {
        if (window == null || monitor == null)
            return;

        try
        {
            // Move the window with Win32 in screen pixels - the same units the monitor bounds are in.
            // Going through WPF's Left/Top means converting pixels to DIPs, and a wrong scale there
            // leaves the window on the operator's screen, where maximizing then keeps it.
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            window.WindowState = WindowState.Normal;

            if (!SetWindowPos(hwnd, IntPtr.Zero, monitor.Left, monitor.Top, monitor.Width, monitor.Height,
                    SWP_NOZORDER | SWP_NOACTIVATE))
            {
                Log.Warning("SetWindowPos failed (error {Error}) moving live window to {Monitor}",
                    Marshal.GetLastWin32Error(), monitor);
            }

            GetWindowRect(hwnd, out var placed);
            Log.Information("Live window moved to monitor {Monitor}; window now at ({Left},{Top}) {Width}x{Height}",
                monitor, placed.left, placed.top, placed.right - placed.left, placed.bottom - placed.top);

            window.WindowState = WindowState.Maximized;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error positioning window on monitor");
        }
    }

    /// <summary>
    /// Picks the monitor for the projector output. Uses the operator's saved Left/Right choice and only
    /// asks again when nothing is saved, the number of monitors has changed, or <paramref name="forceAsk"/> is set.
    /// </summary>
    public MonitorBounds? SelectDisplayMonitor(List<MonitorBounds> monitors, AppSettings settings, bool forceAsk = false)
    {
        if (monitors == null || monitors.Count == 0)
            return null;

        if (monitors.Count == 1)
            return monitors[0];

        var side = settings.DisplayMonitorSide;
        var needsAsk = forceAsk || side == null || settings.DisplayMonitorCount != monitors.Count;

        if (needsAsk)
        {
            var selectionWindow = new MonitorSelectionWindow();
            if (Application.Current?.MainWindow is { IsLoaded: true } owner)
                selectionWindow.Owner = owner;

            if (selectionWindow.ShowDialog() == true)
            {
                side = selectionWindow.Result == MonitorSelectionWindow.SelectionResult.Left ? "Left" : "Right";
                settings.DisplayMonitorSide = side;
                settings.DisplayMonitorCount = monitors.Count;
                settings.Save();
                Log.Information("Display monitor set to {Side} ({Count} monitors)", side, monitors.Count);
            }
        }

        return PickBySide(monitors, side ?? "Right");
    }

    /// <summary>Returns the leftmost or rightmost monitor on the desktop.</summary>
    public static MonitorBounds PickBySide(IReadOnlyList<MonitorBounds> monitors, string side)
    {
        // Ordered by the Right *coordinate*: smallest = leftmost on screen, largest = rightmost.
        return side == "Left"
            ? monitors.OrderBy(m => m.Right).First()
            : monitors.OrderByDescending(m => m.Right).First();
    }
}
