namespace SoundGuard.Core.Engine;

/// <summary>Protection thresholds and timing. Persisted as part of <see cref="Config.AppConfig"/>.</summary>
public sealed class ProtectionConfig
{
    // --- Stage B: loudness ---
    /// <summary>Short-term LUFS threshold (range -14 … -26).</summary>
    public double LufsThreshold { get; set; } = -18.0;

    // --- Stage A: peak ---
    /// <summary>Peak (true-peak) threshold in dBFS.</summary>
    public double PeakThresholdDb { get; set; } = -1.0;

    // --- Extreme danger ---
    /// <summary>
    /// Short-term loudness above this value (LUFS) starts counting toward a hard mute.
    /// Default -6 LUFS (very loud but below clipping); tunable because decoder/amplifier gain
    /// can make this level dangerous in practice.
    /// </summary>
    public double DangerThresholdLufs { get; set; } = -6.0;

    /// <summary>Hold time above the danger threshold before muting.</summary>
    public double DangerHoldMs { get; set; } = 100.0;

    // --- Response shaping ---
    /// <summary>Over threshold by ≤ this many LU counts as "mild" (progressive) limiting.</summary>
    public double MildOverLufs { get; set; } = 3.0;

    /// <summary>
    /// Maximum master-volume attenuation the protection may apply (dB). Raised to 80 dB because
    /// the gain reduction is now compensated by a 2x scale (see <see cref="ResponsePolicy"/>):
    /// the pre-compensation 40 dB ceiling no longer matches the actual applied attenuation.
    /// </summary>
    public double MaxAttenuationDb { get; set; } = 80.0;

    /// <summary>
    /// Limiter strength (压限幅度), 0.0–2.0. Scales the computed gain reduction before the fixed
    /// 2x compensation in <see cref="ResponsePolicy"/> is applied. 1.0 = full limiting (the "neutral"
    /// point, shown as 50% in the UI), 0.0 = no limiting, 2.0 = double limiting. Default 1.0.
    /// </summary>
    public double LimiterStrength { get; set; } = 1.0;

    // --- Stage A timings ---
    public double PeakAttackMs { get; set; } = 0.8;
    public double PeakReleaseMs { get; set; } = 200.0;
    public double PeakLookaheadMs { get; set; } = 1.5;
    public double PeakKneeDb { get; set; } = 3.0;

    // --- Stage B timings ---
    public double LoudnessAttackMs { get; set; } = 150.0;
    public double LoudnessReleaseMs { get; set; } = 2000.0;

    // --- Recovery ---
    public double AutoRecoverMs { get; set; } = 10000.0;
    public bool AutoRecoverEnabled { get; set; } = true;

    // --- Game mode ---
    public bool GameMode { get; set; }
    public double GameModeLufsOffset { get; set; } = -3.0;

    /// <summary>Effective LUFS threshold, tightened when game mode is active.</summary>
    public double EffectiveLufsThreshold => GameMode ? LufsThreshold + GameModeLufsOffset : LufsThreshold;
}
