using BinPicking.Core.Sensing;

namespace BinPicking.Core.Grasping;

/// <summary>
/// Searches a depth image for pick points. <see cref="GraspEvaluator"/> judges each location; the
/// planner scans the image, counts the outcomes and ranks what remains.
/// </summary>
public static class GraspPlanner
{
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

    /// <summary>Best candidates first, at least one cup width apart.</summary>
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
