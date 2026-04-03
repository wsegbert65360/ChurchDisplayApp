using System.Runtime.InteropServices;
using System.Windows;
using Serilog;

namespace ChurchDisplayApp.Services;

public class MonitorService
{
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
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

        bool EnumCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData)
        {
            MonitorInfoEx mi = new() { cbSize = (uint)Marshal.SizeOf(typeof(MonitorInfoEx)) };
            GetMonitorInfo(hMonitor, ref mi);

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

        return results;
    }

    public void PositionWindowOnMonitor(Window window, MonitorBounds monitor)
    {
        if (window == null || monitor == null)
            return;

        try
        {
            window.Left = monitor.Left;
            window.Top = monitor.Top;
            window.Width = monitor.Width;
            window.Height = monitor.Height;
            window.WindowState = WindowState.Maximized;
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
