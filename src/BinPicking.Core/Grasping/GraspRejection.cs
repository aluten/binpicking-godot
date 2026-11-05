namespace BinPicking.Core.Grasping;

/// <summary>The first requirement that an evaluated location failed.</summary>
public enum GraspRejection
{
    None,
    TooFewPoints,
    NotFlat,
    TooTilted,
    ToolCollision,
    OutsideOpening,
}
