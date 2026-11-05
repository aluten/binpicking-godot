#nullable enable

using System;
using System.Threading.Tasks;
using BinPicking.Core.Sensing;
using Godot;

/// <summary>
/// Virtual depth camera that looks straight down. Provides perfect depth from the camera and the
/// projector, plus the reflectance per pixel. The sensor model turns those into a realistic measurement.
/// </summary>
public partial class DepthSensorRig : Node3D
{
    public const int DepthLayer = 2;
    public const int ModelLayer = 4;

    private const int ImageWidth = 1280;
    private const int ImageHeight = 960;
    private const float EncodedRange = 6f;

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

    public PinholeCamera Camera { get; private set; }

    /// <summary>Places the camera in world space; image X is world X.</summary>
    public void Configure(Vector3 position, float verticalFovDegrees, float projectorBaseline)
    {
        Camera = PinholeCamera.FromVerticalFov(ImageWidth, ImageHeight, verticalFovDegrees);

        _cameraDepth = AddView(position, verticalFovDegrees, depthPass: true);
        _projectorDepth = AddView(position + Vector3.Right * projectorBaseline, verticalFovDegrees, depthPass: true);
        _albedo = AddView(position, verticalFovDegrees, depthPass: false);
        _albedo.DebugDraw = Viewport.DebugDrawEnum.Unshaded;

        AddBodyModel(position, projectorBaseline);
    }

    /// <summary>Renders once and returns depth from camera and projector, and the reflectance.</summary>
    public async Task<(float[] Depth, float[] ProjectorDepth, float[] Reflectance)> CaptureAsync()
    {
        SetUpdating(SubViewport.UpdateMode.Always);
        for (int i = 0; i < 4; i++)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }
        Image depth = _cameraDepth.GetTexture().GetImage();
        Image projector = _projectorDepth.GetTexture().GetImage();
        Image albedo = _albedo.GetTexture().GetImage();
        SetUpdating(SubViewport.UpdateMode.Disabled);

        return (ReadDepth(depth), ReadDepth(projector), ReadReflectance(albedo));
    }

    private void SetUpdating(SubViewport.UpdateMode mode)
    {
        _cameraDepth.RenderTargetUpdateMode = mode;
        _projectorDepth.RenderTargetUpdateMode = mode;
        _albedo.RenderTargetUpdateMode = mode;
    }

    private SubViewport AddView(Vector3 position, float verticalFovDegrees, bool depthPass)
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
            CullMask = depthPass ? 1u | (1u << (DepthLayer - 1)) : 1u,
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

    private void AddBodyModel(Vector3 position, float baseline)
    {
        // Only visible to the overview camera, not to the sensor itself.
        var material = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.3f, 0.5f) };
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(baseline + 0.08f, 0.06f, 0.08f), Material = material },
            Position = position + new Vector3(baseline / 2f, 0.04f, 0f),
            Layers = 1u << (ModelLayer - 1),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

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
