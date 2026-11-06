using BinPicking.Core.Sensing;

namespace BinPicking.Core.StructuredLight;

/// <summary>
/// A projector next to the camera, looking parallel to it, at a distance of <paramref name="Baseline"/> along camera X.
/// </summary>
/// <param name="Columns">The number of columns in a pattern.</param>
/// <param name="TanHalfWidth">The tangent of the half angle that the pattern covers horizontally.</param>
/// <param name="Baseline">Distance from camera to projector in metres.</param>
public readonly record struct ProjectorModel(int Columns, float TanHalfWidth, float Baseline)
{
    /// <summary>The horizontal direction of a position in the pattern, as X / Z in projector space.</summary>
    public float Slope(float position) => (2f * position / Columns - 1f) * TanHalfWidth;

    /// <summary>The position in the pattern that lights a point in camera space.</summary>
    public float PositionOf(float x, float z) => ((x - Baseline) / z / TanHalfWidth + 1f) * Columns / 2f;

    /// <summary>
    /// Triangulation: intersects the ray of a camera pixel with the plane of light from a pattern position.
    /// From X = a Z for the camera and X - Baseline = b Z for the projector follows Z = Baseline / (a - b).
    /// </summary>
    /// <returns>Depth along the camera axis in metres, or NaN when the rays do not meet in front of the camera.</returns>
    public float Depth(PinholeCamera camera, int pixelX, float position)
    {
        float cameraSlope = (pixelX + 0.5f - camera.Cx) / camera.Fx;
        float difference = cameraSlope - Slope(position);
        return difference > 1e-6f ? Baseline / difference : float.NaN;
    }
}
