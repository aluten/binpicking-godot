namespace BinPicking.Core.Grasping;

/// <summary>A pick point in camera space, in metres. The normal points towards the camera.</summary>
/// <param name="PixelX">Image column of the evaluated location.</param>
/// <param name="PixelY">Image row of the evaluated location.</param>
/// <param name="X">The cup centre on the fitted plane.</param>
/// <param name="Y">The cup centre on the fitted plane.</param>
/// <param name="Z">The cup centre on the fitted plane.</param>
/// <param name="NormalX">Surface normal, which is also the approach direction of the tool.</param>
/// <param name="NormalY">Surface normal.</param>
/// <param name="NormalZ">Surface normal; negative, because camera Z points away from the camera.</param>
/// <param name="ValidShare">Share of the cup area with a measurement.</param>
/// <param name="InlierShare">Share of the measured points that lie in the plane.</param>
/// <param name="TiltDegrees">Angle between the surface normal and the camera axis.</param>
/// <param name="CenterOffset">Distance to the middle of the flat surface.</param>
/// <param name="Score">Ranking value; higher is better. See <see cref="GraspEvaluator"/>.</param>
public sealed record GraspCandidate(
    int PixelX,
    int PixelY,
    float X,
    float Y,
    float Z,
    float NormalX,
    float NormalY,
    float NormalZ,
    float ValidShare,
    float InlierShare,
    float TiltDegrees,
    float CenterOffset,
    float Score);
