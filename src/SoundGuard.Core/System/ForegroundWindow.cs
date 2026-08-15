using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SoundGuard.Core.System;

/// <summary>Resolves the process name of the foreground window, for protection-event logging.</summary>
public static class ForegroundWindow
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    public static string? GetProcessName()
    {
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return null;

            return Process.GetProcessById((int)pid)?.ProcessName;
        }
        catch
        {
            return null;
        }
    }
}
