using SoundGuard.Core.Dsp;
using Xunit;

namespace SoundGuard.Tests;

public class PeakMeterTests
{
    [Fact]
    public void KnownPeak_MapsToCorrectDbFs()
    {
        var meter = new PeakMeter(TestSupport.SampleRate);
        float[] block = { 0.5f, -0.5f, 0.25f, -0.1f };
        meter.Process(block);

        // 20·log10(0.5) = -6.0206 dBFS.
        Assert.Equal(-6.02, meter.PeakDbFs, 2);
    }

    [Fact]
    public void Silence_ClampsToNoiseFloor()
    {
        var meter = new PeakMeter(TestSupport.SampleRate);
        meter.Process(new float[480]);
        Assert.Equal(Db.SilenceDb, meter.PeakDbFs);
    }
}
