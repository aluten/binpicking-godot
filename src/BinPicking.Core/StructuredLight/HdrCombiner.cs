using BinPicking.Core.Sensing;

namespace BinPicking.Core.StructuredLight;

/// <summary>
/// Combines measurements of the same scene at different exposures into one, per pixel. A longer exposure collects more
/// light and so has less relative noise, as long as it does not clip. So each pixel takes the longest exposure that
/// gave a valid depth. Dark parts then come from the long exposure and bright parts from the short one.
/// <para>
/// This is one simple way. Real cameras may instead merge the images before decoding, or weigh the exposures;
/// how a specific camera does it is not described here.
/// </para>
/// </summary>
public static class HdrCombiner
{
    /// <param name="measurements">Decoded measurements of the same view, ordered from shortest to longest exposure.</param>
    /// <returns>Per pixel the longest valid exposure. Without a valid one, the reason of the longest exposure that
    /// did not overexpose, because that one says best why the pixel failed.</returns>
    public static DepthSensorResult Combine(IReadOnlyList<DepthSensorResult> measurements)
    {
        if (measurements.Count == 0)
        {
            throw new ArgumentException("At least one measurement is needed.");
        }

        int count = measurements[0].Depth.Length;
        if (measurements.Any(m => m.Depth.Length != count))
        {
            throw new ArgumentException("All measurements must have the same size.");
        }

        var depth = new float[count];
        var missing = new MissingReason[count];
        for (int i = 0; i < count; i++)
        {
            depth[i] = float.NaN;
            missing[i] = measurements[0].Missing[i];
            for (int e = measurements.Count - 1; e >= 0; e--)
            {
                if (measurements[e].Missing[i] == MissingReason.None)
                {
                    depth[i] = measurements[e].Depth[i];
                    missing[i] = MissingReason.None;
                    break;
                }
                if (measurements[e].Missing[i] != MissingReason.Overexposed)
                {
                    missing[i] = measurements[e].Missing[i];
                    break;
                }
            }
        }
        return new DepthSensorResult(depth, missing);
    }
}
