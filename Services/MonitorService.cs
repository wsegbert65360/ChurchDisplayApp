using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Serilog;

namespace ChurchDisplayApp.Services;

public class MonitorService
{
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    private const int MDT_EFFECTIVE_DPI = 0;

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
            var (scaleX, scaleY) = GetMonitorDipScale(window, monitor);
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.WindowState = WindowState.Normal;
            window.Left = monitor.Left / scaleX;
            window.Top = monitor.Top / scaleY;
            window.Width = monitor.Width / scaleX;
            window.Height = monitor.Height / scaleY;
            window.WindowState = WindowState.Maximized;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error positioning window on monitor");
        }
    }

    private static (double ScaleX, double ScaleY) GetMonitorDipScale(Window window, MonitorBounds monitor)
    {
        if (monitor.Handle != IntPtr.Zero &&
            GetDpiForMonitor(monitor.Handle, MDT_EFFECTIVE_DPI, out var dpiX, out var dpiY) == 0 &&
            dpiX > 0 && dpiY > 0)
        {
            return (dpiX / 96.0, dpiY / 96.0);
        }

        var dpi = VisualTreeHelper.GetDpi(window);
        var scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1;
        var scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1;
        return (scaleX, scaleY);
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
                // OrderByDescending(m => m.Right) → monitor with largest Right *coordinate* (rightmost on screen)
                ? monitors.OrderByDescending(m => m.Right).First()
                // OrderBy(m => m.Right) → monitor with smallest Right *coordinate* (leftmost on screen)
                : monitors.OrderBy(m => m.Right).First();
        }

        return monitors.OrderByDescending(m => m.Right).First();
    }
}
