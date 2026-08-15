namespace SoundGuard.Core.Audio;

/// <summary>
/// Minimal abstraction over an audio capture backend so the engine is testable and
/// the WASAPI backend can later be swapped for a virtual-device / APO backend.
/// </summary>
public interface ICaptureSource : IDisposable
{
    /// <summary>Format of the produced samples. Set before <see cref="Start"/>.</summary>
    AudioFormat Format { get; }

    /// <summary>
    /// Raised on the capture thread with interleaved 32-bit float samples.
    /// The array is reused by the backend: consumers must copy before returning.
    /// </summary>
    event Action<float[], int>? DataAvailable;

    void Start();
    void Stop();
}
