namespace SoundGuard.Core.Dsp;

/// <summary>Exact ITU-R BS.1770-4 Annex 1 K-weighting constants.</summary>
public static class KWeighting
{
    /// <summary>Stage 1 high-shelf centre frequency.</summary>
    public const double ShelfCenterHz = 1681.974450955533;

    /// <summary>Stage 1 high-shelf gain (boost).</summary>
    public const double ShelfGainDb = 3.999843853973347;

    /// <summary>Stage 1 high-shelf Q.</summary>
    public const double ShelfQ = 0.7071752369554196;

    /// <summary>Stage 2 high-pass centre frequency.</summary>
    public const double HighPassCenterHz = 38.13547087602444;

    /// <summary>Stage 2 high-pass Q.</summary>
    public const double HighPassQ = 0.5003270373238773;

    /// <summary>Offset applied when converting mean-square to LUFS (accounts for the shelf gain).</summary>
    public const double LoudnessOffsetDb = -0.691;
}

/// <summary>
/// ITU-R BS.1770-4 K-weighting: high-shelf (+4 dB @ ~1.68 kHz) followed by a high-pass (~38 Hz),
/// applied independently to every channel. Channel summation weights are applied later, not here.
/// </summary>
public sealed class KWeightingFilter
{
    private readonly BiquadFilter[] _shelf;
    private readonly BiquadFilter[] _highPass;

    public KWeightingFilter(int channels, double sampleRate)
    {
        _shelf = new BiquadFilter[channels];
        _highPass = new BiquadFilter[channels];

        for (int c = 0; c < channels; c++)
        {
            _shelf[c] = new BiquadFilter();
            _shelf[c].ConfigureHighShelf(sampleRate, KWeighting.ShelfCenterHz, KWeighting.ShelfGainDb, KWeighting.ShelfQ);

            _highPass[c] = new BiquadFilter();
            _highPass[c].ConfigureHighPass(sampleRate, KWeighting.HighPassCenterHz, KWeighting.HighPassQ);
        }
    }

    public double Process(int channel, double sample) =>
        _highPass[channel].Process(_shelf[channel].Process(sample));

    public void Reset()
    {
        for (int c = 0; c < _shelf.Length; c++)
        {
            _shelf[c].Reset();
            _highPass[c].Reset();
        }
    }
}
