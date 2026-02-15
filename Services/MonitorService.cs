using System.Runtime.InteropServices;
using System.Windows;

namespace ChurchDisplayApp.Services;

/// <summary>
/// Provides monitor detection and window positioning services for multi-monitor setups.
/// </summary>
public class MonitorService
{
    // P/Invoke declarations for monitor detection
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfoEx
    {
        public uint cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    private static List<MonitorBounds> _monitorBounds = new();

    /// <summary>
    /// Represents the bounds and properties of a monitor.
    /// </summary>
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

    /// <summary>
    /// Gets a list of all available monitors.
    /// </summary>
    public List<MonitorBounds> GetMonitors()
    {
        _monitorBounds.Clear();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnumCallback, IntPtr.Zero);
        return new List<MonitorBounds>(_monitorBounds);
    }

    private static bool MonitorEnumCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData)
    {
        MonitorInfoEx mi = new() { cbSize = (uint)Marshal.SizeOf(typeof(MonitorInfoEx)) };
        GetMonitorInfo(hMonitor, ref mi);

        const uint MONITORINFOF_PRIMARY = 1;
        bool isPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;

        _monitorBounds.Add(new MonitorBounds
        {
            Left = mi.rcMonitor.left,
            Top = mi.rcMonitor.top,
            Right = mi.rcMonitor.right,
            Bottom = mi.rcMonitor.bottom,
            IsPrimary = isPrimary
        });

        return true;
    }

    /// <summary>
    /// Positions a window on the specified monitor and maximizes it.
    /// </summary>
    public void PositionWindowOnMonitor(Window window, MonitorBounds monitor)
    {
        if (window == null || monitor == null)
            return;

        try
        {
            // Position the window on the monitor
            window.Left = monitor.Left;
            window.Top = monitor.Top;
            window.Width = monitor.Width;
            window.Height = monitor.Height;
            window.WindowState = WindowState.Maximized;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error positioning window: {ex.Message}");
        }
    }

    /// <summary>
    /// Prompts the user to select which monitor to use for display output.
    /// Returns the rightmost monitor if user selects "Yes", otherwise the leftmost.
    /// </summary>
    public MonitorBounds? SelectDisplayMonitor(List<MonitorBounds> monitors)
    {
        if (monitors == null || monitors.Count == 0)
            return null;

        if (monitors.Count == 1)
            return monitors[0];

        var result = MessageBox.Show(
            $"Multiple monitors detected ({monitors.Count} total).\n\n" +
            "Would you like to use the rightmost monitor (likely your projector/display) for the live output?\n\n" +
            "Click YES for rightmost, NO for leftmost.",
            "Select Display",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        return result == MessageBoxResult.Yes
            ? monitors.OrderByDescending(m => m.Right).First()  // Rightmost
            : monitors.OrderBy(m => m.Right).First();           // Leftmost
    }
}
