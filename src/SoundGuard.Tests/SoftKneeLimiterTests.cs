using SoundGuard.Core.Dsp;
using Xunit;

namespace SoundGuard.Tests;

public class SoftKneeLimiterTests
{
    [Fact]
    public void SoftKnee_BelowKnee_ReturnsZeroGain()
    {
        Assert.Equal(0.0, SoftKnee.GainDb(levelDb: -11.0, thresholdDb: -1.0, kneeDb: 3.0), 3);
    }

    [Fact]
    public void SoftKnee_AboveKnee_ReturnsFullReduction()
    {
        Assert.Equal(-10.0, SoftKnee.GainDb(levelDb: 9.0, thresholdDb: -1.0, kneeDb: 3.0), 3);
    }

    [Fact]
    public void SoftKnee_AtThreshold_ReturnsKneeOverEight()
    {
        // At x=0: -(knee/2)²/(2·knee) = -knee/8 = -0.375 dB for knee=3.
        Assert.Equal(-0.375, SoftKnee.GainDb(levelDb: -1.0, thresholdDb: -1.0, kneeDb: 3.0), 3);
    }

    [Fact]
    public void BelowThreshold_PassesThroughUnchanged()
    {
        var limiter = new SoftKneeLimiter(1, TestSupport.SampleRate, new LimiterParams { ThresholdDb = -1.0 });
        double outputPeak = Feed(limiter, amplitude: 0.01, out double gr);

        Assert.True(gr < 0.05, $"GR should be ~0 for a -40 dBFS signal, got {gr:F3}");
        Assert.InRange(outputPeak, 0.009, 0.011);
    }

    [Fact]
    public void AboveThreshold_AppliesGainReduction()
    {
        var limiter = new SoftKneeLimiter(1, TestSupport.SampleRate, new LimiterParams { ThresholdDb = -1.0 });
        double outputPeak = Feed(limiter, amplitude: 1.0, out double gr);

        Assert.True(gr > 0.5, $"GR should exceed 0.5 dB for a 0 dBFS signal, got {gr:F3}");
        Assert.True(outputPeak < 0.92, $"Output peak {outputPeak:F3} should be reduced below ~0.92");
        Assert.True(outputPeak > 0.5, $"Output peak {outputPeak:F3} should not be silence");
    }

    /// <summary>Feed 2 s of a 997 Hz sine in ~5 ms blocks; return the peak of the final block's output.</summary>
    private static double Feed(SoftKneeLimiter limiter, double amplitude, out double gainReductionDb)
    {
        const int blockFrames = 240; // ~5 ms @ 48 kHz
        int totalFrames = TestSupport.SampleRate * 2;

        var block = new float[blockFrames];
        double gr = 0.0;
        double finalPeak = 0.0;

        for (int start = 0; start < totalFrames; start += blockFrames)
        {
            for (int i = 0; i < blockFrames; i++)
                block[i] = (float)(amplitude * Math.Sin(2.0 * Math.PI * 997.0 * (start + i) / TestSupport.SampleRate));

            gr = limiter.Process(block, blockFrames);

            if (start + blockFrames >= totalFrames)
                foreach (float s in block) finalPeak = Math.Max(finalPeak, Math.Abs(s));
        }

        gainReductionDb = gr;
        return finalPeak;
    }
}
