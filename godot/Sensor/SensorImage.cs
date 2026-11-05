#nullable enable

using BinPicking.Core.Sensing;
using Godot;
using static PalletDimensions;

/// <summary>
/// Draws depth as an RGB image. Near is light and far is dark.
/// Holes: magenta is low light, blue is shadow and yellow is an edge.
/// </summary>
public static class SensorImage
{
    public static byte[] Draw(float[] depth, MissingReason[]? missing)
    {
        const float near = SensorMount.AboveRim - 0.05f;
        float far = SensorMount.AboveRim + RimHeight + 0.05f;
        var pixels = new byte[depth.Length * 3];
        for (int i = 0; i < depth.Length; i++)
        {
            byte r, g, b;
            if (missing != null && missing[i] != MissingReason.None)
            {
                (r, g, b) = missing[i] switch
                {
                    MissingReason.LowSignal => ((byte)230, (byte)30, (byte)160),
                    MissingReason.Shadow => ((byte)40, (byte)90, (byte)230),
                    _ => ((byte)240, (byte)210, (byte)30),
                };
            }
            else
            {
                float t = Mathf.Clamp((far - depth[i]) / (far - near), 0f, 1f);
                r = g = b = (byte)(t * 255f);
            }
            pixels[i * 3] = r;
            pixels[i * 3 + 1] = g;
            pixels[i * 3 + 2] = b;
        }
        return pixels;
    }

    public static Image ToImage(byte[] pixels, PinholeCamera camera) =>
        Image.CreateFromData(camera.Width, camera.Height, false, Image.Format.Rgb8, pixels);
}
