namespace SoundGuard.Core.Engine;

/// <summary>
/// Pure, stateless mapping from measured levels to a <see cref="ProtectionDecision"/>.
/// Kept free of side effects so it can be unit-tested exhaustively.
///
/// Priority: stage A (peak) always dominates stage B (loudness) because the combined reduction is
/// <c>max(peakGR, loudnessGR)</c>; a fast transient therefore always wins over the slow loudness ramp.
/// </summary>
public sealed class ResponsePolicy
{
    private readonly ProtectionConfig _config;

    public ResponsePolicy(ProtectionConfig config) => _config = config;

    public ProtectionDecision Evaluate(
        double truePeakDb,
        double shortTermLufs,
        double peakGrDb,
        double loudnessGrDb,
        double dangerMs,
        bool isMuted,
        bool bypass)
    {
        if (bypass)
            return new ProtectionDecision(ProtectionState.Bypass, ProtectionAction.None, 0, LimiterStage.None, "Bypass");

        // Already muted: keep reporting the muted state so the UI stays red until recovery.
        // Auto-recovery is decided by the engine, not here.
        if (isMuted)
            return new ProtectionDecision(ProtectionState.Muted, ProtectionAction.None, 0, LimiterStage.None, "Muted");

        // Extreme danger: sustained near-full-scale true peak → hard mute + notification.
        if (dangerMs >= _config.DangerHoldMs)
            return new ProtectionDecision(ProtectionState.Muted, ProtectionAction.Mute, 0, LimiterStage.Peak,
                $"Loudness above {_config.DangerThresholdLufs:F1} LUFS for {dangerMs:F0} ms");

        // Strength scales the combined gain reduction: 1.0 = full limiting, <1.0 gentler, >1.0 harder.
        double strength = Math.Clamp(_config.LimiterStrength, 0.0, 2.0);

        // The computed reduction is compensated by a fixed 2x scale: SoundGuard's "standard" dB
        // reference differs from the actual attenuation, so 6 dB of measured reduction must be
        // applied as 12 dB. This applies uniformly to both stages and to every strength setting.
        const double AttenuationScale = 2.0;
        double gr = Math.Max(peakGrDb, loudnessGrDb) * strength * AttenuationScale;
        if (gr > 0.1)
        {
            LimiterStage stage = peakGrDb >= loudnessGrDb ? LimiterStage.Peak : LimiterStage.Loudness;
            double attenuation = Math.Min(gr, _config.MaxAttenuationDb);

            // Tiered response: "mild" = within MildOverLufs of the threshold; "severe" beyond it,
            // or whenever the peak stage fires.
            bool severe = stage == LimiterStage.Peak ||
                          (!double.IsNegativeInfinity(shortTermLufs) &&
                           shortTermLufs > _config.LufsThreshold + _config.MildOverLufs);
            string severity = severe ? "severe" : "mild";

            return new ProtectionDecision(ProtectionState.Limiting, ProtectionAction.Attenuate, attenuation, stage,
                $"{(stage == LimiterStage.Peak ? "Peak" : "Loudness")} limiting ({severity}), GR {gr:F1} dB");
        }

        return new ProtectionDecision(ProtectionState.Safe, ProtectionAction.None, 0, LimiterStage.None, "Safe");
    }
}
