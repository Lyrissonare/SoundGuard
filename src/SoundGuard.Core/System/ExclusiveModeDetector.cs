using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace SoundGuard.Core.System;

public enum CaptureMode
{
    Shared,
    ExclusiveLikely,
    Unknown,
}

/// <summary>
/// Best-effort exclusive-mode detection.
///
/// WASAPI exposes no direct "the endpoint is in exclusive mode" flag, and a shared-mode loopback
/// cannot observe the audio of an exclusive stream (the exclusive app owns the endpoint). The
/// practical heuristic is: if a render session is actively producing level but the loopback capture
/// is silent, the endpoint is likely held exclusively. This class provides the session-level
/// half of that check; the full fallback (master-volume control / virtual device prompt) is wired
/// at milestones 3–4.
/// </summary>
public sealed class ExclusiveModeDetector
{
    /// <summary>Returns true if at least one render session is actively producing audible level.</summary>
    public bool IsAnySessionActive()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager?.Sessions;
            if (sessions == null) return false;

            for (int i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                if (session.State == AudioSessionState.AudioSessionStateActive &&
                    session.AudioMeterInformation?.MasterPeakValue > 0.01f)
                {
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
