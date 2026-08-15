namespace SoundGuard.Core.Dsp;

/// <summary>
/// 8x-oversampled true-peak measurement (ITU-R BS.1770-4 Annex 2 style).
///
/// The sample peak only sees values at discrete sample instants; a signal can exceed 0 dBFS
/// *between* samples. We reconstruct 7 additional sub-sample points per sample with a
/// windowed-sinc (Hann) interpolation kernel and take the maximum, which catches inter-sample
/// overs. The kernel is exact at integer positions (frac = 0 returns the sample itself).
/// </summary>
public static class TruePeak
{
    public const int Oversample = 8;

    /// <summary>Half-width of the interpolation kernel in samples (each side).</summary>
    private const int Half = 8;

    /// <summary>Maximum |value| over all channels and all 8x sub-sample positions.</summary>
    public static double Measure(ReadOnlySpan<float> samples, int frames, int channels)
    {
        double max = 0.0;
        for (int f = 0; f < frames; f++)
        {
            for (int m = 0; m < Oversample; m++)
            {
                double frac = m / (double)Oversample;
                for (int c = 0; c < channels; c++)
                {
                    double v = Math.Abs(Interpolate(samples, frames, channels, f, c, frac));
                    if (v > max) max = v;
                }
            }
        }
        return max;
    }

    /// <summary>
    /// Windowed-sinc reconstruction at sample index <paramref name="frame"/> plus fractional
    /// offset <paramref name="frac"/> in [0,1). Edge samples are clamped (the error is confined
    /// to the first/last few samples of each block and is negligible for the kernel width used).
    /// </summary>
    private static double Interpolate(ReadOnlySpan<float> samples, int frames, int channels, int frame, int channel, double frac)
    {
        double sum = 0.0;
        for (int k = -Half + 1; k <= Half; k++)
        {
            int idx = frame + k;
            if (idx < 0 || idx >= frames) continue;

            double t = frac - k; // distance in samples from the kernel centre
            double sinc = Math.Abs(t) < 1e-12 ? 1.0 : Math.Sin(Math.PI * t) / (Math.PI * t);
            double window = 0.5 * (1.0 + Math.Cos(Math.PI * t / Half)); // Hann, zero at |t| = Half

            sum += samples[idx * channels + channel] * sinc * window;
        }
        return sum;
    }
}
