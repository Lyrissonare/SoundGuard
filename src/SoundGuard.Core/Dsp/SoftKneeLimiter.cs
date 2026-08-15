namespace SoundGuard.Core.Dsp;

/// <summary>Static soft-knee transfer function (pure math, gain-only — no waveshaping nonlinearity).</summary>
public static class SoftKnee
{
    /// <summary>
    /// Soft-knee gain (dB) for a level relative to a threshold.
    ///
    /// <para>
    /// x = level - threshold:
    ///   x ≤ -knee/2  → gain = 0        (no reduction)
    ///   x ≥ +knee/2  → gain = -x       (full limiting)
    ///   otherwise    → -(x + knee/2)² / (2·knee)   (quadratic, C¹-continuous blend)
    /// </para>
    /// <para>
    /// The result is a time-varying linear gain applied as <c>sample *= gain</c> — it is a soft-<em>knee</em>
    /// limiter, not a harmonic-generating clipper. No aliasing, no distortion products.
    /// </para>
    /// </summary>
    public static double GainDb(double levelDb, double thresholdDb, double kneeDb)
    {
        double x = levelDb - thresholdDb;
        if (x <= -kneeDb / 2.0) return 0.0;
        if (x >= kneeDb / 2.0) return -x;
        double num = x + kneeDb / 2.0;
        return -(num * num) / (2.0 * kneeDb);
    }
}

/// <summary>Tunable parameters for the stage-A peak limiter.</summary>
public sealed class LimiterParams
{
    public double ThresholdDb { get; init; } = -1.0;
    public double KneeDb { get; init; } = 3.0;
    public double AttackMs { get; init; } = 0.8;
    public double ReleaseMs { get; init; } = 200.0;
    public double LookaheadMs { get; init; } = 1.5;
}

/// <summary>
/// Stage A — soft-knee lookahead peak limiter.
///
/// The detector measures the 8x-oversampled true peak of each ~5 ms block and converts it to a
/// target gain with the soft-knee curve. That gain is smoothed per sample with a fast attack
/// (&lt;1 ms) and slower release. A short lookahead delay lines the gain up so reduction begins
/// <em>before</em> the peak reaches the output. The only processing applied is a linear gain
/// multiplication (<c>sample *= gain</c>).
/// </summary>
public sealed class SoftKneeLimiter
{
    private readonly int _channels;
    private readonly int _delayFrames;
    private readonly float[] _delay;          // ring of interleaved frames (the lookahead)
    private readonly int _delayCapacityFrames;
    private readonly double _kneeDb;
    private long _writeFrame;
    private readonly GainSmoother _smoother;

    /// <summary>dB of reduction currently applied (≥ 0).</summary>
    public double GainReductionDb { get; private set; }

    /// <summary>Most recently measured 8x true peak (linear).</summary>
    public double MeasuredTruePeakLinear { get; private set; }

    /// <summary>Most recently measured 8x true peak (dBFS).</summary>
    public double MeasuredTruePeakDb => Db.FromLinear(MeasuredTruePeakLinear);

    /// <summary>Live threshold (dBFS). Updating this takes effect on the next block.</summary>
    public double ThresholdDb { get; set; }

    public SoftKneeLimiter(int channels, double sampleRate, LimiterParams p)
    {
        _channels = channels;
        ThresholdDb = p.ThresholdDb;
        _kneeDb = p.KneeDb;
        _delayFrames = Math.Max(1, (int)Math.Round(p.LookaheadMs / 1000.0 * sampleRate));

        // Reserve the lookahead plus a generous block window (250 ms worth) so no valid read position
        // is ever overwritten while a block is being processed.
        int maxBlockFrames = (int)(sampleRate * 0.25) + 256;
        _delayCapacityFrames = _delayFrames + maxBlockFrames;
        _delay = new float[_delayCapacityFrames * channels];

        _smoother = new GainSmoother(sampleRate, p.AttackMs, p.ReleaseMs);
    }

    /// <summary>
    /// Process one block <em>in place</em> and return the gain reduction (dB) applied.
    /// The block size should match the detection window (~5 ms).
    /// </summary>
    public double Process(Span<float> samples, int frames)
    {
        // Detector: 8x true peak over the whole block, then soft-knee target gain in dB.
        MeasuredTruePeakLinear = TruePeak.Measure(samples, frames, _channels);
        double levelDb = Db.FromLinear(MeasuredTruePeakLinear);
        double targetDb = SoftKnee.GainDb(levelDb, ThresholdDb, _kneeDb);

        for (int f = 0; f < frames; f++)
        {
            double gain = Db.ToLinear(_smoother.Step(targetDb));
            int src = f * _channels;
            int write = (int)(_writeFrame % _delayCapacityFrames);

            // Push the current frame into the lookahead ring.
            for (int c = 0; c < _channels; c++)
                _delay[write * _channels + c] = samples[src + c];

            // Read the frame delayed by the lookahead and apply the (already-smoothed) gain.
            long delayedFrame = _writeFrame - _delayFrames;
            if (delayedFrame >= 0)
            {
                int read = (int)(delayedFrame % _delayCapacityFrames);
                for (int c = 0; c < _channels; c++)
                    samples[src + c] = (float)(_delay[read * _channels + c] * gain);
            }
            else
            {
                // Lookahead not yet filled: output silence for the first few frames.
                for (int c = 0; c < _channels; c++)
                    samples[src + c] = 0f;
            }

            _writeFrame++;
        }

        GainReductionDb = Math.Max(0.0, -_smoother.CurrentDb);
        return GainReductionDb;
    }

    public void Reset()
    {
        Array.Clear(_delay);
        _writeFrame = 0;
        _smoother.Reset();
        GainReductionDb = 0.0;
        MeasuredTruePeakLinear = 0.0;
    }

}
