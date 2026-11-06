namespace BinPicking.Core.StructuredLight;

/// <summary>
/// Turns the light on each pixel into what an image sensor delivers: shot noise, read noise, clipping and rounding.
/// Weak signals suffer most, because shot noise grows only with the square root of the light. This is why black
/// parts get holes in a structured-light measurement without any special rule for black.
/// </summary>
public static class CameraNoiseModel
{
    /// <param name="light">Light per pixel in the unit of the renderer.</param>
    /// <param name="settings">The sensor.</param>
    /// <param name="seed">The same seed gives the same noise.</param>
    /// <returns>The image, from 0 to 1 of the full scale.</returns>
    public static float[] Expose(float[] light, CameraNoiseSettings settings, int seed)
    {
        var random = new Random(seed);
        int levels = (1 << settings.Bits) - 1;
        var image = new float[light.Length];
        for (int i = 0; i < light.Length; i++)
        {
            float electrons = MathF.Max(light[i], 0f) * settings.ElectronsPerLightUnit;
            float sigma = MathF.Sqrt(electrons + settings.ReadNoiseElectrons * settings.ReadNoiseElectrons);
            float measured = electrons + sigma * Gaussian(random);
            float share = Math.Clamp(measured / settings.FullWellElectrons, 0f, 1f);
            image[i] = MathF.Round(share * levels) / levels;
        }
        return image;
    }

    /// <summary>Normally distributed value (Box-Muller). Shot noise is close to normal from a few dozen electrons.</summary>
    private static float Gaussian(Random random)
    {
        double a = 1.0 - random.NextDouble();
        double b = random.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(a)) * Math.Cos(2.0 * Math.PI * b));
    }
}
