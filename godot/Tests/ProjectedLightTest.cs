#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Experiment: can a Godot spot light act as the projector of a structured-light camera?
/// Projects stripes and sine patterns on a flat plate that is half white and half black, and measures what a
/// virtual camera sees. It then lights the stored fill once with a pattern. Nothing else uses this code.
/// Run this scene with F6. Results appear in the Output panel.
/// Optional: -- --out=folder saves images.
/// </summary>
public partial class ProjectedLightTest : Node3D
{
    private const int ImageWidth = 1280;
    private const int ImageHeight = 960;
    private const float CameraFovDegrees = 35f;
    private const float Baseline = 0.15f;
    private const float SpotAngleDegrees = 35f;
    private const float LightEnergy = 6f;
    private const int FramesPerImage = 3;

    private readonly int[] _textureWidths = { 1024, 2048, 4096 };
    private readonly float[] _plateDistances = { 1.5f, 2.5f };

    private SubViewport _viewport = null!;
    private Camera3D _camera = null!;
    private SpotLight3D _light = null!;
    private Node3D _plate = null!;
    private string? _outFolder;

    public override async void _Ready()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--out="))
            {
                _outFolder = arg[6..];
            }
        }

        BuildRig();
        BuildPlate();
        GD.Print($"Filter projector: {ProjectSettings.GetSetting("rendering/textures/light_projectors/filter")}");

        foreach (float distance in _plateDistances)
        {
            _plate.Position = new Vector3(0f, 2.5f - distance, 0f);
            foreach (int width in _textureWidths)
            {
                await MeasureStripesAsync(width, distance);
                await MeasureSineAsync(width, distance);
            }
        }

        _plate.QueueFree();
        await MeasureFillAsync();
        GD.Print("KLAAR");
        GetTree().Quit();
    }

    private void BuildRig()
    {
        _viewport = new SubViewport
        {
            Size = new Vector2I(ImageWidth, ImageHeight),
            UseHdr2D = true,
            Msaa3D = Viewport.Msaa.Disabled,
            ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled,
            PositionalShadowAtlasSize = 8192,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        AddChild(_viewport);

        // The camera looks straight down from 2.5 m above y = 0, with no light other than the projector.
        _camera = new Camera3D
        {
            Fov = CameraFovDegrees,
            Near = 0.1f,
            Far = 6f,
            Position = new Vector3(0f, 2.5f, 0f),
            RotationDegrees = new Vector3(-90f, 0f, 0f),
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = Colors.Black,
                AmbientLightSource = Godot.Environment.AmbientSource.Disabled,
                ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
            },
        };
        _viewport.AddChild(_camera);
        _camera.MakeCurrent();

        _light = new SpotLight3D
        {
            Position = new Vector3(Baseline, 2.5f, 0f),
            RotationDegrees = new Vector3(-90f, 0f, 0f),
            SpotAngle = SpotAngleDegrees,
            SpotAngleAttenuation = 0.01f,
            SpotRange = 20f,
            SpotAttenuation = 2f,
            LightEnergy = LightEnergy,
            ShadowEnabled = true,
            ShadowBias = 0.02f,
            ShadowNormalBias = 0.5f,
        };
        AddChild(_light);
    }

    /// <summary>White for world Z below 0, black above.</summary>
    private void BuildPlate()
    {
        _plate = new Node3D();
        AddChild(_plate);
        AddBox(new Vector3(3f, 0.01f, 1.5f), new Vector3(0f, 0f, -0.75f), 0.9f);
        AddBox(new Vector3(3f, 0.01f, 1.5f), new Vector3(0f, 0f, 0.75f), 0.03f);
    }

    private void AddBox(Vector3 size, Vector3 position, float albedo)
    {
        _plate.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = size,
                Material = new StandardMaterial3D { AlbedoColor = new Color(albedo, albedo, albedo), Roughness = 0.8f },
            },
            Position = position,
        });
    }

    private async Task<(float[] White, float[] Black, double Ms)> CaptureRowsAsync(ImageTexture pattern)
    {
        ulong started = Time.GetTicksMsec();
        _light.LightProjector = pattern;
        for (int i = 0; i < FramesPerImage; i++)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }
        Image image = _viewport.GetTexture().GetImage();
        double ms = Time.GetTicksMsec() - started;
        return (ReadRow(image, ImageHeight / 4), ReadRow(image, ImageHeight * 3 / 4), ms);
    }

    private static float[] ReadRow(Image image, int y)
    {
        var row = new float[ImageWidth];
        for (int x = 0; x < ImageWidth; x++)
        {
            row[x] = image.GetPixel(x, y).R;
        }
        return row;
    }

    /// <summary>How clearly stripes of a given period survive, from coarse to the finest possible.</summary>
    private async Task MeasureStripesAsync(int width, float distance)
    {
        foreach (int period in new[] { 512, 64, 16, 8, 4, 2 })
        {
            ImageTexture pattern = MakeStripes(width, period, 0f);
            (float[] white, float[] black, double ms) = await CaptureRowsAsync(pattern);
            GD.Print($"STREEP afstand {distance} m, breedte {width}, periode {period} tx ({Millimeters(width, distance) * period:F1} mm): " +
                     $"contrast wit {Contrast(white):F2}, zwart {Contrast(black):F2}; helder wit {Percentile(white, 0.95f):F3}, zwart {Percentile(black, 0.95f):F4}; {ms:F0} ms");
        }
    }

    /// <summary>Four-step phase shift on the flat plate: how straight is the recovered phase, and what depth error follows?</summary>
    private async Task MeasureSineAsync(int width, float distance)
    {
        foreach (int period in new[] { 128, 32, 16 })
        {
            var white = new float[4][];
            var black = new float[4][];
            for (int k = 0; k < 4; k++)
            {
                (white[k], black[k], _) = await CaptureRowsAsync(MakeSine(width, period, k * Mathf.Pi / 2f));
            }

            float mm = Millimeters(width, distance) * period;
            GD.Print($"SINUS afstand {distance} m, breedte {width}, periode {period} tx ({mm:F1} mm): " +
                     $"wit {PhaseReport(white, mm, distance)}; zwart {PhaseReport(black, mm, distance)}");
        }
    }

    private static string PhaseReport(float[][] images, float periodMm, float distance)
    {
        // Only the middle of the image, well inside the cone of the light.
        int start = ImageWidth / 4, end = ImageWidth * 3 / 4;
        var phases = new List<double>();
        double previous = 0, offset = 0, modulation = 0;
        for (int x = start; x < end; x++)
        {
            double a = images[3][x] - images[1][x];
            double b = images[0][x] - images[2][x];
            double phase = Math.Atan2(a, b);
            if (x > start)
            {
                if (phase - previous > Math.PI) offset -= Math.Tau;
                if (phase - previous < -Math.PI) offset += Math.Tau;
            }
            previous = phase;
            phases.Add(phase + offset);
            modulation += Math.Sqrt(a * a + b * b) / 2.0;
        }

        // Straight-line fit: a flat plate parallel to the projector gives a straight phase.
        double n = phases.Count, sx = 0, sy = 0, sxx = 0, sxy = 0;
        for (int i = 0; i < phases.Count; i++)
        {
            sx += i; sy += phases[i]; sxx += (double)i * i; sxy += i * phases[i];
        }
        double slope = (n * sxy - sx * sy) / (n * sxx - sx * sx);
        double intercept = (sy - slope * sx) / n;
        double squared = 0;
        for (int i = 0; i < phases.Count; i++)
        {
            double residual = phases[i] - (slope * i + intercept);
            squared += residual * residual;
        }
        double rms = Math.Sqrt(squared / n);
        double depthMm = rms / Math.Tau * periodMm * distance / Baseline;
        return $"modulatie {modulation / n:F4}, restfout {rms:F3} rad = {depthMm:F2} mm diepte, {Math.Abs(slope) * ImageWidth / Math.Tau:F1} perioden/beeld";
    }

    /// <summary>The real pile, lit once, to see shadows and light on black links.</summary>
    private async Task MeasureFillAsync()
    {
        if (!BinFillStore.Exists())
        {
            GD.Print("Geen vulling opgeslagen.");
            return;
        }

        Mesh? mesh = LinkAssets.LoadSingleMesh(LinkAssets.LodPath);
        var fill = new BinFillView(mesh!, BinFillStore.Load());
        AddChild(fill);
        var bin = new PalletBin();
        AddChild(bin);
        _camera.Position = new Vector3(0f, SensorMount.Height, 0f);
        _camera.Fov = SensorMount.VerticalFovDegrees;
        _light.Position = new Vector3(Baseline, SensorMount.Height, 0f);

        foreach ((string name, ImageTexture pattern) in new[]
        {
            ("vol", MakeFlat(2048)),
            ("streep", MakeStripes(2048, 32, 0f)),
            ("sinus", MakeSine(2048, 32, 0f)),
        })
        {
            ulong started = Time.GetTicksMsec();
            _light.LightProjector = pattern;
            for (int i = 0; i < FramesPerImage; i++)
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            }
            Image image = _viewport.GetTexture().GetImage();
            GD.Print($"BAK {name}: {Time.GetTicksMsec() - started} ms voor {FramesPerImage} frames, formaat {image.GetFormat()}");
            if (_outFolder != null)
            {
                image.Convert(Image.Format.Rgba8);
                image.SavePng($"{_outFolder}/bak_{name}.png");
            }
        }
    }

    private float Millimeters(int width, float distance) =>
        2f * distance * Mathf.Tan(Mathf.DegToRad(SpotAngleDegrees)) / width * 1000f;

    private static float Contrast(float[] row)
    {
        float high = Percentile(row, 0.95f), low = Percentile(row, 0.05f);
        return high + low < 1e-5f ? 0f : (high - low) / (high + low);
    }

    private static float Percentile(float[] row, float share)
    {
        // The middle of the image only, well inside the cone of the light.
        var middle = new float[row.Length / 2];
        Array.Copy(row, row.Length / 4, middle, 0, middle.Length);
        Array.Sort(middle);
        return middle[(int)(share * (middle.Length - 1))];
    }

    private static ImageTexture MakeFlat(int width) => FromColumns(width, _ => 1f);

    private static ImageTexture MakeStripes(int width, int period, float shift) =>
        FromColumns(width, x => (x + shift) % period < period / 2f ? 1f : 0f);

    private static ImageTexture MakeSine(int width, int period, float phase) =>
        FromColumns(width, x => 0.5f + 0.5f * Mathf.Cos(Mathf.Tau * (x + 0.5f) / period + phase));

    private static ImageTexture FromColumns(int width, Func<int, float> value)
    {
        const int height = 16;
        var bytes = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte v = (byte)Mathf.RoundToInt(value(x) * 255f);
                int at = (y * width + x) * 4;
                bytes[at] = v;
                bytes[at + 1] = v;
                bytes[at + 2] = v;
                bytes[at + 3] = 255;
            }
        }
        return ImageTexture.CreateFromImage(Image.CreateFromData(width, height, false, Image.Format.Rgba8, bytes));
    }
}
