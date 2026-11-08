#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BinPicking.Core.Sensing;
using BinPicking.Core.StructuredLight;
using Godot;
using static PalletDimensions;

/// <summary>
/// Captures what the sensor sees and turns it into a realistic measurement, in one of two ways: perfect depth
/// with a formula for holes and noise, or depth decoded from images of projected light. The rest of the demo only uses
/// <see cref="Camera"/>, <see cref="Result"/> and, for verification, <see cref="IdealDepth"/>. So this is the
/// class that a real camera would replace; see docs/demo-questions.md.
/// </summary>
public sealed class SensorMeasurement
{
    private readonly DepthSensorSettings _settings = new();
    private readonly DepthSensorRig _rig;
    private readonly ProjectedLightRig _lightRig;
    private int _measurements;
    private float[] _projectorDepth = System.Array.Empty<float>();
    private float[] _reflectance = System.Array.Empty<float>();
    private bool[] _isPart = System.Array.Empty<bool>();

    /// <summary>Hangs both rigs above the pallet, at the position of the sensor mount.</summary>
    /// <param name="rig">Renders perfect depth, which both ways need: the formula as input, both for verification.</param>
    /// <param name="lightRig">Projects the patterns.</param>
    /// <param name="linkMesh">The link mesh, which the light rig draws in its own world.</param>
    public SensorMeasurement(DepthSensorRig rig, ProjectedLightRig lightRig, Mesh linkMesh)
    {
        _rig = rig;
        _rig.Configure(SensorMount.Position, SensorMount.VerticalFovDegrees, _settings.ProjectorBaseline);
        _lightRig = lightRig;
        _lightRig.Configure(_rig.Camera, SensorMount.Position, SensorMount.VerticalFovDegrees, _settings.ProjectorBaseline, linkMesh);
    }

    /// <summary>Which way the next measurement uses.</summary>
    public SensorKind Kind { get; set; } = SensorKind.Formula;

    /// <summary>
    /// The exposures that key E steps through, in electrons per unit of light. Medium suits black links and
    /// overexposes white ones; HDR combines short and long. The values are own choices, not camera settings.
    /// </summary>
    public static readonly (string Name, float[] Electrons)[] Exposures =
    {
        ("kort", new[] { 4000f }),
        ("middel", new[] { 16000f }),
        ("lang", new[] { 64000f }),
        ("HDR kort + lang", new[] { 4000f, 64000f }),
    };

    /// <summary>Which of <see cref="Exposures"/> the next projected-light measurement uses.</summary>
    public int ExposureIndex { get; set; } = 1;

    /// <summary>
    /// The camera images of the latest projected-light measurement, as the decoder received them, from 0 to 1.
    /// With HDR these are the images of the longest exposure. Empty after a formula measurement.
    /// </summary>
    public IReadOnlyList<float[]> PatternImages { get; private set; } = System.Array.Empty<float[]>();

    /// <summary>The number of images a real camera would take for the current exposure setting.</summary>
    public int ImageCount => _lightRig.Settings.ImageCount * Exposures[ExposureIndex].Electrons.Length;

    /// <summary>A short name for the pattern in an image, for on screen.</summary>
    public string DescribePattern(int image)
    {
        StructuredLightSettings settings = _lightRig.Settings;
        return image < settings.SineSteps
            ? $"sinus, {image * 360 / settings.SineSteps} graden verschoven"
            : $"strepen (Gray-code) {image - settings.SineSteps + 1} van {settings.GrayBits}, van grof naar fijn";
    }

    /// <summary>The camera model that belongs to the measurement.</summary>
    public PinholeCamera Camera => _rig.Camera;

    /// <summary>Perfect depth in metres. This exists only in simulation, and the search never uses it.</summary>
    public float[] IdealDepth { get; private set; } = System.Array.Empty<float>();

    /// <summary>The measured depth, with NaN for a hole. Null until the first measurement.</summary>
    public DepthSensorResult? Result { get; private set; }

    /// <summary>One line about the measurement, for on screen.</summary>
    public string Summary { get; private set; } = string.Empty;

    /// <summary>Captures the scene as it is now, including the colour of the links, and measures it.</summary>
    /// <param name="partName">How to call the links in the summary.</param>
    /// <param name="links">The links in the bin, which the light rig draws in its own world.</param>
    public async Task MeasureAsync(string partName, IReadOnlyList<Transform3D> links)
    {
        _measurements++;
        (IdealDepth, _projectorDepth, _reflectance, _isPart) = await _rig.CaptureAsync();
        CheckRimDistance();

        if (Kind == SensorKind.Formula)
        {
            Result = DepthSensorModel.Apply(Camera, IdealDepth, _projectorDepth, _reflectance, _settings);
            PatternImages = System.Array.Empty<float[]>();
        }
        else
        {
            // The scene does not change between exposures, so one render of the light serves them all.
            // Each exposure gets noise of its own, as separate camera images would.
            List<float[]> light = await _lightRig.CaptureAsync(links);
            var measurements = new List<DepthSensorResult>();
            float[] exposures = Exposures[ExposureIndex].Electrons;
            for (int e = 0; e < exposures.Length; e++)
            {
                var noise = new CameraNoiseSettings { ElectronsPerLightUnit = exposures[e] };
                // New noise for every measurement, but the same series gives the same noise when it is run again.
                List<float[]> images = light
                    .Select((image, i) => CameraNoiseModel.Expose(image, noise, _measurements * 1000 + e * 100 + i))
                    .ToList();
                // The decoder knows the sensor, so it can reject pixels whose signal is lost in the noise.
                measurements.Add(PatternDecoder.Decode(Camera, _lightRig.Projector, _lightRig.Settings, images, noise));
                PatternImages = images;
            }
            Result = measurements.Count == 1 ? measurements[0] : HdrCombiner.Combine(measurements);
        }
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
        string reasons = Kind == SensorKind.Formula
            ? $"weinig licht {Percent(stats.PartLowSignal, stats.PartPixels):F0}%, " +
              $"schaduw {Percent(stats.PartShadow, stats.PartPixels):F0}%, " +
              $"rand {Percent(stats.PartEdge, stats.PartPixels):F0}%"
            // From the images alone, shadow cannot be told apart from too little light.
            : $"weinig signaal of schaduw {Percent(stats.PartLowSignal, stats.PartPixels):F0}%, " +
              $"patronen oneens {Percent(stats.PartEdge, stats.PartPixels):F0}%, " +
              $"overbelicht {Percent(stats.PartOverexposed, stats.PartPixels):F0}%";
        string sensor = Kind == SensorKind.Formula
            ? "Sensor (formule)"
            : $"Sensor (licht, belichting {Exposures[ExposureIndex].Name}, {ImageCount} beelden)";
        Summary = $"{sensor}, {partName}: {Percent(stats.PartMissing, stats.PartPixels):F0}% zonder meting " +
                  $"({reasons}), afwijking {stats.PartNoiseMeters * 1000.0:F1} mm";
        GD.Print($"INFO {Summary}; hout en achtergrond " +
                 $"{Percent(stats.OtherMissing, stats.OtherPixels):F0}% zonder meting");
    }
}
