using BinPicking.Core.Sensing;

namespace BinPicking.Core.Grasping;

/// <summary>
/// Checks a pick point against perfect depth. This ground truth exists only in simulation. It tests flatness
/// under the cup, not a real vacuum seal such as the contact model in Dex-Net 3.0.
/// </summary>
public static class SealVerifier
{
    /// <summary>Returns the share of the cup that lies within the seal tolerance of the chosen plane.</summary>
    public static float TrueSealShare(
        PinholeCamera camera, float[] idealDepth, GraspCandidate grasp, GraspSettings settings)
    {
        int radius = (int)MathF.Ceiling(camera.Fx * (settings.CupDiameter / 2f) / grasp.Z) + 1;
        float cupRadiusSquared = settings.CupDiameter * settings.CupDiameter / 4f;
        int total = 0, sealedCount = 0;
        for (int y = Math.Max(grasp.PixelY - radius, 0); y <= Math.Min(grasp.PixelY + radius, camera.Height - 1); y++)
        {
            for (int x = Math.Max(grasp.PixelX - radius, 0); x <= Math.Min(grasp.PixelX + radius, camera.Width - 1); x++)
            {
                float z = idealDepth[y * camera.Width + x];
                (float pointX, float pointY) = camera.Unproject(x, y, z);
                float dx = pointX - grasp.X, dy = pointY - grasp.Y, dz = z - grasp.Z;
                // Split the offset into a part along the normal and a part in the plane (Pythagoras).
                float along = dx * grasp.NormalX + dy * grasp.NormalY + dz * grasp.NormalZ;
                float inPlaneSquared = dx * dx + dy * dy + dz * dz - along * along;
                if (inPlaneSquared > cupRadiusSquared)
                {
                    continue;
                }
                total++;
                if (MathF.Abs(along) <= settings.SealTolerance)
                {
                    sealedCount++;
                }
            }
        }
        return total == 0 ? 0f : sealedCount / (float)total;
    }
}
