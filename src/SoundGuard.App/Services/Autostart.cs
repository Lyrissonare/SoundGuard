using Microsoft.Win32;

namespace SoundGuard.App.Services;

/// <summary>Adds/removes the "run at logon" registry entry.</summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SoundGuard";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) != null;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, Environment.ProcessPath ?? "SoundGuard.exe");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
