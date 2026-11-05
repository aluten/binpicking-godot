namespace BinPicking.Core.Sensing;

/// <summary>
/// Turns perfect depth into roughly what a structured-light camera would measure:
/// holes from low returned light, projector shadow, errors along edges, and noise.
/// </summary>
public static class DepthSensorModel
{
    /// <param name="camera">The camera that produced the perfect depth.</param>
    /// <param name="idealDepth">Depth per pixel along the optical axis, in metres.</param>
    /// <param name="projectorDepth">The same scene from the projector, shifted in parallel.</param>
    /// <param name="reflectance">Share of light returned per pixel, from 0 to 1.</param>
    public static DepthSensorResult Apply(
        PinholeCamera camera,
        float[] idealDepth,
        float[] projectorDepth,
        float[] reflectance,
        DepthSensorSettings settings)
    {
        int count = camera.Width * camera.Height;
        if (idealDepth.Length != count || projectorDepth.Length != count || reflectance.Length != count)
        {
            throw new ArgumentException("All images must match the camera size.");
        }

        var depth = new float[count];
        var missing = new MissingReason[count];
        var pixelRandom = new Random(settings.Seed);
        float[] clusters = MakeClusters(camera, settings);

        for (int y = 0; y < camera.Height; y++)
        {
            for (int x = 0; x < camera.Width; x++)
            {
                int i = y * camera.Width + x;
                float z = idealDepth[i];

                // The draw order is fixed so that the same seed gives the same image.
                double edgeRoll = pixelRandom.NextDouble();
                double signalRoll = pixelRandom.NextDouble();
                double noiseRollA = pixelRandom.NextDouble();
                double noiseRollB = pixelRandom.NextDouble();

                if (IsShadowed(camera, projectorDepth, x, y, z, settings))
                {
                    missing[i] = MissingReason.Shadow;
                }
                else if (IsEdge(camera, idealDepth, x, y, settings.EdgeJump) && edgeRoll < settings.EdgeMissingChance)
                {
                    missing[i] = MissingReason.Edge;
                }
                else
                {
                    float signal = Signal(camera, idealDepth, reflectance[i], x, y, settings);
                    float roll = 0.5f * (float)signalRoll + 0.5f * clusters[i];
                    if (roll < MissingChance(signal, settings))
                    {
                        missing[i] = MissingReason.LowSignal;
                    }
                    else
                    {
                        depth[i] = z + Noise(signal, z, settings) * Gaussian(noiseRollA, noiseRollB);
                        continue;
                    }
                }

                depth[i] = float.NaN;
            }
        }

        return new DepthSensorResult(depth, missing);
    }

    /// <summary>How much light returns: less for black, oblique and distant surfaces.</summary>
    private static float Signal(
        PinholeCamera camera, float[] depth, float reflectance, int x, int y, DepthSensorSettings settings)
    {
        float z = depth[y * camera.Width + x];
        float distance = settings.ReferenceDistance / z;
        return reflectance * Incidence(camera, depth, x, y) * distance * distance;
    }

    /// <summary>Cosine of the angle between surface and view ray, derived from the depth itself.</summary>
    private static float Incidence(PinholeCamera camera, float[] depth, int x, int y)
    {
        if (x <= 0 || y <= 0 || x >= camera.Width - 1 || y >= camera.Height - 1)
        {
            return 1f;
        }

        var (ax, ay, az) = Point(camera, depth, x - 1, y);
        var (bx, by, bz) = Point(camera, depth, x + 1, y);
        var (cx, cy, cz) = Point(camera, depth, x, y - 1);
        var (dx, dy, dz) = Point(camera, depth, x, y + 1);

        float ux = bx - ax, uy = by - ay, uz = bz - az;
        float vx = dx - cx, vy = dy - cy, vz = dz - cz;
        float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
        float normalLength = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
        if (normalLength < 1e-12f)
        {
            return 1f;
        }

        var (px, py, pz) = Point(camera, depth, x, y);
        float rayLength = MathF.Sqrt(px * px + py * py + pz * pz);
        return MathF.Abs(nx * px + ny * py + nz * pz) / (normalLength * rayLength);
    }

    private static (float X, float Y, float Z) Point(PinholeCamera camera, float[] depth, int x, int y)
    {
        float z = depth[y * camera.Width + x];
        return ((x + 0.5f - camera.Cx) * z / camera.Fx, (y + 0.5f - camera.Cy) * z / camera.Fy, z);
    }

    private static float MissingChance(float signal, DepthSensorSettings settings)
    {
        float t = (signal - settings.SignalAlwaysMissing) / (settings.SignalAlwaysValid - settings.SignalAlwaysMissing);
        t = Math.Clamp(t, 0f, 1f);
        return 1f - t * t * (3f - 2f * t);
    }

    private static float Noise(float signal, float z, DepthSensorSettings settings)
    {
        float distance = z / settings.ReferenceDistance;
        float signalFactor = MathF.Sqrt(settings.NoiseReferenceSignal / MathF.Max(signal, 1e-4f));
        float noise = settings.BaseNoise * distance * distance * MathF.Max(signalFactor, 1f);
        return MathF.Min(noise, settings.MaxNoise);
    }

    private static bool IsShadowed(
        PinholeCamera camera, float[] projectorDepth, int x, int y, float z, DepthSensorSettings settings)
    {
        // The same point, seen from the projector that sits Baseline towards +X.
        float pointX = (x + 0.5f - camera.Cx) * z / camera.Fx;
        int projectorX = (int)MathF.Floor(camera.Cx + camera.Fx * (pointX - settings.ProjectorBaseline) / z);
        if (projectorX < 0 || projectorX >= camera.Width)
        {
            return false;
        }
        return projectorDepth[y * camera.Width + projectorX] < z - settings.ShadowTolerance;
    }

    private static bool IsEdge(PinholeCamera camera, float[] depth, int x, int y, float jump)
    {
        float z = depth[y * camera.Width + x];
        return (x > 0 && MathF.Abs(depth[y * camera.Width + x - 1] - z) > jump)
            || (x < camera.Width - 1 && MathF.Abs(depth[y * camera.Width + x + 1] - z) > jump)
            || (y > 0 && MathF.Abs(depth[(y - 1) * camera.Width + x] - z) > jump)
            || (y < camera.Height - 1 && MathF.Abs(depth[(y + 1) * camera.Width + x] - z) > jump);
    }

    private static float[] MakeClusters(PinholeCamera camera, DepthSensorSettings settings)
    {
        int size = Math.Max(settings.ClusterSize, 1);
        int columns = camera.Width / size + 1;
        int rows = camera.Height / size + 1;
        var random = new Random(settings.Seed + 1);
        var blocks = new float[columns * rows];
        for (int i = 0; i < blocks.Length; i++)
        {
            blocks[i] = (float)random.NextDouble();
        }

        var clusters = new float[camera.Width * camera.Height];
        for (int y = 0; y < camera.Height; y++)
        {
            for (int x = 0; x < camera.Width; x++)
            {
                clusters[y * camera.Width + x] = blocks[y / size * columns + x / size];
            }
        }
        return clusters;
    }

    private static float Gaussian(double a, double b)
    {
        return (float)(Math.Sqrt(-2.0 * Math.Log(1.0 - a)) * Math.Cos(2.0 * Math.PI * b));
    }
}
