namespace SoundGuard.Core.Dsp;

/// <summary>
/// Stage B — loudness limiter. Converts short-term LUFS into a slow, smooth gain.
///
/// targetGain(dB) = clamp(threshold − shortTermLufs, −maxReduction, 0), i.e. it removes exactly the
/// amount by which the 3-second loudness exceeds the threshold. Attack 50–200 ms, release 1–3 s.
/// No per-sample audio is processed here; the gain drives the master-volume protection action.
/// </summary>
public sealed class LoudnessLimiter
{
    private readonly double _maxReductionDb;
    private readonly double _attackPerBlock;
    private readonly double _releasePerBlock;
    private double _gainDb;

    /// <summary>dB of reduction currently applied (≥ 0).</summary>
    public double GainReductionDb { get; private set; }

    /// <summary>Live threshold (LUFS).</summary>
    public double ThresholdLufs { get; set; }

    /// <param name="blockSeconds">Duration of one analysis block, for correct time constants.</param>
    public LoudnessLimiter(double thresholdLufs, double attackMs, double releaseMs, double blockSeconds, double maxReductionDb = 80.0)
    {
        ThresholdLufs = thresholdLufs;
        _maxReductionDb = maxReductionDb;
        _attackPerBlock = Math.Exp(-blockSeconds / (attackMs / 1000.0));
        _releasePerBlock = Math.Exp(-blockSeconds / (releaseMs / 1000.0));
    }

    /// <summary>Advance one block; returns the smoothed gain reduction (dB) for this block.</summary>
    public double Step(double shortTermLufs)
    {
        double targetDb = 0.0;
        if (!double.IsNaN(shortTermLufs) && !double.IsNegativeInfinity(shortTermLufs))
        {
            double over = shortTermLufs - ThresholdLufs;
            if (over > 0.0)
                targetDb = Math.Max(-_maxReductionDb, -over);
        }

        double coef = targetDb < _gainDb ? _attackPerBlock : _releasePerBlock;
        _gainDb = targetDb + coef * (_gainDb - targetDb);

        GainReductionDb = Math.Max(0.0, -_gainDb);
        return GainReductionDb;
    }

    public void Reset()
    {
        _gainDb = 0.0;
        GainReductionDb = 0.0;
    }
}
