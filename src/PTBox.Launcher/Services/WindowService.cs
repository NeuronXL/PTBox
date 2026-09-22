using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PTBox.Launcher.Services;

public static class WindowService
{
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(PointNative point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr window, int id);
    private delegate bool EnumWindow(IntPtr window, IntPtr parameter);
    [StructLayout(LayoutKind.Sequential)] private struct PointNative { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor; public Rect Work; public uint Flags; }

    public static bool BringProcessForward(Process process)
    {
        process.Refresh();
        var handle = process.MainWindowHandle;
        if (handle == IntPtr.Zero)
            EnumWindows((window, _) =>
            {
                GetWindowThreadProcessId(window, out var pid);
                if (pid == process.Id && IsWindowVisible(window)) { handle = window; return false; }
                return true;
            }, IntPtr.Zero);
        if (handle == IntPtr.Zero) return false;
        if (IsIconic(handle)) ShowWindow(handle, 9);
        SetForegroundWindow(handle);
        return true;
    }
    public static void Restore(Window window, bool fullscreen)
    {
        window.Show();
        window.WindowState = fullscreen ? WindowState.Maximized : WindowState.Normal;
        window.Activate();
        SetForegroundWindow(new WindowInteropHelper(window).Handle);
    }
    public static void FullscreenPrimary(Window window)
    {
        // Physical monitor coordinates avoid work-area/taskbar and mixed-DPI rounding issues.
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (GetMonitorInfo(MonitorFromPoint(new PointNative(), 1), ref info))
            SetWindowPos(new WindowInteropHelper(window).Handle, IntPtr.Zero, info.Monitor.Left, info.Monitor.Top,
                info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top, 0x0004 | 0x0010);
    }
}
