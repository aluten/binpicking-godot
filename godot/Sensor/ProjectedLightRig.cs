#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BinPicking.Core.Sensing;
using BinPicking.Core.StructuredLight;
using Godot;

/// <summary>
/// The camera and projector of a structured-light sensor, lit for real: a spot light projects each pattern and a
/// camera takes an image. The rig has a world of its own with only the pallet and the links, so the lights of the
/// demo view do not end up in the measurement. Returns the light per pixel; noise and decoding happen in the core.
/// </summary>
public partial class ProjectedLightRig : Node3D
{
    /// <summary>Half the opening angle of the projector, horizontally. Must cover the bin from the sensor height.</summary>
    private const float SpotAngleDegrees = 35f;

    /// <summary>Brightness of the projector in Godot units. Exposure decides how much of it the camera records.</summary>
    private const float LightEnergy = 6f;

    /// <summary>Frames to wait after changing the pattern, so the image shows the new pattern.</summary>
    private const int FramesPerImage = 3;

    /// <summary>The pattern only varies per column, so a few rows are enough.</summary>
    private const int PatternHeight = 16;

    private readonly List<ImageTexture> _patterns = new();
    private SubViewport _viewport = null!;
    private SpotLight3D _projectorLight = null!;
    private BinFillView _fill = null!;

    /// <summary>The projector that belongs to the images, for triangulation.</summary>
    public ProjectorModel Projector { get; private set; }

    /// <summary>The patterns that the decoder expects, in the same order.</summary>
    public StructuredLightSettings Settings { get; } = new();

    /// <summary>Builds the rig. The camera matches the other sensor, so both measurements can be compared per pixel.</summary>
    /// <param name="camera">The camera model of the other sensor.</param>
    /// <param name="position">Camera position in world space, looking straight down.</param>
    /// <param name="verticalFovDegrees">Vertical field of view of the camera.</param>
    /// <param name="baseline">Distance from camera to projector along world X, in metres.</param>
    /// <param name="linkMesh">The link mesh. Its material is shared, so key B changes the colour here too.</param>
    public void Configure(PinholeCamera camera, Vector3 position, float verticalFovDegrees, float baseline, Mesh linkMesh)
    {
        Projector = new ProjectorModel(Settings.Columns, Mathf.Tan(Mathf.DegToRad(SpotAngleDegrees)), baseline);
        BuildPatterns();

        _viewport = new SubViewport
        {
            Size = new Vector2I(camera.Width, camera.Height),
            OwnWorld3D = true,
            UseHdr2D = true, // Float buffer, so bright light does not clip before the camera model does.
            Msaa3D = Viewport.Msaa.Disabled,
            ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled,
            PositionalShadowAtlasSize = 8192, // Sharp shadows of the projector.
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        AddChild(_viewport);

        var cameraNode = new Camera3D
        {
            Fov = verticalFovDegrees,
            Near = 0.1f,
            Far = 6f,
            Position = position,
            RotationDegrees = new Vector3(-90f, 0f, 0f),
            // No ambient light: the projector is the only light, as in a dark hall.
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = Colors.Black,
                AmbientLightSource = Godot.Environment.AmbientSource.Disabled,
                ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
            },
        };
        _viewport.AddChild(cameraNode);
        cameraNode.MakeCurrent();

        _projectorLight = new SpotLight3D
        {
            Position = position + Vector3.Right * baseline,
            RotationDegrees = new Vector3(-90f, 0f, 0f),
            SpotAngle = SpotAngleDegrees,
            SpotAngleAttenuation = 0.01f, // Even brightness across the cone.
            SpotRange = 20f,
            SpotAttenuation = 2f, // Falls off with distance squared.
            LightEnergy = LightEnergy,
            ShadowEnabled = true,
            ShadowBias = 0.02f,
            ShadowNormalBias = 0.5f,
        };
        _viewport.AddChild(_projectorLight);

        _viewport.AddChild(new PalletBin());
        _fill = new BinFillView(linkMesh, Array.Empty<Transform3D>());
        _viewport.AddChild(_fill);
    }

    /// <summary>Projects every pattern in turn on these links and returns the light per pixel, before any noise.</summary>
    public async Task<List<float[]>> CaptureAsync(IReadOnlyList<Transform3D> links)
    {
        _fill.ShowLinks(links);
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        var images = new List<float[]>();
        foreach (ImageTexture pattern in _patterns)
        {
            _projectorLight.LightProjector = pattern;
            for (int i = 0; i < FramesPerImage; i++)
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            }
            images.Add(ReadLight(_viewport.GetTexture().GetImage()));
        }
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        return images;
    }

    /// <summary>One texture per pattern, sampled at the centre of each column.</summary>
    private void BuildPatterns()
    {
        var generator = new PatternGenerator(Settings);
        for (int image = 0; image < Settings.ImageCount; image++)
        {
            var bytes = new byte[Settings.Columns * PatternHeight * 4];
            for (int x = 0; x < Settings.Columns; x++)
            {
                byte value = (byte)Mathf.RoundToInt(generator.Value(image, x + 0.5f) * 255f);
                for (int y = 0; y < PatternHeight; y++)
                {
                    int at = (y * Settings.Columns + x) * 4;
                    bytes[at] = value;
                    bytes[at + 1] = value;
                    bytes[at + 2] = value;
                    bytes[at + 3] = 255;
                }
            }
            _patterns.Add(ImageTexture.CreateFromImage(
                Image.CreateFromData(Settings.Columns, PatternHeight, false, Image.Format.Rgba8, bytes)));
        }
    }

    /// <summary>The red channel of the half-float image. The links and the light are grey, so one channel is enough.</summary>
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
}
