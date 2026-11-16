using System.Runtime.InteropServices;
using System.Windows;
using Serilog;

namespace ChurchDisplayApp.Services;

public class MonitorService
{
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

    // CharSet must match MonitorInfoEx (Auto => Unicode). Without it the ANSI entry point is
    // called with a Unicode-sized struct, every call fails, and zero monitors are found.
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
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
                IsPrimary = isPrimary
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    /// <summary>
    /// Moves the window onto the given monitor and maximizes it there.
    /// Monitor bounds are in Win32 pixel coordinates (as returned by EnumDisplayMonitors),
    /// so the window is placed with SetWindowPos in that same coordinate space. Assigning
    /// them to WPF's Left/Top (device-independent units) lands the window in the wrong place
    /// whenever display scaling is not 100%.
    /// </summary>
    public void PositionWindowOnMonitor(Window window, MonitorBounds monitor)
    {
        if (window == null || monitor == null)
            return;

        try
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;

            // A maximized window can't be moved; restore first, move, then re-maximize
            // so Windows maximizes it on the monitor it now sits on.
            if (window.WindowState == WindowState.Maximized)
                window.WindowState = WindowState.Normal;

            var hwnd = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
            if (!SetWindowPos(hwnd, IntPtr.Zero, monitor.Left, monitor.Top, monitor.Width, monitor.Height,
                    SWP_NOZORDER | SWP_NOACTIVATE))
            {
                Log.Warning("SetWindowPos failed (error {Error}) moving window to {Monitor}",
                    Marshal.GetLastWin32Error(), monitor);
            }

            window.WindowState = WindowState.Maximized;
            Log.Information("Positioned {Title} on monitor {Monitor}", window.Title, monitor);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error positioning window on monitor");
        }
    }

    public MonitorBounds? SelectDisplayMonitor(List<MonitorBounds> monitors)
    {
        if (monitors == null || monitors.Count == 0)
            return null;

        if (monitors.Count == 1)
            return monitors[0];

        var selectionWindow = new MonitorSelectionWindow();
        if (selectionWindow.ShowDialog() == true)
        {
            return selectionWindow.Result == MonitorSelectionWindow.SelectionResult.Right
                ? monitors.OrderByDescending(m => m.Right).First()
                : monitors.OrderBy(m => m.Right).First();
        }

        return monitors.OrderByDescending(m => m.Right).First();
    }
}
