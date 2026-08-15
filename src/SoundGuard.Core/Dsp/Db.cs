namespace SoundGuard.Core.Dsp;

/// <summary>
/// Decibel conversion helpers. Samples stay 32-bit float; dB arithmetic is done in double
/// to keep threshold math precise.
/// </summary>
public static class Db
{
    /// <summary>Floor used for display and to avoid log(0).</summary>
    public const double SilenceDb = -120.0;

    private const double Epsilon = 1e-12;

    /// <summary>Linear amplitude → dBFS. Anything at/below the noise floor clamps to <see cref="SilenceDb"/>.</summary>
    public static double FromLinear(double linear) =>
        linear <= Epsilon ? SilenceDb : Math.Max(SilenceDb, 20.0 * Math.Log10(linear));

    /// <summary>dB → linear amplitude.</summary>
    public static double ToLinear(double db) => Math.Pow(10.0, db / 20.0);

    /// <summary>dB → linear amplitude (float).</summary>
    public static float ToLinearF(float db) => (float)Math.Pow(10.0, db / 20.0);

    /// <summary>
    /// Convert a loudness value (LUFS is a power-domain dB) to mean-square power.
    /// Uses the BS.1770 offset of -0.691 dB.
    /// </summary>
    public static double LoudnessToPower(double lufs) => Math.Pow(10.0, (lufs + 0.691) / 10.0);

    /// <summary>Convert mean-square power back to LUFS.</summary>
    public static double PowerToLoudness(double power) => -0.691 + 10.0 * Math.Log10(power);
}
