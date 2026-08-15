using SoundGuard.Core.Engine;

namespace SoundGuard.Core.Logging;

/// <summary>A single protection trigger event, written to the log.</summary>
public readonly record struct ProtectionEvent(
    DateTime TimestampUtc,
    string? Process,
    double ShortTermLufs,
    double TruePeakDb,
    ProtectionState State,
    string Action);

/// <summary>
/// Appends tab-separated protection events to <c>%APPDATA%\SoundGuard\events.log</c>.
/// Thread-safe; failures are swallowed so logging can never interrupt audio processing.
/// </summary>
public sealed class ProtectionLogger
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public ProtectionLogger(string? filePath = null)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SoundGuard");
        _filePath = filePath ?? Path.Combine(dir, "events.log");
    }

    public void Log(ProtectionEvent e)
    {
        string line = string.Join('\t',
            e.TimestampUtc.ToString("O"),
            e.Process ?? "-",
            double.IsFinite(e.ShortTermLufs) ? e.ShortTermLufs.ToString("F1") : "-",
            double.IsFinite(e.TruePeakDb) ? e.TruePeakDb.ToString("F1") : "-",
            e.State.ToString(),
            e.Action);

        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                File.AppendAllText(_filePath, line + Environment.NewLine);
            }
            catch
            {
                // Logging must never throw.
            }
        }
    }
}
