namespace BinPicking.Core.Sensing;

/// <summary>Summary of a measurement, split into pixels on the parts and all other pixels.</summary>
public sealed record DepthSensorStatistics(
    int PartPixels,
    int PartLowSignal,
    int PartShadow,
    int PartEdge,
    int OtherPixels,
    int OtherMissing,
    double PartNoiseMeters,
    int PartOverexposed = 0)
{
    /// <summary>Pixels on parts without a measurement, whatever the reason.</summary>
    public int PartMissing => PartLowSignal + PartShadow + PartEdge + PartOverexposed;

    /// <summary>Counts the holes per reason and computes the depth error against perfect depth.</summary>
    /// <param name="result">The measurement.</param>
    /// <param name="idealDepth">Perfect depth, which exists only in simulation.</param>
    /// <param name="isPart">Tells whether a pixel index lies on a part.</param>
    public static DepthSensorStatistics Compute(DepthSensorResult result, float[] idealDepth, Func<int, bool> isPart)
    {
        int parts = 0, low = 0, shadow = 0, edge = 0, over = 0, other = 0, otherMissing = 0, measured = 0;
        double squaredError = 0;
        for (int i = 0; i < result.Depth.Length; i++)
        {
            if (!isPart(i))
            {
                other++;
                if (result.Missing[i] != MissingReason.None)
                {
                    otherMissing++;
                }
                continue;
            }

            parts++;
            switch (result.Missing[i])
            {
                case MissingReason.LowSignal:
                    low++;
                    break;
                case MissingReason.Shadow:
                    shadow++;
                    break;
                case MissingReason.Edge:
                    edge++;
                    break;
                case MissingReason.Overexposed:
                    over++;
                    break;
                default:
                    double error = result.Depth[i] - idealDepth[i];
                    squaredError += error * error;
                    measured++;
                    break;
            }
        }

        double noise = measured == 0 ? 0.0 : Math.Sqrt(squaredError / measured);
        return new DepthSensorStatistics(parts, low, shadow, edge, other, otherMissing, noise, over);
    }
}
