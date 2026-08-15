namespace SoundGuard.Core.Dsp;

/// <summary>
/// ITU-R BS.1770-4 loudness meter producing Momentary (400 ms) and Short-term (3 s) LUFS.
///
/// Pipeline: K-weight each channel → accumulate mean-squares into 100 ms blocks → sum channels
/// (all channel weights 1.0) → gate with the absolute gate (-70 LUFS) and, for short-term, the
/// relative gate (-10 LU below the absolute-gated measurement) → LUFS.
/// </summary>
public sealed class LoudnessMeter
{
    private const double BlockDurationMs = 100.0;
    private const int MomentaryBlocks = 4;   // 400 ms
    private const int ShortTermBlocks = 30;  // 3 s

    private readonly KWeightingFilter _kFilter;
    private readonly int _channels;
    private readonly int _blockSamples;
    private readonly double[] _blockSumSquares;
    private int _blockCount;

    private readonly List<double> _momentary = new();
    private readonly List<double> _shortTerm = new();
    private double _momentarySum;
    private double _shortTermSum;

    public double MomentaryLufs { get; private set; } = double.NegativeInfinity;
    public double ShortTermLufs { get; private set; } = double.NegativeInfinity;

    public LoudnessMeter(int channels, double sampleRate)
    {
        _channels = channels;
        _kFilter = new KWeightingFilter(channels, sampleRate);
        _blockSamples = Math.Max(1, (int)Math.Round(sampleRate * BlockDurationMs / 1000.0));
        _blockSumSquares = new double[channels];
    }

    public void Process(ReadOnlySpan<float> samples, int frames)
    {
        for (int f = 0; f < frames; f++)
        {
            int offset = f * _channels;
            for (int c = 0; c < _channels; c++)
            {
                double k = _kFilter.Process(c, samples[offset + c]);
                _blockSumSquares[c] += k * k;
            }

            if (++_blockCount >= _blockSamples)
                FlushBlock();
        }
    }

    public void Reset()
    {
        _kFilter.Reset();
        Array.Clear(_blockSumSquares);
        _blockCount = 0;
        _momentary.Clear();
        _shortTerm.Clear();
        _momentarySum = _shortTermSum = 0.0;
        MomentaryLufs = ShortTermLufs = double.NegativeInfinity;
    }

    private void FlushBlock()
    {
        // Mean square per channel, summed across channels (BS.1770 channel weight = 1.0 for all).
        double energy = 0.0;
        for (int c = 0; c < _channels; c++)
            energy += _blockSumSquares[c] / _blockSamples;
        Array.Clear(_blockSumSquares);
        _blockCount = 0;

        Push(_momentary, energy, ref _momentarySum, MomentaryBlocks);
        Push(_shortTerm, energy, ref _shortTermSum, ShortTermBlocks);

        MomentaryLufs = ComputeGated(_momentary, relativeGate: false);
        ShortTermLufs = ComputeGated(_shortTerm, relativeGate: true);
    }

    private static void Push(List<double> window, double energy, ref double sum, int maxBlocks)
    {
        window.Add(energy);
        sum += energy;
        while (window.Count > maxBlocks)
        {
            sum -= window[0];
            window.RemoveAt(0);
        }
    }

    private static double ComputeGated(List<double> window, bool relativeGate)
    {
        if (window.Count == 0) return double.NegativeInfinity;

        double absGate = Db.LoudnessToPower(-70.0);
        double relGate = double.NegativeInfinity;

        if (relativeGate)
        {
            // Relative gate: -10 LU below the absolute-gated (ungated) measurement.
            double ungatedSum = 0.0;
            int n = 0;
            foreach (double e in window)
            {
                if (e >= absGate) { ungatedSum += e; n++; }
            }
            if (n > 0)
                relGate = Db.LoudnessToPower(Db.PowerToLoudness(ungatedSum / n) - 10.0);
        }

        double gatedSum = 0.0;
        int count = 0;
        foreach (double e in window)
        {
            if (e >= absGate && e >= relGate) { gatedSum += e; count++; }
        }

        return count == 0 ? double.NegativeInfinity : Db.PowerToLoudness(gatedSum / count);
    }
}
