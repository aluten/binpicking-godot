using BinPicking.Core.Sensing;

namespace BinPicking.Core.Grasping;

/// <summary>
/// Searches a depth image for pick points. <see cref="GraspEvaluator"/> judges each location; the
/// planner scans the image, counts the outcomes and ranks what remains.
/// <para>
/// This is the seam for a real camera: the search only needs a camera model, depth in metres with NaN for a
/// hole, and the bin. It knows nothing about Godot or about where the depth comes from.
/// </para>
/// </summary>
public static class GraspPlanner
{
    /// <param name="camera">The camera that made the depth image.</param>
    /// <param name="depth">Depth per pixel in metres, with NaN for a hole.</param>
    /// <param name="bin">The volume in which picking is allowed.</param>
    /// <param name="settings">The requirements and the search step.</param>
    /// <returns>The best candidates, best first, and how many locations failed each requirement.</returns>
    public static (List<GraspCandidate> Candidates, GraspSearchReport Report) FindGrasps(
        PinholeCamera camera, float[] depth, BinRegion bin, GraspSettings settings)
    {
        var evaluator = new GraspEvaluator(camera, depth, bin, settings);
        var accepted = new List<GraspCandidate>();
        int evaluated = 0, tooFew = 0, notFlat = 0, tooTilted = 0, offCenter = 0, collision = 0, outside = 0;

        for (int y = 0; y < camera.Height; y += settings.SearchStep)
        {
            for (int x = 0; x < camera.Width; x += settings.SearchStep)
            {
                if (!evaluator.IsInsideBin(x, y))
                {
                    continue;
                }

                evaluated++;
                GraspEvaluation evaluation = evaluator.Evaluate(x, y);
                if (evaluation.Candidate != null)
                {
                    accepted.Add(evaluation.Candidate);
                    continue;
                }

                switch (evaluation.Rejection)
                {
                    case GraspRejection.TooFewPoints:
                        tooFew++;
                        break;
                    case GraspRejection.NotFlat:
                        notFlat++;
                        break;
                    case GraspRejection.TooTilted:
                        tooTilted++;
                        break;
                    case GraspRejection.OffCenter:
                        offCenter++;
                        break;
                    case GraspRejection.ToolCollision:
                        collision++;
                        break;
                    case GraspRejection.OutsideOpening:
                        outside++;
                        break;
                }
            }
        }

        var report = new GraspSearchReport(evaluated, tooFew, notFlat, tooTilted, offCenter, collision, outside, accepted.Count);
        return (KeepSeparated(accepted, settings), report);
    }

    /// <summary>
    /// Best candidates first, at least one cup width apart. Otherwise the list would hold the same flat
    /// face many times, leaving no real alternative when a pick fails.
    /// </summary>
    private static List<GraspCandidate> KeepSeparated(List<GraspCandidate> accepted, GraspSettings settings)
    {
        var result = new List<GraspCandidate>();
        float minimumSquared = settings.CupDiameter * settings.CupDiameter;
        foreach (GraspCandidate candidate in accepted.OrderByDescending(c => c.Score))
        {
            bool free = result.All(other =>
            {
                float dx = other.X - candidate.X, dy = other.Y - candidate.Y, dz = other.Z - candidate.Z;
                return dx * dx + dy * dy + dz * dz >= minimumSquared;
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
