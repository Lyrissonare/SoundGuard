namespace SoundGuard.Core.System;

/// <summary>
/// Abstraction over the OS audio endpoint so the engine is testable without touching real hardware.
/// Implemented by <see cref="MasterVolumeController"/> for the default render endpoint.
/// </summary>
public interface ISystemAudioController : IDisposable
{
    bool IsMuted { get; }
    float VolumeDb { get; }
    void SetMuted(bool muted);
    void SetVolumeDb(float db);
}
