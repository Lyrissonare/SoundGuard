using SoundGuard.Core.Dsp;
using Xunit;

namespace SoundGuard.Tests;

public class LoudnessMeterTests
{
    private const double Freq = 997.0; // BS.1770 reference frequency

    private static double MeasureShortTerm(double amplitude)
    {
        var meter = new LoudnessMeter(1, TestSupport.SampleRate);
        float[] signal = TestSupport.Sine(Freq, amplitude, seconds: 4.0);
        TestSupport.Feed(meter, signal);
        return meter.ShortTermLufs;
    }

    [Fact]
    public void DoublingAmplitude_RaisesLoudnessByAboutSixLu()
    {
        // Loudness is 10·log10(power); doubling amplitude quadruples power → +6.02 LU.
        // This is a scale test independent of the absolute K-weighting gain at 997 Hz.
        double lufsLow = MeasureShortTerm(0.1);
        double lufsHigh = MeasureShortTerm(0.2);

        Assert.False(double.IsNegativeInfinity(lufsLow));
        Assert.False(double.IsNegativeInfinity(lufsHigh));
        Assert.InRange(lufsHigh - lufsLow, 5.5, 6.5);
    }

    [Fact]
    public void SteadyTone_MomentaryTracksShortTerm()
    {
        var meter = new LoudnessMeter(1, TestSupport.SampleRate);
        TestSupport.Feed(meter, TestSupport.Sine(Freq, 0.2, 4.0));

        Assert.False(double.IsNegativeInfinity(meter.ShortTermLufs));
        Assert.False(double.IsNegativeInfinity(meter.MomentaryLufs));
        Assert.InRange(Math.Abs(meter.ShortTermLufs - meter.MomentaryLufs), 0.0, 0.5);
    }

    [Fact]
    public void ReferenceTone_ReadsRoughlyMinusTwentyThreeLufs()
    {
        // The 997 Hz tone at -23 dBFS RMS (amplitude ≈ 0.1001) is the EBU R128 calibration tone.
        // K-weighting at 997 Hz is a small positive boost, so expect ≈ -22 LUFS; allow a band.
        double lufs = MeasureShortTerm(0.1001);
        Assert.InRange(lufs, -26.0, -19.0);
    }

    [Fact]
    public void Silence_ReadsNegativeInfinity()
    {
        var meter = new LoudnessMeter(1, TestSupport.SampleRate);
        var silence = new float[TestSupport.SampleRate * 4];
        TestSupport.Feed(meter, silence);

        Assert.True(double.IsNegativeInfinity(meter.ShortTermLufs));
        Assert.True(double.IsNegativeInfinity(meter.MomentaryLufs));
    }
}
