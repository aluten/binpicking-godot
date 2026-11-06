namespace BinPicking.Core.Grasping;

/// <summary>Counts why locations were rejected, which helps to explain the pipeline.</summary>
/// <param name="Evaluated">Locations inside the bin that were judged.</param>
/// <param name="Accepted">Locations that passed every requirement, before candidates are thinned out.</param>
public sealed record GraspSearchReport(
    int Evaluated,
    int TooFewPoints,
    int NotFlat,
    int TooTilted,
    int OffCenter,
    int ToolCollision,
    int OutsideOpening,
    int Accepted);
