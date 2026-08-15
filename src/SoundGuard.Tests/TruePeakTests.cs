using SoundGuard.Core.Dsp;
using Xunit;

namespace SoundGuard.Tests;

public class TruePeakTests
{
    [Fact]
    public void ConstantSignal_TruePeakEqualsValue()
    {
        var samples = new float[480];
        Array.Fill(samples, 0.9f);
        double peak = TruePeak.Measure(samples, 480, 1);
        Assert.Equal(0.9, peak, 3);
    }

    [Fact]
    public void Sine_TruePeakEqualsAmplitude()
    {
        // A band-limited sine well below Nyquist is reconstructed exactly by the sinc interpolator.
        float[] sine = TestSupport.Sine(997.0, 1.0, 0.1); // 4800 frames
        double peak = TruePeak.Measure(sine, sine.Length, 1);
        Assert.InRange(peak, 0.995, 1.005);
    }

    [Fact]
    public void InterSamplePeak_IsDetectedAboveSamplePeak()
    {
        // 7.5 kHz @ 48 kHz = 6.4 samples/cycle, so no sample lands exactly on the crest: the sample
        // peak reads ~0.88 while the reconstructed (true) peak is ~1.0. This is the classic
        // inter-sample overshoot case the 8x oversampler must catch.
        float[] sine = TestSupport.Sine(7500.0, 1.0, 0.05);

        double samplePeak = 0.0;
        foreach (float s in sine)
            samplePeak = Math.Max(samplePeak, Math.Abs(s));

        double truePeak = TruePeak.Measure(sine, sine.Length, 1);

        Assert.True(truePeak > samplePeak + 0.05,
            $"truePeak {truePeak:F4} should exceed samplePeak {samplePeak:F4}");
        Assert.InRange(truePeak, 0.95, 1.05);
    }
}
