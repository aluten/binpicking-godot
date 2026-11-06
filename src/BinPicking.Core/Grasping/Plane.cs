namespace BinPicking.Core.Grasping;

/// <summary>
/// A plane in camera space, written as z = A x + B y + C. This form cannot describe a surface that stands
/// parallel to the view ray, but the camera cannot measure such a surface anyway.
/// </summary>
internal readonly record struct Plane(float A, float B, float C)
{
    private float Length => MathF.Sqrt(A * A + B * B + 1f);

    /// <summary>X component of the unit normal that points towards the camera.</summary>
    public float NormalX => A / Length;

    /// <summary>Y component of the unit normal that points towards the camera.</summary>
    public float NormalY => B / Length;

    /// <summary>Component towards the camera; 1 means facing the camera directly.</summary>
    public float NormalZTowardCamera => 1f / Length;

    /// <summary>The depth of the plane at this X and Y.</summary>
    public float ZAt(float x, float y) => A * x + B * y + C;

    /// <summary>Perpendicular distance from a point to the plane.</summary>
    public float Distance(float x, float y, float z) => MathF.Abs(ZAt(x, y) - z) / Length;
}
