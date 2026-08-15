namespace SoundGuard.Core.Dsp;

/// <summary>
/// Sample-peak meter (dBFS) over a block, plus a slow peak-hold falloff for display.
/// This is the fast "true sample peak"; the 8x-oversampled (inter-sample) peak lives in
/// <see cref="TruePeak"/> and is used by the limiter.
/// </summary>
public sealed class PeakMeter
{
    private readonly double _holdDecayPerSample;
    private double _holdPeak;

    public double PeakDbFs { get; private set; } = Db.SilenceDb;
    public double HoldDbFs { get; private set; } = Db.SilenceDb;

    public PeakMeter(double sampleRate, double holdSeconds = 2.0)
    {
        _holdDecayPerSample = Math.Exp(-1.0 / (holdSeconds * sampleRate));
    }

    public void Process(ReadOnlySpan<float> samples)
    {
        double peak = 0.0;
        for (int i = 0; i < samples.Length; i++)
        {
            double a = Math.Abs(samples[i]);
            if (a > peak) peak = a;
        }

        PeakDbFs = Db.FromLinear(peak);

        // Peak hold decays exponentially toward the current peak over the block.
        _holdPeak = Math.Max(peak, _holdPeak * Math.Pow(_holdDecayPerSample, samples.Length));
        HoldDbFs = Db.FromLinear(_holdPeak);
    }

    public void Reset()
    {
        _holdPeak = 0;
        PeakDbFs = HoldDbFs = Db.SilenceDb;
    }
}
