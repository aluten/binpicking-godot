namespace BinPicking.Core.Grasping;

/// <summary>Counts why locations were rejected, which helps to explain the pipeline.</summary>
public sealed record GraspSearchReport(
    int Evaluated,
    int TooFewPoints,
    int NotFlat,
    int TooTilted,
    int OffCenter,
    int ToolCollision,
    int OutsideOpening,
    int Accepted);
