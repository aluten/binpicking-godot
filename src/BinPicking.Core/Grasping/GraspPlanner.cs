using BinPicking.Core.Sensing;

namespace BinPicking.Core.Grasping;

/// <summary>De bak in het camerastelsel: X naar rechts, Y omlaag in beeld, Z van de camera af.</summary>
public readonly record struct BinRegion(float MinX, float MaxX, float MinY, float MaxY, float RimZ, float FloorZ);

/// <summary>Pickpunt in het camerastelsel. De normaal wijst naar de camera.</summary>
public sealed record GraspCandidate(
    int PixelX,
    int PixelY,
    float X,
    float Y,
    float Z,
    float NormalX,
    float NormalY,
    float NormalZ,
    float ValidShare,
    float InlierShare,
    float TiltDegrees,
    float Score);

/// <summary>Telt waarom kandidaten afvielen. Handig om de pipeline uit te leggen.</summary>
public sealed record GraspSearchReport(
    int Evaluated,
    int TooFewPoints,
    int NotFlat,
    int TooTilted,
    int ToolCollision,
    int OutsideOpening,
    int Accepted);

/// <summary>
/// Zoekt in gemeten diepte naar plekken waar een zuignap kan afdichten: een vlak stuk, met genoeg
/// metingen, niet te schuin, met ruimte voor de houder, en bereikbaar door de opening van de bak.
/// </summary>
public static class GraspPlanner
{
    public static (List<GraspCandidate> Candidates, GraspSearchReport Report) FindGrasps(
        PinholeCamera camera, float[] depth, BinRegion bin, GraspSettings settings)
    {
        if (depth.Length != camera.Width * camera.Height)
        {
            throw new ArgumentException("De diepte moet even groot zijn als de camera.");
        }

        var found = new List<GraspCandidate>();
        int evaluated = 0, tooFew = 0, notFlat = 0, tooTilted = 0, collision = 0, outside = 0;
        float maxTiltCos = MathF.Cos(settings.MaxTiltDegrees * MathF.PI / 180f);
        var xs = new List<float>();
        var ys = new List<float>();
        var zs = new List<float>();

        for (int py = 0; py < camera.Height; py += settings.SearchStep)
        {
            for (int px = 0; px < camera.Width; px += settings.SearchStep)
            {
                float z = depth[py * camera.Width + px];
                if (float.IsNaN(z) || z < bin.RimZ || z > bin.FloorZ)
                {
                    continue;
                }

                (float centerX, float centerY) = Unproject(camera, px, py, z);
                if (centerX < bin.MinX || centerX > bin.MaxX || centerY < bin.MinY || centerY > bin.MaxY)
                {
                    continue;
                }

                evaluated++;
                int cupPixels = CollectDisc(camera, depth, px, py, z, settings.CupDiameter / 2f, xs, ys, zs, out int discArea);
                if (cupPixels < settings.MinValidShare * discArea)
                {
                    tooFew++;
                    continue;
                }

                if (!FitPlane(xs, ys, zs, settings.PlaneTolerance, out Plane plane, out int inliers)
                    || inliers < settings.MinInlierShare * cupPixels)
                {
                    notFlat++;
                    continue;
                }

                if (plane.NormalZTowardCamera < maxTiltCos)
                {
                    tooTilted++;
                    continue;
                }

                float centerZ = plane.ZAt(centerX, centerY);
                if (!ToolIsClear(camera, depth, px, py, centerZ, plane, settings))
                {
                    collision++;
                    continue;
                }

                if (!ReachesThroughOpening(centerX, centerY, centerZ, plane, bin, settings))
                {
                    outside++;
                    continue;
                }

                float validShare = cupPixels / (float)discArea;
                float inlierShare = inliers / (float)cupPixels;
                float heightScore = (bin.FloorZ - centerZ) / (bin.FloorZ - bin.RimZ);
                float score = validShare * inlierShare * plane.NormalZTowardCamera * (0.5f + 0.5f * heightScore);
                float tilt = MathF.Acos(Math.Clamp(plane.NormalZTowardCamera, 0f, 1f)) * 180f / MathF.PI;
                found.Add(new GraspCandidate(
                    px, py, centerX, centerY, centerZ,
                    plane.NormalX, plane.NormalY, -plane.NormalZTowardCamera,
                    validShare, inlierShare, tilt, score));
            }
        }

        List<GraspCandidate> best = KeepSeparated(found, settings);
        return (best, new GraspSearchReport(evaluated, tooFew, notFlat, tooTilted, collision, outside, found.Count));
    }

    /// <summary>
    /// Controle met perfecte diepte: welk deel van de nap ligt echt binnen de tolerantie van het gekozen vlak?
    /// Dit kan alleen in simulatie.
    /// </summary>
    public static float TrueSealShare(
        PinholeCamera camera, float[] idealDepth, GraspCandidate grasp, GraspSettings settings)
    {
        int radius = (int)MathF.Ceiling(camera.Fx * (settings.CupDiameter / 2f) / grasp.Z) + 1;
        float cupRadiusSquared = settings.CupDiameter * settings.CupDiameter / 4f;
        int total = 0, sealed_ = 0;
        for (int y = Math.Max(grasp.PixelY - radius, 0); y <= Math.Min(grasp.PixelY + radius, camera.Height - 1); y++)
        {
            for (int x = Math.Max(grasp.PixelX - radius, 0); x <= Math.Min(grasp.PixelX + radius, camera.Width - 1); x++)
            {
                float z = idealDepth[y * camera.Width + x];
                (float pointX, float pointY) = Unproject(camera, x, y, z);
                float dx = pointX - grasp.X, dy = pointY - grasp.Y, dz = z - grasp.Z;
                float along = dx * grasp.NormalX + dy * grasp.NormalY + dz * grasp.NormalZ;
                float inPlaneSquared = dx * dx + dy * dy + dz * dz - along * along;
                if (inPlaneSquared > cupRadiusSquared)
                {
                    continue;
                }
                total++;
                if (MathF.Abs(along) <= settings.PlaneTolerance)
                {
                    sealed_++;
                }
            }
        }
        return total == 0 ? 0f : sealed_ / (float)total;
    }

    private static (float X, float Y) Unproject(PinholeCamera camera, int x, int y, float z) =>
        ((x + 0.5f - camera.Cx) * z / camera.Fx, (y + 0.5f - camera.Cy) * z / camera.Fy);

    private static int CollectDisc(
        PinholeCamera camera, float[] depth, int px, int py, float z, float radius,
        List<float> xs, List<float> ys, List<float> zs, out int discArea)
    {
        xs.Clear();
        ys.Clear();
        zs.Clear();
        discArea = 0;
        float pixelRadius = camera.Fx * radius / z;
        int r = (int)MathF.Ceiling(pixelRadius);
        for (int y = py - r; y <= py + r; y++)
        {
            for (int x = px - r; x <= px + r; x++)
            {
                int dx = x - px, dy = y - py;
                if (dx * dx + dy * dy > pixelRadius * pixelRadius)
                {
                    continue;
                }
                discArea++;
                if (x < 0 || y < 0 || x >= camera.Width || y >= camera.Height)
                {
                    continue;
                }
                float value = depth[y * camera.Width + x];
                if (!float.IsNaN(value))
                {
                    (float pointX, float pointY) = Unproject(camera, x, y, value);
                    xs.Add(pointX);
                    ys.Add(pointY);
                    zs.Add(value);
                }
            }
        }
        return xs.Count;
    }

    private readonly record struct Plane(float A, float B, float C)
    {
        public float ZAt(float x, float y) => A * x + B * y + C;

        private float Length => MathF.Sqrt(A * A + B * B + 1f);

        public float NormalX => A / Length;

        public float NormalY => B / Length;

        /// <summary>Component richting de camera; 1 betekent recht naar de camera gericht.</summary>
        public float NormalZTowardCamera => 1f / Length;

        public float Distance(float x, float y, float z) => MathF.Abs(ZAt(x, y) - z) / Length;
    }

    /// <summary>Kleinste-kwadratenfit, herhaald zonder uitschieters.</summary>
    private static bool FitPlane(
        List<float> xs, List<float> ys, List<float> zs, float tolerance, out Plane plane, out int inliers)
    {
        plane = default;
        inliers = 0;
        float limit = tolerance * 4f;
        for (int round = 0; round < 3; round++)
        {
            bool first = round == 0;
            if (!Solve(xs, ys, zs, first ? null : plane, limit, out Plane next))
            {
                return false;
            }
            plane = next;
            limit = MathF.Max(limit / 2f, tolerance);
        }

        for (int i = 0; i < xs.Count; i++)
        {
            if (plane.Distance(xs[i], ys[i], zs[i]) <= tolerance)
            {
                inliers++;
            }
        }
        return true;
    }

    private static bool Solve(
        List<float> xs, List<float> ys, List<float> zs, Plane? previous, float limit, out Plane plane)
    {
        plane = default;
        double sxx = 0, sxy = 0, sx = 0, syy = 0, sy = 0, n = 0, sxz = 0, syz = 0, sz = 0;
        for (int i = 0; i < xs.Count; i++)
        {
            if (previous is { } p && p.Distance(xs[i], ys[i], zs[i]) > limit)
            {
                continue;
            }
            double x = xs[i], y = ys[i], z = zs[i];
            sxx += x * x;
            sxy += x * y;
            sx += x;
            syy += y * y;
            sy += y;
            n += 1;
            sxz += x * z;
            syz += y * z;
            sz += z;
        }

        double m00 = syy * n - sy * sy, m01 = sxy * n - sy * sx, m02 = sxy * sy - syy * sx;
        double determinant = sxx * m00 - sxy * m01 + sx * m02;
        if (n < 3 || Math.Abs(determinant) < 1e-18)
        {
            return false;
        }

        double a = (sxz * m00 - sxy * (syz * n - sy * sz) + sx * (syz * sy - syy * sz)) / determinant;
        double b = (sxx * (syz * n - sz * sy) - sxz * m01 + sx * (sxy * sz - syz * sx)) / determinant;
        double c = (sxx * (syy * sz - sy * syz) - sxy * (sxy * sz - sx * syz) + sxz * m02) / determinant;
        plane = new Plane((float)a, (float)b, (float)c);
        return true;
    }

    private static bool ToolIsClear(
        PinholeCamera camera, float[] depth, int px, int py, float z, Plane plane, GraspSettings settings)
    {
        float toolRadius = settings.ToolDiameter / 2f;
        float pixelRadius = camera.Fx * toolRadius / z;
        int r = (int)MathF.Ceiling(pixelRadius);
        for (int y = py - r; y <= py + r; y++)
        {
            for (int x = px - r; x <= px + r; x++)
            {
                int dx = x - px, dy = y - py;
                if (dx * dx + dy * dy > pixelRadius * pixelRadius
                    || x < 0 || y < 0 || x >= camera.Width || y >= camera.Height)
                {
                    continue;
                }
                float value = depth[y * camera.Width + x];
                if (float.IsNaN(value))
                {
                    continue;
                }
                (float pointX, float pointY) = Unproject(camera, x, y, value);
                // Kleinere z is dichter bij de camera, dus boven het vlak.
                if (plane.ZAt(pointX, pointY) - value > settings.ToolClearance)
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>De houder komt langs de normaal binnen en moet binnen de opening van de bak blijven.</summary>
    private static bool ReachesThroughOpening(
        float x, float y, float z, Plane plane, BinRegion bin, GraspSettings settings)
    {
        float toTop = z - bin.RimZ;
        float slope = 1f / plane.NormalZTowardCamera;
        float rimX = x + plane.NormalX * toTop * slope;
        float rimY = y + plane.NormalY * toTop * slope;
        float margin = settings.ToolDiameter / 2f;
        return Min(x, rimX) - margin >= bin.MinX && Max(x, rimX) + margin <= bin.MaxX
            && Min(y, rimY) - margin >= bin.MinY && Max(y, rimY) + margin <= bin.MaxY;

        static float Min(float a, float b) => MathF.Min(a, b);
        static float Max(float a, float b) => MathF.Max(a, b);
    }

    /// <summary>Beste kandidaten eerst, met minstens een napbreedte onderlinge afstand.</summary>
    private static List<GraspCandidate> KeepSeparated(List<GraspCandidate> found, GraspSettings settings)
    {
        var result = new List<GraspCandidate>();
        foreach (GraspCandidate candidate in found.OrderByDescending(c => c.Score))
        {
            bool free = result.All(other =>
            {
                float dx = other.X - candidate.X, dy = other.Y - candidate.Y, dz = other.Z - candidate.Z;
                return dx * dx + dy * dy + dz * dz >= settings.CupDiameter * settings.CupDiameter;
            });
            if (free)
            {
                result.Add(candidate);
                if (result.Count >= settings.MaxCandidates)
                {
                    break;
                }
            }
        }
        return result;
    }
}
