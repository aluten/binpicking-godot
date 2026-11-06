#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BinPicking.Core.Grasping;
using BinPicking.Core.Sensing;
using BinPicking.Core.StructuredLight;
using Godot;

/// <summary>
/// Experiment: measures depth with real projected light. A Godot spot light projects the patterns, a virtual
/// camera takes the images, sensor noise is added and the core library decodes the depth. Compares that with
/// perfect depth, first on a flat plate and then on the stored fill. The demo itself does not use this yet.
/// Run this scene with F6. Results appear in the Output panel and the measured bin appears in the window.
/// Optional: -- --out=folder saves images, --quit closes the window when finished.
/// </summary>
public partial class StructuredLightTest : Node3D
{
    private const int ImageWidth = 1280;
    private const int ImageHeight = 960;
    private const float Baseline = 0.15f;
    private const float SpotAngleDegrees = 35f;
    private const float LightEnergy = 6f;
    private const int FramesPerImage = 3;
    private const int PatternHeight = 16;
    private const float LargeErrorMeters = 0.005f;

    private static readonly float[] Exposures = { 4000f, 16000f, 64000f };

    private readonly StructuredLightSettings _settings = new();
    private readonly List<ImageTexture> _patterns = new();
    private readonly GraspSettings _graspSettings = new();
    private PinholeCamera _pinhole;
    private ProjectorModel _projector;
    private SubViewport _viewport = null!;
    private SpotLight3D _light = null!;
    private Node3D _plate = null!;
    private string? _outFolder;

    public override async void _Ready()
    {
        string[] args = OS.GetCmdlineUserArgs();
        _outFolder = args.FirstOrDefault(a => a.StartsWith("--out="))?[6..];

        _pinhole = PinholeCamera.FromVerticalFov(ImageWidth, ImageHeight, SensorMount.VerticalFovDegrees);
        _projector = new ProjectorModel(_settings.Columns, Mathf.Tan(Mathf.DegToRad(SpotAngleDegrees)), Baseline);
        BuildPatterns();
        BuildRig();
        BuildPlate();

        foreach (float distance in new[] { 1.5f, 2.5f })
        {
            await MeasurePlateAsync(distance);
        }
        _plate.QueueFree();

        byte[]? shown = await MeasureBinAsync();
        ShowResult(shown);
        GD.Print("KLAAR");
        if (args.Contains("--quit"))
        {
            GetTree().Quit();
        }
    }

    private void BuildPatterns()
    {
        var generator = new PatternGenerator(_settings);
        for (int image = 0; image < _settings.ImageCount; image++)
        {
            var bytes = new byte[_settings.Columns * PatternHeight * 4];
            for (int x = 0; x < _settings.Columns; x++)
            {
                byte value = (byte)Mathf.RoundToInt(generator.Value(image, x + 0.5f) * 255f);
                for (int y = 0; y < PatternHeight; y++)
                {
                    int at = (y * _settings.Columns + x) * 4;
                    bytes[at] = value;
                    bytes[at + 1] = value;
                    bytes[at + 2] = value;
                    bytes[at + 3] = 255;
                }
            }
            _patterns.Add(ImageTexture.CreateFromImage(Image.CreateFromData(_settings.Columns, PatternHeight, false, Image.Format.Rgba8, bytes)));
        }
    }

    private void BuildRig()
    {
        _viewport = new SubViewport
        {
            Size = new Vector2I(ImageWidth, ImageHeight),
            UseHdr2D = true, // Float buffer, so the light does not clip and has no sRGB curve.
            Msaa3D = Viewport.Msaa.Disabled,
            ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled,
            PositionalShadowAtlasSize = 8192,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        AddChild(_viewport);

        var camera = new Camera3D
        {
            Fov = SensorMount.VerticalFovDegrees,
            Near = 0.1f,
            Far = 6f,
            CullMask = 1u,
            Position = SensorMount.Position,
            RotationDegrees = new Vector3(-90f, 0f, 0f),
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = Colors.Black,
                AmbientLightSource = Godot.Environment.AmbientSource.Disabled,
                ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
            },
        };
        _viewport.AddChild(camera);
        camera.MakeCurrent();

        _light = new SpotLight3D
        {
            Position = SensorMount.Position + Vector3.Right * Baseline,
            RotationDegrees = new Vector3(-90f, 0f, 0f),
            SpotAngle = SpotAngleDegrees,
            SpotAngleAttenuation = 0.01f,
            SpotRange = 20f,
            SpotAttenuation = 2f, // Falls off with distance squared.
            LightEnergy = LightEnergy,
            ShadowEnabled = true,
            ShadowBias = 0.02f,
            ShadowNormalBias = 0.5f,
        };
        AddChild(_light);
    }

    /// <summary>White on the half with world Z below 0, which is the top of the image; black on the other half.</summary>
    private void BuildPlate()
    {
        _plate = new Node3D();
        AddChild(_plate);
        AddPlatePart(new Vector3(0f, 0f, -0.75f), 0.9f);
        AddPlatePart(new Vector3(0f, 0f, 0.75f), 0.03f);
    }

    private void AddPlatePart(Vector3 position, float albedo)
    {
        _plate.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = new Vector3(3f, 0.01f, 1.5f),
                Material = new StandardMaterial3D { AlbedoColor = new Color(albedo, albedo, albedo), Roughness = 0.8f },
            },
            Position = position,
        });
    }

    /// <summary>Projects every pattern in turn and returns the light per pixel, without any noise yet.</summary>
    private async Task<List<float[]>> CaptureLightAsync()
    {
        var images = new List<float[]>();
        foreach (ImageTexture pattern in _patterns)
        {
            _light.LightProjector = pattern;
            for (int i = 0; i < FramesPerImage; i++)
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            }
            images.Add(ReadLight(_viewport.GetTexture().GetImage()));
        }
        return images;
    }

    private static float[] ReadLight(Image image)
    {
        int stride = image.GetFormat() == Image.Format.Rgbah ? 8 : 6;
        byte[] bytes = image.GetData();
        var light = new float[image.GetWidth() * image.GetHeight()];
        for (int i = 0; i < light.Length; i++)
        {
            light[i] = (float)BitConverter.ToHalf(bytes, i * stride);
        }
        return light;
    }

    private DepthSensorResult Decode(List<float[]> light, CameraNoiseSettings noise)
    {
        List<float[]> images = light.Select((l, i) => CameraNoiseModel.Expose(l, noise, 100 + i)).ToList();
        return PatternDecoder.Decode(_pinhole, _projector, _settings, images);
    }

    private async Task MeasurePlateAsync(float distance)
    {
        _plate.Position = new Vector3(0f, SensorMount.Height - distance - 0.005f, 0f);
        List<float[]> light = await CaptureLightAsync();

        foreach (float exposure in Exposures)
        {
            DepthSensorResult result = Decode(light, new CameraNoiseSettings { ElectronsPerLightUnit = exposure });
            GD.Print($"PLAAT {distance} m, belichting {exposure:F0}: " +
                     $"wit {PlateReport(result, distance, ImageHeight / 8, ImageHeight * 3 / 8)}; " +
                     $"zwart {PlateReport(result, distance, ImageHeight * 5 / 8, ImageHeight * 7 / 8)}");
        }
    }

    private string PlateReport(DepthSensorResult result, float distance, int fromRow, int toRow)
    {
        var errors = new List<float>();
        int total = 0, overexposed = 0;
        for (int y = fromRow; y < toRow; y++)
        {
            for (int x = ImageWidth / 4; x < ImageWidth * 3 / 4; x++)
            {
                int i = y * ImageWidth + x;
                total++;
                overexposed += result.Missing[i] == MissingReason.Overexposed ? 1 : 0;
                if (!float.IsNaN(result.Depth[i]))
                {
                    errors.Add(result.Depth[i] - distance);
                }
            }
        }

        if (errors.Count == 0)
        {
            return $"geen meting ({overexposed * 100 / total}% overbelicht)";
        }
        errors.Sort();
        float median = errors[errors.Count / 2];
        float spread = (errors[errors.Count * 84 / 100] - errors[errors.Count * 16 / 100]) / 2f;
        return $"{(total - errors.Count) * 100f / total:F0}% gaten, afwijking {median * 1000f:F1} mm, spreiding {spread * 1000f:F1} mm";
    }

    /// <summary>The stored fill, black and white, at several exposures, including what the pick search makes of it.</summary>
    private async Task<byte[]?> MeasureBinAsync()
    {
        if (!BinFillStore.Exists())
        {
            GD.Print("Geen vulling opgeslagen.");
            return null;
        }

        Mesh mesh = LinkAssets.LoadSingleMesh(LinkAssets.LodPath)!;
        var appearance = new LinkAppearance(mesh);
        AddChild(new BinFillView(mesh, BinFillStore.Load()));
        AddChild(new PalletBin());

        var rig = new DepthSensorRig();
        AddChild(rig);
        rig.Configure(SensorMount.Position, SensorMount.VerticalFovDegrees, Baseline);

        byte[]? shown = null;
        foreach (bool white in new[] { false, true })
        {
            appearance.SetWhite(white);
            (float[] ideal, _, _, bool[] isPart) = await rig.CaptureAsync();
            ulong started = Time.GetTicksMsec();
            List<float[]> light = await CaptureLightAsync();
            GD.Print($"BAK {(white ? "wit" : "zwart")}: {_patterns.Count} beelden in {Time.GetTicksMsec() - started} ms");

            foreach (float exposure in Exposures)
            {
                DepthSensorResult result = Decode(light, new CameraNoiseSettings { ElectronsPerLightUnit = exposure });
                ReportBin(result, ideal, isPart, white, exposure);

                if (_outFolder != null)
                {
                    SensorImage.ToImage(SensorImage.Draw(result.Depth, result.Missing), _pinhole)
                        .SavePng($"{_outFolder}/{(white ? "wit" : "zwart")}_{exposure:F0}.png");
                }
                if (!white && exposure == Exposures[1])
                {
                    shown = SensorImage.Draw(result.Depth, result.Missing);
                }
            }
        }
        appearance.SetWhite(false);
        return shown;
    }

    private void ReportBin(DepthSensorResult result, float[] ideal, bool[] isPart, bool white, float exposure)
    {
        DepthSensorStatistics stats = DepthSensorStatistics.Compute(result, ideal, i => isPart[i]);
        var errors = new List<float>();
        for (int i = 0; i < ideal.Length; i++)
        {
            if (isPart[i] && !float.IsNaN(result.Depth[i]))
            {
                errors.Add(Mathf.Abs(result.Depth[i] - ideal[i]));
            }
        }
        errors.Sort();
        float median = errors.Count > 0 ? errors[errors.Count / 2] : float.NaN;
        int large = errors.Count(e => e > LargeErrorMeters);

        (List<GraspCandidate> candidates, GraspSearchReport report) =
            GraspPlanner.FindGrasps(_pinhole, result.Depth, SensorMount.BinRegion, _graspSettings);
        int sealing = candidates.Count(c => SealVerifier.TrueSealShare(_pinhole, ideal, c, _graspSettings) >= 0.9f);

        float Percent(int value) => value * 100f / Mathf.Max(stats.PartPixels, 1);
        GD.Print($"BAK {(white ? "wit" : "zwart")}, belichting {exposure:F0}: {Percent(stats.PartMissing):F0}% gaten " +
                 $"(signaal {Percent(stats.PartLowSignal):F0}, onenig {Percent(stats.PartEdge):F0}, overbelicht {Percent(stats.PartOverexposed):F0}); " +
                 $"fout mediaan {median * 1000f:F1} mm, boven 5 mm {large * 100f / Mathf.Max(errors.Count, 1):F0}%; " +
                 $"pickpunten {report.Accepted} van {report.Evaluated}, {sealing} van {candidates.Count} dichten af");
    }

    /// <summary>Shows the measured bin in the window, so there is something to see besides the Output panel.</summary>
    private void ShowResult(byte[]? pixels)
    {
        if (pixels == null)
        {
            return;
        }

        var layer = new CanvasLayer();
        AddChild(layer);
        var image = new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(SensorImage.ToImage(pixels, _pinhole)),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        image.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(image);
        layer.AddChild(new Label { Text = $"Zwarte schakels, gemeten met geprojecteerd licht, belichting {Exposures[1]:F0}. Magenta = te weinig signaal, geel = onenig of overbelicht.", Position = new Vector2(10f, 10f) });
    }
}
