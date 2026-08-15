using SoundGuard.Core.Engine;

namespace SoundGuard.Core.Config;

/// <summary>Application-level settings persisted to JSON. Wraps the engine's <see cref="ProtectionConfig"/>.</summary>
public sealed class AppConfig
{
    public ProtectionConfig Protection { get; set; } = new();

    /// <summary>Start minimized to tray. Off by default: the main window shows in the foreground.</summary>
    public bool MinimizeToTray { get; set; } = false;

    public bool StartWithWindows { get; set; }

    /// <summary>Process names (case-insensitive) that bypass protection entirely.</summary>
    public List<string> Whitelist { get; set; } = new();

    public bool IsWhitelisted(string? processName) =>
        processName != null && Whitelist.Any(w => string.Equals(w, processName, StringComparison.OrdinalIgnoreCase));
}
