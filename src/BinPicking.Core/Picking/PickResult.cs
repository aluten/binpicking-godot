namespace BinPicking.Core.Picking;

/// <summary>Result of one pick attempt.</summary>
/// <param name="RingShare">Share of the contact ring that touched the part.</param>
/// <param name="PulledAlong">Other parts that rose noticeably during lifting.</param>
/// <param name="Disturbed">Other parts that had moved noticeably after settling.</param>
/// <param name="MovedBeforeLift">
/// Other parts that had moved noticeably before lifting began. Nothing has pulled on them yet, so this
/// movement is a simulation artefact rather than an effect of the pick.
/// </param>
/// <param name="LostBeforeLift">The part came off before lifting began, which also points to an artefact.</param>
public sealed record PickResult(
    PickOutcome Outcome,
    float RingShare,
    int PulledAlong,
    int Disturbed,
    float CenterOffset,
    int MovedBeforeLift = 0,
    bool LostBeforeLift = false);
