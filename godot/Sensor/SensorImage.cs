#nullable enable

using BinPicking.Core.Sensing;
using Godot;
using static PalletDimensions;

/// <summary>
/// Draws depth as an RGB image. Near is light and far is dark.
/// Holes: magenta is low light, blue is shadow, yellow is an edge (or patterns that disagree) and orange is overexposed.
/// </summary>
public static class SensorImage
{
    /// <summary>Returns RGB bytes, three per pixel. Without <paramref name="missing"/> no holes are coloured.</summary>
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
                    MissingReason.Overexposed => ((byte)250, (byte)130, (byte)20),
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

    /// <summary>
    /// Draws one camera image as grey. The brightness is stretched so the brightest 1% is white, otherwise black
    /// links would hardly be visible. Clipped pixels are orange.
    /// </summary>
    public static byte[] DrawCameraImage(float[] image, float saturationLevel)
    {
        float[] sorted = (float[])image.Clone();
        System.Array.Sort(sorted);
        float white = Mathf.Max(sorted[(int)(sorted.Length * 0.99f)], 1e-4f);
        var pixels = new byte[image.Length * 3];
        for (int i = 0; i < image.Length; i++)
        {
            if (image[i] >= saturationLevel)
            {
                pixels[i * 3] = 250;
                pixels[i * 3 + 1] = 130;
                pixels[i * 3 + 2] = 20;
                continue;
            }
            byte v = (byte)(Mathf.Clamp(image[i] / white, 0f, 1f) * 255f);
            pixels[i * 3] = v;
            pixels[i * 3 + 1] = v;
            pixels[i * 3 + 2] = v;
        }
        return pixels;
    }

    /// <summary>Wraps RGB bytes in a Godot image.</summary>
    public static Image ToImage(byte[] pixels, PinholeCamera camera) =>
        Image.CreateFromData(camera.Width, camera.Height, false, Image.Format.Rgb8, pixels);
}
