using SoundGuard.Core.Engine;
using Xunit;

namespace SoundGuard.Tests;

public class ResponsePolicyTests
{
    [Fact]
    public void FullStrength_AppliesFullReduction()
    {
        var policy = new ResponsePolicy(new ProtectionConfig { LimiterStrength = 1.0 });

        var d = policy.Evaluate(truePeakDb: -20, shortTermLufs: -12, peakGrDb: 0, loudnessGrDb: 6,
                                dangerMs: 0, isMuted: false, bypass: false);

        Assert.Equal(ProtectionState.Limiting, d.State);
        Assert.Equal(12.0, d.AttenuationDb, 3); // 6 dB × 1.0 strength × 2.0 scale
    }

    [Fact]
    public void HalfStrength_HalvesGainReduction()
    {
        var policy = new ResponsePolicy(new ProtectionConfig { LimiterStrength = 0.5 });

        var d = policy.Evaluate(truePeakDb: -20, shortTermLufs: -12, peakGrDb: 0, loudnessGrDb: 6,
                                dangerMs: 0, isMuted: false, bypass: false);

        Assert.Equal(ProtectionState.Limiting, d.State);
        Assert.Equal(6.0, d.AttenuationDb, 3); // 6 dB × 0.5 strength × 2.0 scale
    }

    [Fact]
    public void DoubleStrength_DoublesGainReduction()
    {
        var policy = new ResponsePolicy(new ProtectionConfig { LimiterStrength = 2.0 });

        var d = policy.Evaluate(truePeakDb: -20, shortTermLufs: -12, peakGrDb: 0, loudnessGrDb: 6,
                                dangerMs: 0, isMuted: false, bypass: false);

        Assert.Equal(ProtectionState.Limiting, d.State);
        Assert.Equal(24.0, d.AttenuationDb, 3); // 6 dB × 2.0 strength × 2.0 scale, below the 80 dB cap
    }

    [Fact]
    public void FullStrength_IsCappedAtMaxAttenuation()
    {
        var policy = new ResponsePolicy(new ProtectionConfig { LimiterStrength = 1.0 });

        var d = policy.Evaluate(truePeakDb: -20, shortTermLufs: -12, peakGrDb: 0, loudnessGrDb: 60,
                                dangerMs: 0, isMuted: false, bypass: false);

        Assert.Equal(ProtectionState.Limiting, d.State);
        Assert.Equal(80.0, d.AttenuationDb, 3); // 60 dB × 2.0 scale = 120 dB → capped at 80 dB
    }

    [Fact]
    public void ZeroStrength_DisablesLimiting()
    {
        var policy = new ResponsePolicy(new ProtectionConfig { LimiterStrength = 0.0 });

        var d = policy.Evaluate(truePeakDb: -20, shortTermLufs: -12, peakGrDb: 0, loudnessGrDb: 6,
                                dangerMs: 0, isMuted: false, bypass: false);

        Assert.Equal(ProtectionState.Safe, d.State);
        Assert.Equal(0.0, d.AttenuationDb, 3);
    }

    [Fact]
    public void Strength_DoesNotAffectExtremeDangerMute()
    {
        var policy = new ResponsePolicy(new ProtectionConfig { LimiterStrength = 0.0, DangerHoldMs = 100.0 });

        // Even at zero strength, sustained near-full-scale must still mute.
        var d = policy.Evaluate(truePeakDb: -0.05, shortTermLufs: -10, peakGrDb: 0, loudnessGrDb: 0,
                                dangerMs: 150, isMuted: false, bypass: false);

        Assert.Equal(ProtectionState.Muted, d.State);
        Assert.Equal(ProtectionAction.Mute, d.Action);
    }

    [Fact]
    public void AlreadyMuted_KeepsReportingMutedState()
    {
        var policy = new ResponsePolicy(new ProtectionConfig());

        // While muted, even a quiet signal must keep the state red ("静音保护") instead of flipping
        // back to Safe, so the UI reflects that the user/system is muted.
        var d = policy.Evaluate(truePeakDb: -40, shortTermLufs: -30, peakGrDb: 0, loudnessGrDb: 0,
                                dangerMs: 0, isMuted: true, bypass: false);

        Assert.Equal(ProtectionState.Muted, d.State);
        Assert.Equal(ProtectionAction.None, d.Action);
    }
}
