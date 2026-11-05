namespace BinPicking.Core.Grasping;

/// <summary>A plane in camera space, written as z = A x + B y + C.</summary>
internal readonly record struct Plane(float A, float B, float C)
{
    private float Length => MathF.Sqrt(A * A + B * B + 1f);

    public float NormalX => A / Length;

    public float NormalY => B / Length;

    /// <summary>Component towards the camera; 1 means facing the camera directly.</summary>
    public float NormalZTowardCamera => 1f / Length;

    public float ZAt(float x, float y) => A * x + B * y + C;

    public float Distance(float x, float y, float z) => MathF.Abs(ZAt(x, y) - z) / Length;
}
