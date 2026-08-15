namespace SoundGuard.Core.Audio;

/// <summary>
/// Describes a capture stream. SoundGuard never resamples, converts bit depth or remixes:
/// it always runs on the device mix format (32-bit float, N channels, native sample rate).
/// </summary>
public readonly record struct AudioFormat(int SampleRate, int Channels)
{
    /// <summary>Bytes per single sample. Fixed at 4 (IEEE 754 single precision float).</summary>
    public int BytesPerSample => 4;

    /// <summary>Bytes per interleaved frame (one sample for every channel).</summary>
    public int BlockAlign => Channels * BytesPerSample;

    public override string ToString() => $"{SampleRate} Hz / {Channels} ch / float32";
}
