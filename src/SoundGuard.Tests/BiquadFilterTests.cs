using SoundGuard.Core.Dsp;
using Xunit;

namespace SoundGuard.Tests;

public class BiquadFilterTests
{
    [Fact]
    public void HighPass_RejectsDc()
    {
        var hp = new BiquadFilter();
        hp.ConfigureHighPass(TestSupport.SampleRate, KWeighting.HighPassCenterHz, KWeighting.HighPassQ);

        double output = 0.0;
        for (int i = 0; i < TestSupport.SampleRate; i++) // 1 s of DC = 1.0
            output = hp.Process(1.0);

        Assert.True(Math.Abs(output) < 1e-3, $"High-pass DC gain should be ~0, got {output}");
    }

    [Fact]
    public void HighShelf_BoostsHighFrequenciesByAboutFourDb()
    {
        var shelf = new BiquadFilter();
        shelf.ConfigureHighShelf(TestSupport.SampleRate, KWeighting.ShelfCenterHz, KWeighting.ShelfGainDb, KWeighting.ShelfQ);

        // Steady-state gain for a 10 kHz sine should approach 10^(4/20) ≈ 1.585.
        double peak = 0.0;
        int warmup = TestSupport.SampleRate / 2;
        for (int i = 0; i < TestSupport.SampleRate; i++)
        {
            double x = Math.Sin(2.0 * Math.PI * 10000.0 * i / TestSupport.SampleRate);
            double y = shelf.Process(x);
            if (i > warmup) peak = Math.Max(peak, Math.Abs(y));
        }

        Assert.InRange(peak, 1.45, 1.70);
    }
}
