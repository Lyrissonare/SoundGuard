namespace SoundGuard.Core.Dsp;

/// <summary>
/// 8x-oversampled true-peak measurement (ITU-R BS.1770-4 Annex 2 style).
///
/// The sample peak only sees values at discrete sample instants; a signal can exceed 0 dBFS
/// *between* samples. We reconstruct 7 additional sub-sample points per sample with a
/// windowed-sinc (Hann) interpolation kernel and take the maximum, which catches inter-sample
/// overs.
///
/// The kernel is normalized per fractional phase so that a constant signal is reproduced exactly
/// (the sum of the windowed-sinc taps equals 1.0 for every phase). Out-of-range samples at block
/// boundaries are filled with a half-sample symmetric extension, the standard boundary condition
/// for interpolation filters.
/// </summary>
public static class TruePeak
{
    public const int Oversample = 8;

    /// <summary>Half-width of the interpolation kernel in samples.</summary>
    private const int Half = 8;

    /// <summary>Tap offsets k: -Half+1 … +Half (16 taps, symmetric around 0.5).</summary>
    private const int TapCount = 2 * Half;

    /// <summary>
    /// Normalized polyphase kernels. First index = phase (0…7), second = tap (0…15).
    /// Precomputed so the per-sample interpolation is just a dot product.
    /// </summary>
    private static readonly double[][] Kernels = BuildKernels();

    /// <summary>Maximum |value| over all channels and all 8x sub-sample positions.</summary>
    public static double Measure(ReadOnlySpan<float> samples, int frames, int channels)
    {
        double max = 0.0;
        for (int f = 0; f < frames; f++)
        {
            for (int m = 0; m < Oversample; m++)
            {
                double[] kernel = Kernels[m];
                for (int c = 0; c < channels; c++)
                {
                    double v = Math.Abs(Interpolate(samples, frames, channels, f, c, kernel));
                    if (v > max) max = v;
                }
            }
        }
        return max;
    }

    private static double Interpolate(ReadOnlySpan<float> samples, int frames, int channels, int frame, int channel, double[] kernel)
    {
        double sum = 0.0;
        for (int tap = 0; tap < TapCount; tap++)
        {
            int idx = frame + (tap - Half + 1);
            idx = Mirror(idx, frames);

            sum += samples[idx * channels + channel] * kernel[tap];
        }
        return sum;
    }

    /// <summary>
    /// Half-sample symmetric extension: x[-1] = x[0], x[N] = x[N-1], etc. This is the standard
    /// boundary condition for interpolation filters and avoids the false overshoot that
    /// whole-sample mirroring produces for odd-symmetric signals (e.g. a sine near the block edge).
    /// </summary>
    private static int Mirror(int idx, int frames)
    {
        if (frames <= 1) return 0;
        if (idx < 0) return -idx - 1;
        if (idx >= frames) return 2 * frames - 1 - idx;
        return idx;
    }

    private static double[][] BuildKernels()
    {
        var kernels = new double[Oversample][];

        for (int m = 0; m < Oversample; m++)
        {
            double frac = m / (double)Oversample;
            var raw = new double[TapCount];
            double sum = 0.0;

            for (int tap = 0; tap < TapCount; tap++)
            {
                int k = tap - Half + 1;               // -7 … +8
                double t = frac - k;                  // distance in samples from the kernel centre
                double sinc = Math.Abs(t) < 1e-12 ? 1.0 : Math.Sin(Math.PI * t) / (Math.PI * t);
                double window = 0.5 * (1.0 + Math.Cos(Math.PI * t / Half)); // Hann, zero at |t| = Half

                raw[tap] = sinc * window;
                sum += raw[tap];
            }

            // Normalize so a constant (DC) input is reconstructed exactly.
            kernels[m] = new double[TapCount];
            for (int tap = 0; tap < TapCount; tap++)
                kernels[m][tap] = raw[tap] / sum;
        }

        return kernels;
    }
}
