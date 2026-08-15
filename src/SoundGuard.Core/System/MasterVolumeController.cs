using NAudio.CoreAudioApi;

namespace SoundGuard.Core.System;

/// <summary>
/// Controls master volume and mute on the default render endpoint via the Core Audio
/// <c>IAudioEndpointVolume</c> API (wrapped by NAudio's CoreAudioApi).
/// </summary>
public sealed class MasterVolumeController : ISystemAudioController
{
    private readonly MMDevice _device;

    public MasterVolumeController()
    {
        using var enumerator = new MMDeviceEnumerator();
        _device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    }

    public bool IsMuted => _device.AudioEndpointVolume.Mute;

    public float VolumeDb => _device.AudioEndpointVolume.MasterVolumeLevel;

    public void SetMuted(bool muted) => _device.AudioEndpointVolume.Mute = muted;

    public void SetVolumeDb(float db) => _device.AudioEndpointVolume.MasterVolumeLevel = db;

    public void Dispose() => _device.Dispose();
}
