namespace BinPicking.Core.Sensing;

/// <summary>Pinhole camera model. Converts between pixels and camera-space points in metres.</summary>
public readonly record struct PinholeCamera(int Width, int Height, float Fx, float Fy, float Cx, float Cy)
{
    /// <summary>Creates a camera from a vertical field of view, assuming square pixels.</summary>
    public static PinholeCamera FromVerticalFov(int width, int height, float verticalFovDegrees)
    {
        float focal = (height / 2f) / MathF.Tan(verticalFovDegrees * MathF.PI / 360f);
        return new PinholeCamera(width, height, focal, focal, width / 2f, height / 2f);
    }

    /// <summary>Returns the camera-space X and Y of a pixel centre at depth <paramref name="z"/>.</summary>
    public (float X, float Y) Unproject(int x, int y, float z) =>
        ((x + 0.5f - Cx) * z / Fx, (y + 0.5f - Cy) * z / Fy);
}
