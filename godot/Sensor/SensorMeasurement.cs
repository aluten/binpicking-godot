#nullable enable

using System.Threading.Tasks;
using BinPicking.Core.Sensing;
using Godot;
using static PalletDimensions;

/// <summary>
/// Captures what the sensor sees and turns it into a realistic measurement. The rest of the demo only uses
/// <see cref="Camera"/>, <see cref="Result"/> and, for verification, <see cref="IdealDepth"/>. So this is the
/// class that a real camera would replace; see docs/demo-questions.md.
/// </summary>
public sealed class SensorMeasurement
{
    private readonly DepthSensorSettings _settings = new();
    private readonly DepthSensorRig _rig;
    private float[] _projectorDepth = System.Array.Empty<float>();
    private float[] _reflectance = System.Array.Empty<float>();
    private bool[] _isPart = System.Array.Empty<bool>();

    /// <summary>Hangs the rig above the pallet, at the position of the sensor mount.</summary>
    public SensorMeasurement(DepthSensorRig rig)
    {
        _rig = rig;
        _rig.Configure(SensorMount.Position, SensorMount.VerticalFovDegrees, _settings.ProjectorBaseline);
    }

    /// <summary>The camera model that belongs to the measurement.</summary>
    public PinholeCamera Camera => _rig.Camera;

    /// <summary>Perfect depth in metres. This exists only in simulation, and the search never uses it.</summary>
    public float[] IdealDepth { get; private set; } = System.Array.Empty<float>();

    /// <summary>The measured depth, with NaN for a hole. Null until the first measurement.</summary>
    public DepthSensorResult? Result { get; private set; }

    /// <summary>One line about the measurement, for on screen.</summary>
    public string Summary { get; private set; } = string.Empty;

    /// <summary>Captures the scene as it is now, including the colour of the links, and applies the sensor model.</summary>
    /// <param name="partName">How to call the links in the summary.</param>
    public async Task MeasureAsync(string partName)
    {
        (IdealDepth, _projectorDepth, _reflectance, _isPart) = await _rig.CaptureAsync();
        CheckRimDistance();
        Result = DepthSensorModel.Apply(Camera, IdealDepth, _projectorDepth, _reflectance, _settings);
        Report(Result, partName);
    }

    /// <summary>The top of the rim has a known height, which verifies the whole chain.</summary>
    private void CheckRimDistance()
    {
        float rimX = InnerLength / 2f + PlankThickness / 2f;
        int x = Mathf.RoundToInt(Camera.Cx + Camera.Fx * rimX / SensorMount.AboveRim);
        float measured = IdealDepth[Camera.Height / 2 * Camera.Width + x];
        bool passed = Mathf.Abs(measured - SensorMount.AboveRim) <= 0.005f;
        GD.Print($"{(passed ? "OK  " : "FOUT")} Afstand tot de rand: {measured * 1000f:F1} mm, " +
                 $"verwacht {SensorMount.AboveRim * 1000f:F0} mm");
    }

    /// <summary>Counts the holes on the links, for on screen and for the Output panel.</summary>
    private void Report(DepthSensorResult result, string partName)
    {
        DepthSensorStatistics stats = DepthSensorStatistics.Compute(
            result, IdealDepth, i => _isPart[i]);

        double Percent(int value, int total) => total == 0 ? 0.0 : 100.0 * value / total;
        Summary = $"{partName}: {Percent(stats.PartMissing, stats.PartPixels):F0}% zonder meting " +
                  $"(weinig licht {Percent(stats.PartLowSignal, stats.PartPixels):F0}%, " +
                  $"schaduw {Percent(stats.PartShadow, stats.PartPixels):F0}%, " +
                  $"rand {Percent(stats.PartEdge, stats.PartPixels):F0}%), " +
                  $"ruis {stats.PartNoiseMeters * 1000.0:F1} mm";
        GD.Print($"INFO Sensor, {Summary}; hout en achtergrond " +
                 $"{Percent(stats.OtherMissing, stats.OtherPixels):F0}% zonder meting");
    }
}
