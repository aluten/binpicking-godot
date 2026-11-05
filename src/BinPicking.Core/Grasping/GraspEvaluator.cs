using BinPicking.Core.Sensing;

namespace BinPicking.Core.Grasping;

/// <summary>
/// Judges whether a suction cup can seal at one location in measured depth: enough points, flat,
/// not too tilted, room for the holder, and reachable through the bin opening.
/// An instance belongs to one depth image and is not thread-safe.
/// </summary>
public sealed class GraspEvaluator
{
    private readonly PinholeCamera _camera;
    private readonly float[] _depth;
    private readonly BinRegion _bin;
    private readonly GraspSettings _settings;
    private readonly float _minTiltCosine;
    private readonly PointSet _points = new();

    public GraspEvaluator(PinholeCamera camera, float[] depth, BinRegion bin, GraspSettings settings)
    {
        if (depth.Length != camera.Width * camera.Height)
        {
            throw new ArgumentException("The depth image must match the camera size.");
        }

        _camera = camera;
        _depth = depth;
        _bin = bin;
        _settings = settings;
        _minTiltCosine = MathF.Cos(settings.MaxTiltDegrees * MathF.PI / 180f);
    }

    /// <summary>Tells whether a pixel has a measurement inside the bin, which makes it worth evaluating.</summary>
    public bool IsInsideBin(int pixelX, int pixelY)
    {
        float z = _depth[pixelY * _camera.Width + pixelX];
        if (float.IsNaN(z) || z < _bin.RimZ || z > _bin.FloorZ)
        {
            return false;
        }

        (float x, float y) = _camera.Unproject(pixelX, pixelY, z);
        return x >= _bin.MinX && x <= _bin.MaxX && y >= _bin.MinY && y <= _bin.MaxY;
    }

    /// <summary>Evaluates a pixel for which <see cref="IsInsideBin"/> is true.</summary>
    public GraspEvaluation Evaluate(int pixelX, int pixelY)
    {
        float z = _depth[pixelY * _camera.Width + pixelX];
        (float centerX, float centerY) = _camera.Unproject(pixelX, pixelY, z);

        int discArea = CollectCupPoints(pixelX, pixelY, z);
        if (_points.Count < _settings.MinValidShare * discArea)
        {
            return GraspEvaluation.Rejected(GraspRejection.TooFewPoints);
        }

        if (!PlaneFitter.TryFit(_points, _settings.PlaneTolerance, out Plane plane, out int inliers)
            || inliers < _settings.MinInlierShare * _points.Count)
        {
            return GraspEvaluation.Rejected(GraspRejection.NotFlat);
        }

        if (plane.NormalZTowardCamera < _minTiltCosine)
        {
            return GraspEvaluation.Rejected(GraspRejection.TooTilted);
        }

        float centerZ = plane.ZAt(centerX, centerY);
        if (!IsToolClear(pixelX, pixelY, centerZ, plane))
        {
            return GraspEvaluation.Rejected(GraspRejection.ToolCollision);
        }

        if (!ReachesThroughOpening(centerX, centerY, centerZ, plane))
        {
            return GraspEvaluation.Rejected(GraspRejection.OutsideOpening);
        }

        float validShare = _points.Count / (float)discArea;
        float inlierShare = inliers / (float)_points.Count;
        float tilt = MathF.Acos(Math.Clamp(plane.NormalZTowardCamera, 0f, 1f)) * 180f / MathF.PI;
        return GraspEvaluation.Accepted(new GraspCandidate(
            pixelX, pixelY, centerX, centerY, centerZ,
            plane.NormalX, plane.NormalY, -plane.NormalZTowardCamera,
            validShare, inlierShare, tilt, Score(validShare, inlierShare, plane, centerZ)));
    }

    private float Score(float validShare, float inlierShare, Plane plane, float z)
    {
        float height = (_bin.FloorZ - z) / (_bin.FloorZ - _bin.RimZ);
        return validShare * inlierShare * plane.NormalZTowardCamera * (0.5f + 0.5f * height);
    }

    /// <summary>Collects the measured points under the cup and returns the cup area in pixels.</summary>
    private int CollectCupPoints(int pixelX, int pixelY, float z)
    {
        _points.Clear();
        int discArea = 0;
        float pixelRadius = _camera.Fx * (_settings.CupDiameter / 2f) / z;
        int r = (int)MathF.Ceiling(pixelRadius);
        for (int y = pixelY - r; y <= pixelY + r; y++)
        {
            for (int x = pixelX - r; x <= pixelX + r; x++)
            {
                int dx = x - pixelX, dy = y - pixelY;
                if (dx * dx + dy * dy > pixelRadius * pixelRadius)
                {
                    continue;
                }
                discArea++;
                if (x < 0 || y < 0 || x >= _camera.Width || y >= _camera.Height)
                {
                    continue;
                }
                float value = _depth[y * _camera.Width + x];
                if (!float.IsNaN(value))
                {
                    (float pointX, float pointY) = _camera.Unproject(x, y, value);
                    _points.Add(pointX, pointY, value);
                }
            }
        }
        return discArea;
    }

    private bool IsToolClear(int pixelX, int pixelY, float z, Plane plane)
    {
        float pixelRadius = _camera.Fx * (_settings.ToolDiameter / 2f) / z;
        int r = (int)MathF.Ceiling(pixelRadius);
        for (int y = pixelY - r; y <= pixelY + r; y++)
        {
            for (int x = pixelX - r; x <= pixelX + r; x++)
            {
                int dx = x - pixelX, dy = y - pixelY;
                if (dx * dx + dy * dy > pixelRadius * pixelRadius
                    || x < 0 || y < 0 || x >= _camera.Width || y >= _camera.Height)
                {
                    continue;
                }
                float value = _depth[y * _camera.Width + x];
                if (float.IsNaN(value))
                {
                    continue;
                }
                (float pointX, float pointY) = _camera.Unproject(x, y, value);
                // A smaller z is closer to the camera, and therefore above the plane.
                if (plane.ZAt(pointX, pointY) - value > _settings.ToolClearance)
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>The holder approaches along the normal and must stay inside the bin opening.</summary>
    private bool ReachesThroughOpening(float x, float y, float z, Plane plane)
    {
        float toTop = z - _bin.RimZ;
        float slope = 1f / plane.NormalZTowardCamera;
        float rimX = x + plane.NormalX * toTop * slope;
        float rimY = y + plane.NormalY * toTop * slope;
        float margin = _settings.ToolDiameter / 2f;
        return MathF.Min(x, rimX) - margin >= _bin.MinX && MathF.Max(x, rimX) + margin <= _bin.MaxX
            && MathF.Min(y, rimY) - margin >= _bin.MinY && MathF.Max(y, rimY) + margin <= _bin.MaxY;
    }
}
