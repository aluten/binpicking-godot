namespace BinPicking.Core.Grasping;

/// <summary>A pick point in camera space. The normal points towards the camera.</summary>
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
