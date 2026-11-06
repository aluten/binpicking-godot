namespace BinPicking.Core.Grasping;

/// <summary>
/// Fits a plane with least squares, repeating the fit without outliers. The first round uses every point, and
/// later rounds drop points that lie far from the previous plane, so a piece of a neighbouring link under the
/// cup does not tilt the result. A deterministic alternative to RANSAC.
/// </summary>
internal static class PlaneFitter
{
    private const int Rounds = 3;

    /// <summary>Start of the outlier limit, as a multiple of the tolerance. It halves after each round, down to the tolerance.</summary>
    private const float InitialLimitFactor = 4f;

    /// <param name="points">The measured points under the cup.</param>
    /// <param name="tolerance">A point counts as an inlier within this distance of the plane, in metres.</param>
    /// <param name="plane">The fitted plane.</param>
    /// <param name="inliers">The number of points within the tolerance.</param>
    /// <returns>False when the points do not define a plane, for example when they lie on a line.</returns>
    public static bool TryFit(PointSet points, float tolerance, out Plane plane, out int inliers)
    {
        plane = default;
        inliers = 0;
        float limit = tolerance * InitialLimitFactor;
        for (int round = 0; round < Rounds; round++)
        {
            bool first = round == 0;
            if (!TrySolve(points, first ? null : plane, limit, out Plane next))
            {
                return false;
            }
            plane = next;
            limit = MathF.Max(limit / 2f, tolerance);
        }

        for (int i = 0; i < points.Count; i++)
        {
            if (plane.Distance(points.Xs[i], points.Ys[i], points.Zs[i]) <= tolerance)
            {
                inliers++;
            }
        }
        return true;
    }

    /// <summary>
    /// Solves the normal equations of z = A x + B y + C with Cramer's rule, which is the 3 x 3 system below:
    /// <code>
    /// | sxx sxy sx | | A |   | sxz |
    /// | sxy syy sy | | B | = | syz |
    /// | sx  sy  n  | | C |   | sz  |
    /// </code>
    /// </summary>
    private static bool TrySolve(PointSet points, Plane? previous, float limit, out Plane plane)
    {
        plane = default;
        double sxx = 0, sxy = 0, sx = 0, syy = 0, sy = 0, n = 0, sxz = 0, syz = 0, sz = 0;
        for (int i = 0; i < points.Count; i++)
        {
            if (previous is { } p && p.Distance(points.Xs[i], points.Ys[i], points.Zs[i]) > limit)
            {
                continue;
            }
            double x = points.Xs[i], y = points.Ys[i], z = points.Zs[i];
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

        // m00, m01 and m02 are the cofactors of the first row, shared by the determinant and by A and B.
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
}
