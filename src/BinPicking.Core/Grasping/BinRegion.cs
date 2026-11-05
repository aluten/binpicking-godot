namespace BinPicking.Core.Grasping;

/// <summary>The bin in camera space: X to the right, Y down in the image, Z away from the camera.</summary>
public readonly record struct BinRegion(float MinX, float MaxX, float MinY, float MaxY, float RimZ, float FloorZ);
