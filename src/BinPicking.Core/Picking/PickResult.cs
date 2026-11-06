namespace BinPicking.Core.Picking;

/// <summary>Result of one pick attempt.</summary>
/// <param name="RingShare">Share of the contact ring that touched the part.</param>
/// <param name="PulledAlong">Other parts that rose noticeably during lifting.</param>
/// <param name="Disturbed">Other parts that had moved noticeably after settling.</param>
public sealed record PickResult(
    PickOutcome Outcome,
    float RingShare,
    int PulledAlong,
    int Disturbed,
    float CenterOffset);
