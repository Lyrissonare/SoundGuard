using SoundGuard.Core.Dsp;

namespace SoundGuard.Tests;

/// <summary>Shared signal-generation and feeding helpers for DSP tests.</summary>
internal static class TestSupport
{
    public const int SampleRate = 48000;

    /// <summary>Generate <paramref name="seconds"/> of an interleaved sine across all channels.</summary>
    public static float[] Sine(double frequencyHz, double amplitude, double seconds, int channels = 1)
    {
        int frames = (int)(SampleRate * seconds);
        var buffer = new float[frames * channels];
        for (int f = 0; f < frames; f++)
        {
            double v = amplitude * Math.Sin(2.0 * Math.PI * frequencyHz * f / SampleRate);
            for (int c = 0; c < channels; c++)
                buffer[f * channels + c] = (float)v;
        }
        return buffer;
    }

    /// <summary>Feed a full signal into a loudness meter in ~100 ms blocks (mono-friendly).</summary>
    public static void Feed(LoudnessMeter meter, float[] samples, int channels = 1, int blockFrames = 480)
    {
        for (int offset = 0; offset < samples.Length; offset += blockFrames * channels)
        {
            int count = Math.Min(blockFrames * channels, samples.Length - offset);
            meter.Process(samples.AsSpan(offset, count), count / channels);
        }
    }
}
