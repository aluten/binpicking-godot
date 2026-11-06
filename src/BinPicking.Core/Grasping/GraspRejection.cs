namespace BinPicking.Core.Grasping;

/// <summary>The first requirement that an evaluated location failed.</summary>
public enum GraspRejection
{
    /// <summary>The location passed every requirement.</summary>
    None,

    /// <summary>Too little of the cup area has a measurement.</summary>
    TooFewPoints,

    /// <summary>No plane fits the points under the cup.</summary>
    NotFlat,

    /// <summary>The surface is tilted too far from the camera axis.</summary>
    TooTilted,

    /// <summary>The cup is too far from the middle of the flat surface.</summary>
    OffCenter,

    /// <summary>The holder would hit something higher nearby.</summary>
    ToolCollision,

    /// <summary>The holder does not fit through the bin opening along the normal.</summary>
    OutsideOpening,
}
