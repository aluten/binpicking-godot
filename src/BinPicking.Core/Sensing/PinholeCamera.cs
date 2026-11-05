namespace BinPicking.Core.Sensing;

/// <summary>Pinhole-camera: rekent tussen pixels en punten in het camerastelsel, in meters.</summary>
public readonly record struct PinholeCamera(int Width, int Height, float Fx, float Fy, float Cx, float Cy)
{
    /// <summary>Maakt een camera met een verticale kijkhoek en vierkante pixels.</summary>
    public static PinholeCamera FromVerticalFov(int width, int height, float verticalFovDegrees)
    {
        float focal = (height / 2f) / MathF.Tan(verticalFovDegrees * MathF.PI / 360f);
        return new PinholeCamera(width, height, focal, focal, width / 2f, height / 2f);
    }
}
