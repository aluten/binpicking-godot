using BinPicking.Core.Sensing;

namespace BinPicking.Core.Grasping;

/// <summary>
/// Judges whether a suction cup can pick at one location in measured depth. This is a geometric
/// heuristic built for this project; the thresholds are first choices. See docs/theory.md.
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

        // Requirement: enough measurements. Own choice, because black parts leave holes in the depth image.
        int discArea = CollectCupPoints(pixelX, pixelY, z);
        if (_points.Count < _settings.MinValidShare * discArea)
        {
            return GraspEvaluation.Rejected(GraspRejection.TooFewPoints);
        }

        // Requirement: flat under the cup, as a proxy for a seal. A plane-fit residual is also used by
        // Jiang et al. Dex-Net 3.0 instead checks the contact ring, which is more accurate.
        if (!PlaneFitter.TryFit(_points, _settings.PlaneTolerance, out Plane plane, out int inliers)
            || inliers < _settings.MinInlierShare * _points.Count)
        {
            return GraspEvaluation.Rejected(GraspRejection.NotFlat);
        }

        // Requirement: not too tilted. Own choice; a real limit depends on the robot and the cup.
        if (plane.NormalZTowardCamera < _minTiltCosine)
        {
            return GraspEvaluation.Rejected(GraspRejection.TooTilted);
        }

        float centerZ = plane.ZAt(centerX, centerY);

        // Requirement: near the middle of the flat surface, to limit tipping. Jiang et al. prefer central
        // points on this assumption; Dex-Net 3.0 models the forces, which this does not do.
        float centerOffset = SurfaceCenterOffset(pixelX, pixelY, centerX, centerY, centerZ, plane);
        if (centerOffset > _settings.MaxCenterOffset)
        {
            return GraspEvaluation.Rejected(GraspRejection.OffCenter);
        }

        // Requirement: room for the holder. A simple stand-in for the collision checks in Jiang et al.
        if (!IsToolClear(pixelX, pixelY, centerZ, plane))
        {
            return GraspEvaluation.Rejected(GraspRejection.ToolCollision);
        }

        // Requirement: reachable through the bin opening. Own choice; no robot kinematics are checked.
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
            validShare, inlierShare, tilt, centerOffset,
            Score(validShare, inlierShare, plane, centerZ, centerOffset)));
    }

    /// <summary>Own heuristic: prefers a well-measured, flat, facing, central and high location.</summary>
    private float Score(float validShare, float inlierShare, Plane plane, float z, float centerOffset)
    {
        float height = (_bin.FloorZ - z) / (_bin.FloorZ - _bin.RimZ);
        float centered = 1f - centerOffset / _settings.MaxCenterOffset;
        return validShare * inlierShare * plane.NormalZTowardCamera
            * (0.5f + 0.5f * height) * (0.5f + 0.5f * centered);
    }

    /// <summary>
    /// Distance from the cup to the centroid of the points in the same plane nearby. The depth image has no
    /// part labels, so this approximates the middle of the surface, not the true centre of mass.
    /// </summary>
    private float SurfaceCenterOffset(int pixelX, int pixelY, float x, float y, float z, Plane plane)
    {
        float pixelRadius = _camera.Fx * _settings.SurfaceSearchRadius / z;
        int r = (int)MathF.Ceiling(pixelRadius);
        double sumX = 0, sumY = 0, sumZ = 0;
        int count = 0;
        for (int py = Math.Max(pixelY - r, 0); py <= Math.Min(pixelY + r, _camera.Height - 1); py++)
        {
            for (int px = Math.Max(pixelX - r, 0); px <= Math.Min(pixelX + r, _camera.Width - 1); px++)
            {
                int dx = px - pixelX, dy = py - pixelY;
                float value = _depth[py * _camera.Width + px];
                if (dx * dx + dy * dy > pixelRadius * pixelRadius || float.IsNaN(value))
                {
                    continue;
                }
                (float pointX, float pointY) = _camera.Unproject(px, py, value);
                if (plane.Distance(pointX, pointY, value) <= _settings.PlaneTolerance)
                {
                    sumX += pointX;
                    sumY += pointY;
                    sumZ += value;
                    count++;
                }
            }
        }

        // The plane fit found inliers under the cup, so count is not zero here.
        float offsetX = (float)(sumX / count) - x;
        float offsetY = (float)(sumY / count) - y;
        float offsetZ = (float)(sumZ / count) - z;
        return MathF.Sqrt(offsetX * offsetX + offsetY * offsetY + offsetZ * offsetZ);
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
