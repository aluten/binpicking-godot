#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using BinPicking.Core.Sensing;
using Godot;

/// <summary>
/// Virtual depth camera that looks straight down. Provides perfect depth from the camera and the
/// projector, plus the reflectance per pixel. The sensor model turns those into a realistic measurement.
/// <para>
/// Nothing is lit here and no pattern is projected: the images are unlit renders. A real camera replaces this
/// class and the sensor model together. See docs/sensor-model.md and docs/demo-questions.md.
/// </para>
/// </summary>
public partial class DepthSensorRig : Node3D
{
    /// <summary>Render layer of the full-screen quad that writes depth; only the depth cameras see it.</summary>
    public const int DepthLayer = 2;

    /// <summary>Render layer of things the sensor must not see, such as the tool and the markers.</summary>
    public const int ModelLayer = 4;

    /// <summary>Render layer of the links, which gives the mask of link pixels for the statistics.</summary>
    public const int LinkLayer = 3;

    private const int ImageWidth = 1280;
    private const int ImageHeight = 960;

    /// <summary>The largest depth that fits in the image, in metres.</summary>
    private const float EncodedRange = 6f;

    /// <summary>Frames to wait, so every viewport has drawn the current scene.</summary>
    private const int FramesPerCapture = 4;

    // Writes depth as colour. A 16-bit float channel is too coarse for millimetres, so the value is split:
    // red holds the coarse steps and green the remainder. See ReadDepth for the way back.
    private const string DepthShaderCode = @"
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never;

uniform sampler2D depth_texture : hint_depth_texture, repeat_disable, filter_nearest;
uniform float encoded_range = 6.0;

void vertex() {
    POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

void fragment() {
    float depth = texture(depth_texture, SCREEN_UV).r;
    vec4 view = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
    view.xyz /= view.w;

    float v = clamp(-view.z / encoded_range, 0.0, 0.999999);
    float scaled = v * 255.0;
    ALBEDO = vec3(floor(scaled) / 255.0, fract(scaled), v);
}
";

    private SubViewport _cameraDepth = null!;
    private SubViewport _projectorDepth = null!;
    private SubViewport _albedo = null!;
    private SubViewport _partMask = null!;

    /// <summary>The camera model that belongs to these images.</summary>
    public PinholeCamera Camera { get; private set; }

    /// <summary>Places the camera in world space; image X is world X.</summary>
    public void Configure(Vector3 position, float verticalFovDegrees, float projectorBaseline)
    {
        Camera = PinholeCamera.FromVerticalFov(ImageWidth, ImageHeight, verticalFovDegrees);

        _cameraDepth = AddView(position, verticalFovDegrees, depthPass: true);
        _projectorDepth = AddView(position + Vector3.Right * projectorBaseline, verticalFovDegrees, depthPass: true);
        _albedo = AddView(position, verticalFovDegrees, depthPass: false, 1u);
        _albedo.DebugDraw = Viewport.DebugDrawEnum.Unshaded;
        _partMask = AddView(position, verticalFovDegrees, depthPass: false, 1u << (LinkLayer - 1));
        _partMask.DebugDraw = Viewport.DebugDrawEnum.Unshaded;

        AddChild(new SensorRigModel(Camera, position, projectorBaseline, SensorMount.AboveRim));
    }

    /// <summary>Renders once and returns depth from camera and projector, the reflectance, and which pixels show a link.</summary>
    public async Task<(float[] Depth, float[] ProjectorDepth, float[] Reflectance, bool[] IsPart)> CaptureAsync()
    {
        SetUpdating(SubViewport.UpdateMode.Always);
        for (int i = 0; i < FramesPerCapture; i++)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }
        Image depth = _cameraDepth.GetTexture().GetImage();
        Image projector = _projectorDepth.GetTexture().GetImage();
        Image albedo = _albedo.GetTexture().GetImage();
        Image mask = _partMask.GetTexture().GetImage();
        SetUpdating(SubViewport.UpdateMode.Disabled);

        return (ReadDepth(depth), ReadDepth(projector), ReadReflectance(albedo), ReadReflectance(mask).Select(r => r > 0f).ToArray());
    }

    /// <summary>The viewports only render during a capture, which saves a lot of rendering time.</summary>
    private void SetUpdating(SubViewport.UpdateMode mode)
    {
        _cameraDepth.RenderTargetUpdateMode = mode;
        _projectorDepth.RenderTargetUpdateMode = mode;
        _albedo.RenderTargetUpdateMode = mode;
        _partMask.RenderTargetUpdateMode = mode;
    }

    /// <summary>Adds one virtual camera with its own image, either for depth or for unlit colour.</summary>
    /// <param name="position">Camera position in world space, looking straight down.</param>
    /// <param name="verticalFovDegrees">Vertical field of view.</param>
    /// <param name="depthPass">True to write depth with the shader, false for plain colour.</param>
    /// <param name="cullMask">The render layers a colour view sees.</param>
    private SubViewport AddView(Vector3 position, float verticalFovDegrees, bool depthPass, uint cullMask = 0u)
    {
        var viewport = new SubViewport
        {
            Size = new Vector2I(ImageWidth, ImageHeight),
            UseHdr2D = true, // Float buffer without sRGB conversion.
            Msaa3D = Viewport.Msaa.Disabled,
            ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled,
            UseTaa = false,
            PositionalShadowAtlasSize = 0,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        AddChild(viewport);

        var camera = new Camera3D
        {
            Fov = verticalFovDegrees,
            Near = 0.1f,
            Far = EncodedRange,
            Position = position,
            RotationDegrees = new Vector3(-90f, 0f, 0f),
            CullMask = depthPass ? 1u | (1u << (DepthLayer - 1)) : cullMask,
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = Colors.Black,
                AmbientLightSource = Godot.Environment.AmbientSource.Disabled,
                ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
            },
        };
        viewport.AddChild(camera);
        camera.MakeCurrent();

        if (depthPass)
        {
            var material = new ShaderMaterial { Shader = new Shader { Code = DepthShaderCode } };
            material.SetShaderParameter("encoded_range", EncodedRange);
            camera.AddChild(new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = new Vector2(2f, 2f) },
                MaterialOverride = material,
                ExtraCullMargin = 16384f,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Layers = 1u << (DepthLayer - 1),
            });
        }
        return viewport;
    }

    /// <summary>Puts the coarse and fine channel back together into depth in metres.</summary>
    private static float[] ReadDepth(Image image)
    {
        (byte[] bytes, int stride) = HalfFloatData(image);
        var depth = new float[image.GetWidth() * image.GetHeight()];
        for (int i = 0; i < depth.Length; i++)
        {
            int at = i * stride;
            float coarse = MathF.Round((float)BitConverter.ToHalf(bytes, at) * 255f) / 255f;
            float fine = (float)BitConverter.ToHalf(bytes, at + 2) / 255f;
            depth[i] = EncodedRange * (coarse + fine);
        }
        return depth;
    }

    /// <summary>The average of red, green and blue, as the share of light that a surface returns.</summary>
    private static float[] ReadReflectance(Image image)
    {
        (byte[] bytes, int stride) = HalfFloatData(image);
        var reflectance = new float[image.GetWidth() * image.GetHeight()];
        for (int i = 0; i < reflectance.Length; i++)
        {
            int at = i * stride;
            float r = (float)BitConverter.ToHalf(bytes, at);
            float g = (float)BitConverter.ToHalf(bytes, at + 2);
            float b = (float)BitConverter.ToHalf(bytes, at + 4);
            reflectance[i] = Math.Clamp((r + g + b) / 3f, 0f, 1f);
        }
        return reflectance;
    }

    /// <summary>The raw image data, and the number of bytes per pixel for its format.</summary>
    private static (byte[] Bytes, int Stride) HalfFloatData(Image image)
    {
        return image.GetFormat() switch
        {
            Image.Format.Rgbh => (image.GetData(), 6),
            Image.Format.Rgbah => (image.GetData(), 8),
            _ => throw new InvalidOperationException($"Unexpected image format: {image.GetFormat()}"),
        };
    }
}
