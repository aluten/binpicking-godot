namespace BinPicking.Core.Grasping;

/// <summary>
/// The bin in camera space: X to the right, Y down in the image, Z away from the camera. In metres.
/// This box only fits a camera that looks straight into the bin. A tilted camera first needs its points
/// transformed; see docs/demo-questions.md.
/// </summary>
/// <param name="RimZ">Distance from the camera to the top of the search volume.</param>
/// <param name="FloorZ">Distance from the camera to the bottom of the search volume.</param>
public readonly record struct BinRegion(float MinX, float MaxX, float MinY, float MaxY, float RimZ, float FloorZ);
