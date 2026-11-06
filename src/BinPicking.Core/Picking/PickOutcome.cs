namespace BinPicking.Core.Picking;

/// <summary>How a pick attempt ended.</summary>
public enum PickOutcome
{
    /// <summary>The part was lifted and placed.</summary>
    Placed,

    /// <summary>The cup could not seal, for example because the ring crosses an edge.</summary>
    NoSeal,

    /// <summary>The part came off during lifting or transport, for example because it was hooked.</summary>
    LostDuringLift,

    /// <summary>No part was found at the pick point.</summary>
    NoPartAtPoint,
}
