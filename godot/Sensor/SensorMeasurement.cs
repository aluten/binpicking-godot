#nullable enable

using System.Linq;
using System.Threading.Tasks;
using BinPicking.Core.Sensing;
using Godot;
using static PalletDimensions;

/// <summary>Captures perfect depth once and derives measurements from it for black or light parts.</summary>
public sealed class SensorMeasurement
{
    private const float PartReflectanceLimit = 0.2f;
    private const float LightPartReflectance = 0.5f;

    private readonly DepthSensorSettings _settings = new();
    private readonly DepthSensorRig _rig;
    private float[] _projectorDepth = System.Array.Empty<float>();
    private float[] _reflectance = System.Array.Empty<float>();

    public SensorMeasurement(DepthSensorRig rig)
    {
        _rig = rig;
        _rig.Configure(SensorMount.Position, SensorMount.VerticalFovDegrees, _settings.ProjectorBaseline);
    }

    public PinholeCamera Camera => _rig.Camera;

    public float[] IdealDepth { get; private set; } = System.Array.Empty<float>();

    public DepthSensorResult? Result { get; private set; }

    public bool LightParts { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public async Task CaptureAsync()
    {
        (IdealDepth, _projectorDepth, _reflectance) = await _rig.CaptureAsync();
        CheckRimDistance();
    }

    /// <summary>Applies the sensor model again, optionally pretending the parts are light.</summary>
    public void Measure(bool lightParts)
    {
        LightParts = lightParts;
        float[] reflectance = lightParts
            ? _reflectance.Select(r => r < PartReflectanceLimit ? LightPartReflectance : r).ToArray()
            : _reflectance;

        Result = DepthSensorModel.Apply(Camera, IdealDepth, _projectorDepth, reflectance, _settings);
        Report(Result);
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

    private void Report(DepthSensorResult result)
    {
        DepthSensorStatistics stats = DepthSensorStatistics.Compute(
            result, IdealDepth, i => _reflectance[i] < PartReflectanceLimit);

        double Percent(int value, int total) => total == 0 ? 0.0 : 100.0 * value / total;
        string kind = LightParts ? "lichte schakels" : "zwarte schakels";
        Summary = $"{kind}: {Percent(stats.PartMissing, stats.PartPixels):F0}% zonder meting " +
                  $"(weinig licht {Percent(stats.PartLowSignal, stats.PartPixels):F0}%, " +
                  $"schaduw {Percent(stats.PartShadow, stats.PartPixels):F0}%, " +
                  $"rand {Percent(stats.PartEdge, stats.PartPixels):F0}%), " +
                  $"ruis {stats.PartNoiseMeters * 1000.0:F1} mm";
        GD.Print($"INFO Sensor, {Summary}; hout en achtergrond " +
                 $"{Percent(stats.OtherMissing, stats.OtherPixels):F0}% zonder meting");
    }
}
