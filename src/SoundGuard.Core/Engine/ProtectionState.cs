namespace SoundGuard.Core.Engine;

/// <summary>Overall protection status shown to the user.</summary>
public enum ProtectionState
{
    Safe,
    Limiting,
    Muted,
    Bypass,
}

/// <summary>Action the engine should execute this block.</summary>
public enum ProtectionAction
{
    None,
    Attenuate,
    Mute,
    Recover,
}

/// <summary>Which limiter stage dominates the current decision.</summary>
public enum LimiterStage
{
    None,
    Peak,
    Loudness,
}

/// <summary>Immutable snapshot of one analysis pass, published to the UI thread.</summary>
public readonly record struct AnalysisResult(
    double MomentaryLufs,
    double ShortTermLufs,
    double SamplePeakDb,
    double TruePeakDb,
    double GainReductionDb,
    ProtectionState State,
    LimiterStage DominantStage,
    double AttenuationDb,
    double DangerMs,
    long DroppedFrames,
    bool IsBypass);

/// <summary>Immutable decision produced by the response policy.</summary>
public readonly record struct ProtectionDecision(
    ProtectionState State,
    ProtectionAction Action,
    double AttenuationDb,
    LimiterStage Stage,
    string Reason);
