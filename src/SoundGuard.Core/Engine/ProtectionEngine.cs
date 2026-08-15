using SoundGuard.Core.Audio;
using SoundGuard.Core.Dsp;
using SoundGuard.Core.System;

namespace SoundGuard.Core.Engine;

/// <summary>
/// Owns the capture source, the lock-free ring buffer and the analysis thread, and drives the
/// system audio controller.
///
/// The capture <see cref="AudioFormat"/> is only known once the WASAPI client connects, so the DSP
/// pipeline is initialized inside <see cref="Start"/> (after <c>capture.Start()</c>).
///
/// Threading model:
///   capture callback (producer) → <see cref="LockFreeRingBuffer"/> → analysis thread (consumer) → UI (events).
/// The UI must marshal events to the dispatcher thread; this class raises them on the analysis thread.
///
/// Audio-control model:
///   Mute/unmute are applied directly (they are one-shot user/safety actions, not a hot path).
///   Master-volume attenuation is applied by a throttled <see cref="System.Threading.Timer"/> at ~10 Hz,
///   because that IS the hot path — doing it per analysis block floods the Core Audio service.
/// </summary>
public sealed class ProtectionEngine : IDisposable
{
    private readonly ProtectionConfig _config;
    private readonly ISystemAudioController _audio;
    private readonly ICaptureSource _capture;
    private readonly ResponsePolicy _policy;

    private LockFreeRingBuffer _ring = null!;
    private PeakMeter _peakMeter = null!;
    private LoudnessMeter _loudnessMeter = null!;
    private SoftKneeLimiter _peakLimiter = null!;
    private LoudnessLimiter _loudnessLimiter = null!;

    private int _channels;
    private int _blockFrames;
    private double _blockSeconds;

    private Thread? _thread;
    private volatile bool _running;

    private bool _bypass;
    private volatile bool _muted;
    private double _dangerMs;
    private DateTime _mutedAt;

    // Desired audio state, written by the analysis/UI threads and applied by the timer thread.
    private readonly object _audioLock = new();
    private readonly Timer _audioTimer;
    private volatile float _targetAttenuationDb;
    private float? _baselineVolumeDb;
    private float _lastAppliedVolumeDb = float.NaN;
    private const int AudioApplyIntervalMs = 100;

    /// <summary>
    /// How long the ring buffer may be starved (no capture data) before we conclude the endpoint is
    /// silent/idle and start synthesizing silence. Must exceed the ~100 ms WASAPI capture period so
    /// normal playback gaps never trigger it.
    /// </summary>
    private const int CaptureIdleMs = 500;

    public event Action<AnalysisResult>? Updated;
    public event Action<string, string, ProtectionState>? NotificationRequested;
    public event Action<ProtectionState>? StateChanged;

    public bool IsRunning => _running;

    /// <summary>Valid after <see cref="Start"/>.</summary>
    public AudioFormat Format => _capture.Format;

    /// <summary>True disables all protection actions while meters keep measuring (for A/B comparison).</summary>
    public bool Bypass
    {
        get => _bypass;
        set => _bypass = value;
    }

    public ProtectionEngine(ProtectionConfig config, ISystemAudioController audio, ICaptureSource capture)
    {
        _config = config;
        _audio = audio;
        _capture = capture;
        _policy = new ResponsePolicy(config);

        // Throttled attenuation loop: master-volume COM calls are applied here (never on the
        // real-time analysis thread), at ~10 Hz, to avoid flooding the Core Audio service.
        _audioTimer = new Timer(ApplyAudioState, null, Timeout.Infinite, Timeout.Infinite);

        // Attach early; the handler drops data until the pipeline is initialized in Start().
        _capture.DataAvailable += OnDataAvailable;
    }

    public void Start()
    {
        if (_running) return;

        _capture.Start();
        InitializePipeline();

        _running = true;
        _audioTimer.Change(AudioApplyIntervalMs, AudioApplyIntervalMs);
        _thread = new Thread(AnalysisLoop) { IsBackground = true, Name = "SoundGuard.Analysis" };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        _thread?.Join(1000);
        _audioTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _capture.Stop();

        // Clean shutdown: unmute and restore the pre-attenuation volume.
        _muted = false;
        _targetAttenuationDb = 0f;
        try { _audio.SetMuted(false); } catch { /* one-shot, non-fatal */ }
        ApplyAudioState(null);
    }

    /// <summary>Enable/disable game mode (tightens the LUFS threshold by <see cref="ProtectionConfig.GameModeLufsOffset"/>).</summary>
    public void SetGameMode(bool enabled)
    {
        _config.GameMode = enabled;
        if (_loudnessLimiter != null)
            _loudnessLimiter.ThresholdLufs = _config.EffectiveLufsThreshold;
    }

    /// <summary>User-initiated mute (tray / button). Auto-recovery still applies.</summary>
    public void MuteForUser()
    {
        _muted = true;
        _mutedAt = DateTime.UtcNow;
        try { _audio.SetMuted(true); } catch { /* one-shot, non-fatal */ }
        StateChanged?.Invoke(ProtectionState.Muted);
    }

    /// <summary>One-click recovery: unmute and restore the pre-attenuation volume.</summary>
    public void RecoverNow()
    {
        _muted = false;
        _mutedAt = default;
        _targetAttenuationDb = 0f;
        try { _audio.SetMuted(false); } catch { /* one-shot, non-fatal */ }
        StateChanged?.Invoke(ProtectionState.Safe);
    }

    private void InitializePipeline()
    {
        _channels = _capture.Format.Channels;
        int sampleRate = _capture.Format.SampleRate;

        if (_channels <= 0 || sampleRate <= 0)
            throw new InvalidOperationException($"Invalid capture format: {_capture.Format}");

        // Detection window: ~5 ms (the stage-A window), rounded to a sane minimum.
        _blockFrames = Math.Max(32, (int)Math.Round(sampleRate * 0.005));
        _blockSeconds = _blockFrames / (double)sampleRate;

        // The ring must hold at least one full WASAPI capture event. NAudio's default loopback
        // buffer is ~100 ms (e.g. 9600 floats @ 48 kHz stereo), far larger than a 5 ms analysis
        // block. Size the ring to ~1 second of audio (min 16 blocks) so a capture event never
        // overflows it and gets dropped wholesale.
        int ringCapacity = Math.Max(_blockFrames * _channels * 16, sampleRate * _channels);
        _ring = new LockFreeRingBuffer(ringCapacity);
        _peakMeter = new PeakMeter(sampleRate);
        _loudnessMeter = new LoudnessMeter(_channels, sampleRate);
        _peakLimiter = new SoftKneeLimiter(_channels, sampleRate, new LimiterParams
        {
            ThresholdDb = _config.PeakThresholdDb,
            KneeDb = _config.PeakKneeDb,
            AttackMs = _config.PeakAttackMs,
            ReleaseMs = _config.PeakReleaseMs,
            LookaheadMs = _config.PeakLookaheadMs,
        });
        _loudnessLimiter = new LoudnessLimiter(
            _config.EffectiveLufsThreshold, _config.LoudnessAttackMs, _config.LoudnessReleaseMs,
            _blockSeconds, _config.MaxAttenuationDb);
    }

    private void OnDataAvailable(float[] samples, int frames)
    {
        // Copy immediately: NAudio reuses the buffer. Drop until the pipeline exists.
        if (_ring == null) return;
        _ring.TryWrite(samples, 0, frames * _channels);
    }

    private void AnalysisLoop()
    {
        float[] block = new float[_blockFrames * _channels];
        int blockMs = Math.Max(1, (int)Math.Round(_blockSeconds * 1000.0));
        int filled = 0;
        int starvedMs = 0;      // consecutive milliseconds with no capture data
        int silenceTimerMs = 0; // throttle for synthesized silence (real-time rate)

        while (_running)
        {
            // Accumulate partial reads into the block so no sample is discarded between events.
            int read = _ring.Read(block, filled, block.Length - filled);
            filled += read;

            if (filled < block.Length)
            {
                Thread.Sleep(1);

                if (read == 0)
                {
                    starvedMs += 1;
                    if (starvedMs >= CaptureIdleMs)
                    {
                        // Capture is idle (no audio playing → WASAPI stops delivering data).
                        // Synthesize silent blocks at real-time rate so the meters decay to zero
                        // and the UI keeps receiving updates instead of freezing.
                        silenceTimerMs += 1;
                        if (silenceTimerMs >= blockMs)
                        {
                            Array.Clear(block, filled, block.Length - filled);
                            ProcessBlock(block);
                            filled = 0;
                            silenceTimerMs = 0;
                        }
                    }
                }
                else
                {
                    starvedMs = 0;
                    silenceTimerMs = 0;
                }
                continue;
            }

            ProcessBlock(block);
            filled = 0;
            starvedMs = 0;
            silenceTimerMs = 0;
        }
    }

    private void ProcessBlock(float[] block)
    {
        _peakMeter.Process(block);
        _loudnessMeter.Process(block, _blockFrames);

        // Push live thresholds into the limiters so slider changes take effect immediately.
        _peakLimiter.ThresholdDb = _config.PeakThresholdDb;
        _loudnessLimiter.ThresholdLufs = _config.EffectiveLufsThreshold;

        double peakGr = _peakLimiter.Process(block, _blockFrames);
        double truePeakDb = _peakLimiter.MeasuredTruePeakDb;
        double loudnessGr = _loudnessLimiter.Step(_loudnessMeter.ShortTermLufs);

        // Danger tracking: sustained short-term loudness above the danger threshold.
        if (_loudnessMeter.ShortTermLufs > _config.DangerThresholdLufs)
            _dangerMs += _blockSeconds * 1000.0;
        else
            _dangerMs = 0.0;

        var decision = _policy.Evaluate(
            truePeakDb, _loudnessMeter.ShortTermLufs, peakGr, loudnessGr, _dangerMs, _muted, _bypass);

        Execute(decision);

        var result = new AnalysisResult(
            _loudnessMeter.MomentaryLufs,
            _loudnessMeter.ShortTermLufs,
            _peakMeter.PeakDbFs,
            truePeakDb,
            decision.AttenuationDb,
            decision.State,
            decision.Stage,
            decision.AttenuationDb,
            _dangerMs,
            _ring.DroppedFrames,
            _bypass);

        Updated?.Invoke(result);
    }

    private void Execute(ProtectionDecision d)
    {
        switch (d.Action)
        {
            case ProtectionAction.Mute:
                _muted = true;
                _mutedAt = DateTime.UtcNow;
                try { _audio.SetMuted(true); } catch { /* one-shot, non-fatal */ }
                StateChanged?.Invoke(ProtectionState.Muted);
                NotificationRequested?.Invoke("SoundGuard 静音保护", d.Reason, ProtectionState.Muted);
                break;

            case ProtectionAction.Attenuate:
                _targetAttenuationDb = (float)d.AttenuationDb;
                break;

            case ProtectionAction.None:
            case ProtectionAction.Recover:
                if (_muted)
                {
                    // Auto-recover after the configured delay, but only once the signal is safe.
                    if (_config.AutoRecoverEnabled && _dangerMs == 0.0 &&
                        (DateTime.UtcNow - _mutedAt).TotalMilliseconds >= _config.AutoRecoverMs)
                    {
                        RecoverNow();
                        NotificationRequested?.Invoke("SoundGuard", "已自动恢复音量", ProtectionState.Safe);
                    }
                }
                else
                {
                    _targetAttenuationDb = 0f;
                }
                break;
        }
    }

    /// <summary>
    /// Throttled application of master-volume attenuation. Runs on a thread-pool thread at ~10 Hz
    /// so Core Audio COM calls never block the analysis thread or flood the audio service.
    /// A failed call cannot crash the loop.
    /// </summary>
    private void ApplyAudioState(object? state)
    {
        lock (_audioLock)
        {
            try
            {
                float attenuation = _targetAttenuationDb;
                if (!_muted && attenuation > 0.05f)
                {
                    _baselineVolumeDb ??= _audio.VolumeDb;
                    float target = Math.Max(_baselineVolumeDb.Value - attenuation, -60f);
                    if (Math.Abs(target - _lastAppliedVolumeDb) > 0.5f)
                    {
                        _audio.SetVolumeDb(target);
                        _lastAppliedVolumeDb = target;
                    }
                }
                else if (attenuation <= 0.05f)
                {
                    if (_baselineVolumeDb is float baseline)
                    {
                        if (Math.Abs(baseline - _lastAppliedVolumeDb) > 0.5f)
                        {
                            _audio.SetVolumeDb(baseline);
                            _lastAppliedVolumeDb = baseline;
                        }
                        _baselineVolumeDb = null;
                    }
                }
            }
            catch
            {
                // Never let a failed audio-control call crash the timer loop.
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _capture.DataAvailable -= OnDataAvailable;
        _capture.Dispose();
        _audioTimer.Dispose();
        _audio.Dispose();
    }
}
