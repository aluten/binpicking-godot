using BinPicking.Core.Sensing;

namespace BinPicking.Core.StructuredLight;

/// <summary>
/// Turns the images of one measurement into depth. Per pixel: the sines give the phase and the signal strength,
/// the stripes say which period that phase belongs to, and together they give the projector column. Triangulation
/// then gives the depth. Nothing in here knows the scene; every hole follows from the images alone.
/// </summary>
public static class PatternDecoder
{
    /// <param name="camera">The camera that made the images.</param>
    /// <param name="projector">The projector that lit the scene.</param>
    /// <param name="settings">The patterns, which must match what was projected.</param>
    /// <param name="images">The images in the order of <see cref="PatternGenerator"/>, from 0 to 1 per pixel.</param>
    /// <returns>Depth in metres with NaN for a hole, the same form as the sensor model delivers.</returns>
    public static DepthSensorResult Decode(
        PinholeCamera camera, ProjectorModel projector, StructuredLightSettings settings, IReadOnlyList<float[]> images)
    {
        if (images.Count != settings.ImageCount)
        {
            throw new ArgumentException($"Expected {settings.ImageCount} images, got {images.Count}.");
        }

        int count = camera.Width * camera.Height;
        var depth = new float[count];
        var missing = new MissingReason[count];
        for (int i = 0; i < count; i++)
        {
            depth[i] = float.NaN;
            missing[i] = DecodePixel(camera, projector, settings, images, i, out float z);
            if (missing[i] == MissingReason.None)
            {
                depth[i] = z;
            }
        }
        return new DepthSensorResult(depth, missing);
    }

    private static MissingReason DecodePixel(
        PinholeCamera camera, ProjectorModel projector, StructuredLightSettings settings,
        IReadOnlyList<float[]> images, int pixel, out float z)
    {
        z = float.NaN;

        // Sines: the average is the threshold for the stripes, and the amplitude is the usable signal.
        double sumCos = 0, sumSin = 0, sum = 0;
        for (int k = 0; k < settings.SineSteps; k++)
        {
            float value = images[k][pixel];
            if (value >= settings.SaturationLevel)
            {
                return MissingReason.Overexposed;
            }
            double shift = Math.Tau * k / settings.SineSteps;
            sumCos += value * Math.Cos(shift);
            sumSin += value * Math.Sin(shift);
            sum += value;
        }

        double amplitude = 2.0 * Math.Sqrt(sumCos * sumCos + sumSin * sumSin) / settings.SineSteps;
        if (amplitude < settings.MinModulation)
        {
            return MissingReason.LowSignal;
        }
        double threshold = sum / settings.SineSteps;
        double phase = Math.Atan2(-sumSin, sumCos);

        // Stripes: which half period is this?
        int gray = 0;
        for (int bit = 0; bit < settings.GrayBits; bit++)
        {
            gray = gray << 1 | (images[settings.SineSteps + bit][pixel] > threshold ? 1 : 0);
        }
        int stripe = PatternGenerator.FromGray(gray);

        // Choose the period that lies closest to the stripe. If they still disagree, the pixel cannot be trusted.
        double expected = Math.PI * (stripe + 0.5);
        double whole = phase + Math.Tau * Math.Round((expected - phase) / Math.Tau);
        if (Math.Abs(whole - expected) > settings.MaxPhaseDisagreement)
        {
            return MissingReason.Edge;
        }

        float position = (float)(whole / Math.Tau * settings.SinePeriod);
        z = projector.Depth(camera, pixel % camera.Width, position);
        return float.IsNaN(z) ? MissingReason.Edge : MissingReason.None;
    }
}
