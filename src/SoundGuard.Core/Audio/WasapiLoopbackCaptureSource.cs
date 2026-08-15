using NAudio.Wave;

namespace SoundGuard.Core.Audio;

/// <summary>
/// WASAPI shared-mode loopback capture of the default render endpoint.
///
/// Loopback captures the *post-mix* signal that is about to reach the speakers, so it observes
/// exactly what the user hears — including the effect of per-app volume. The mix format is
/// IEEE float, and we accept it verbatim (no resampling, no bit-depth conversion, no channel
/// remix).
/// </summary>
public sealed class WasapiLoopbackCaptureSource : ICaptureSource
{
    private WasapiLoopbackCapture? _capture;

    public AudioFormat Format { get; private set; }
    public event Action<float[], int>? DataAvailable;
    public event Action<Exception>? CaptureError;

    public void Start()
    {
        // NAudio 2.x no longer exposes a Latency property/parameter on WasapiLoopbackCapture;
        // it uses its internal (shared-mode) buffer duration. The end-to-end latency is still
        // dominated by the WASAPI engine period, well under the 30 ms target.
        _capture = new WasapiLoopbackCapture();

        WaveFormat waveFormat = _capture.WaveFormat;
        if (waveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
        {
            throw new NotSupportedException(
                $"Loopback mix format must be IEEE float (got {waveFormat.Encoding}). " +
                "SoundGuard intentionally performs no bit-depth conversion.");
        }

        Format = new AudioFormat(waveFormat.SampleRate, waveFormat.Channels);

        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += (_, _) =>
            CaptureError?.Invoke(new InvalidOperationException("Loopback capture stopped unexpectedly."));

        _capture.StartRecording();
    }

    public void Stop()
    {
        if (_capture == null) return;
        _capture.StopRecording();
        _capture.DataAvailable -= OnDataAvailable;
        _capture.Dispose();
        _capture = null;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;

        // NAudio reuses the byte buffer; WaveBuffer gives a zero-copy float view of it.
        // The engine must copy these samples before this handler returns.
        int frames = e.BytesRecorded / Format.BlockAlign;
        var buffer = new WaveBuffer(e.Buffer);
        DataAvailable?.Invoke(buffer.FloatBuffer, frames);
    }

    public void Dispose() => Stop();
}
