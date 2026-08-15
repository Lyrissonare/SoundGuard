namespace SoundGuard.Core.Dsp;

/// <summary>
/// One-pole smoother for a gain value expressed in dB (≤ 0). Used by both limiter stages.
///
/// The coefficient is the standard one-pole form <c>exp(-1 / (tau * fs))</c>; when called once per
/// sample it yields a true first-order attack/release envelope. Attack uses the smaller of the two
/// time constants depending on whether the target calls for more or less reduction.
/// </summary>
public sealed class GainSmoother
{
    private readonly double _attackCoef;
    private readonly double _releaseCoef;
    private double _gainDb;

    public GainSmoother(double sampleRate, double attackMs, double releaseMs)
    {
        _attackCoef = Math.Exp(-1.0 / (attackMs / 1000.0 * sampleRate));
        _releaseCoef = Math.Exp(-1.0 / (releaseMs / 1000.0 * sampleRate));
    }

    /// <summary>Current smoothed gain in dB (≤ 0).</summary>
    public double CurrentDb => _gainDb;

    /// <summary>Advance one sample toward <paramref name="targetDb"/> (≤ 0) and return the new gain.</summary>
    public double Step(double targetDb)
    {
        double coef = targetDb < _gainDb ? _attackCoef : _releaseCoef;
        _gainDb = targetDb + coef * (_gainDb - targetDb);
        return _gainDb;
    }

    public void Reset() => _gainDb = 0.0;
}
