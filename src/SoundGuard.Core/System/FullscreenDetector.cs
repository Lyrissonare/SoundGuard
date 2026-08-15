using System.Runtime.InteropServices;

namespace SoundGuard.Core.System;

/// <summary>
/// Detects whether the foreground window covers its monitor (a heuristic for "game is fullscreen").
/// Used to auto-enable game mode, which tightens the loudness threshold.
/// </summary>
public static class FullscreenDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);

    private const uint MonitorDefaultToNearest = 0x00000002;

    public static bool IsForegroundFullscreen()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;

        if (!GetWindowRect(hwnd, out RECT window)) return false;

        IntPtr monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info)) return false;

        return window.Left <= info.rcMonitor.Left &&
               window.Top <= info.rcMonitor.Top &&
               window.Right >= info.rcMonitor.Right &&
               window.Bottom >= info.rcMonitor.Bottom;
    }
}
