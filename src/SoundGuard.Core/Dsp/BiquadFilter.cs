namespace SoundGuard.Core.Dsp;

/// <summary>
/// Transposed Direct Form II biquad filter, used for the two stages of BS.1770 K-weighting.
/// Coefficients follow the RBJ Audio EQ Cookbook.
/// </summary>
public sealed class BiquadFilter
{
    private double _b0, _b1, _b2, _a1, _a2;
    private double _z1, _z2; // filter state (one instance per channel)

    /// <summary>High-shelf (RBJ). <paramref name="gainDb"/> is the shelf gain (positive = boost).</summary>
    public void ConfigureHighShelf(double sampleRate, double centerHz, double gainDb, double q)
    {
        double a = Math.Pow(10.0, gainDb / 40.0);
        double w0 = 2.0 * Math.PI * centerHz / sampleRate;
        double cos = Math.Cos(w0);
        double sin = Math.Sin(w0);
        double alpha = sin / (2.0 * q);
        double sq = 2.0 * Math.Sqrt(a) * alpha;

        double b0 = a * ((a + 1.0) + (a - 1.0) * cos + sq);
        double b1 = -2.0 * a * ((a - 1.0) + (a + 1.0) * cos);
        double b2 = a * ((a + 1.0) + (a - 1.0) * cos - sq);
        double a0 = (a + 1.0) - (a - 1.0) * cos + sq;
        double a1 = 2.0 * ((a - 1.0) - (a + 1.0) * cos);
        double a2 = (a + 1.0) - (a - 1.0) * cos - sq;

        Normalize(a0, a1, a2, b0, b1, b2);
    }

    /// <summary>Second-order high-pass (RBJ).</summary>
    public void ConfigureHighPass(double sampleRate, double centerHz, double q)
    {
        double w0 = 2.0 * Math.PI * centerHz / sampleRate;
        double cos = Math.Cos(w0);
        double sin = Math.Sin(w0);
        double alpha = sin / (2.0 * q);

        double b0 = (1.0 + cos) / 2.0;
        double b1 = -(1.0 + cos);
        double b2 = (1.0 + cos) / 2.0;
        double a0 = 1.0 + alpha;
        double a1 = -2.0 * cos;
        double a2 = 1.0 - alpha;

        Normalize(a0, a1, a2, b0, b1, b2);
    }

    private void Normalize(double a0, double a1, double a2, double b0, double b1, double b2)
    {
        _b0 = b0 / a0;
        _b1 = b1 / a0;
        _b2 = b2 / a0;
        _a1 = a1 / a0;
        _a2 = a2 / a0;
        Reset();
    }

    public void Reset()
    {
        _z1 = 0;
        _z2 = 0;
    }

    public double Process(double x)
    {
        double y = _b0 * x + _z1;
        _z1 = _b1 * x - _a1 * y + _z2;
        _z2 = _b2 * x - _a2 * y;
        return y;
    }
}
