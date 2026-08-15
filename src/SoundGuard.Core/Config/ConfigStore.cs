using System.Text.Json;

namespace SoundGuard.Core.Config;

/// <summary>
/// Loads/saves <see cref="AppConfig"/> as JSON in <c>%APPDATA%\SoundGuard\config.json</c>.
/// Corrupt or missing files fall back to defaults.
/// </summary>
public sealed class ConfigStore
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SoundGuard");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>True if a saved config file already exists (i.e. this is not the first run).</summary>
    public bool ConfigExists => File.Exists(ConfigPath);

    public AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath)) ?? new AppConfig();
        }
        catch
        {
            // Corrupt config → fall through to defaults.
        }
        return new AppConfig();
    }

    public void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, Options));
        }
        catch
        {
            // Best effort; never crash the app over persistence.
        }
    }
}
