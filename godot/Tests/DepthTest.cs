using Godot;

/// <summary>
/// Verifies that depth from Godot arrives accurately in C#.
/// The camera looks straight down at objects of a known height.
/// Run this scene with F6. Results appear in the Output panel.
/// </summary>
public partial class DepthTest : Node3D
{
    private const int Width = 640;
    private const int Height = 480;
    private const float FovDegrees = 40f;
    private const float CameraHeight = 1.5f;

    // Range encoded into the colour channels, in metres.
    private const float EncodedRange = 4f;
    private const float ToleranceMeters = 0.001f;

    // Depth is split over two channels, so the limited precision of a
    // 16-bit float does not make the measurement inaccurate.
    private const string DepthShaderCode = @"
        shader_type spatial;
        render_mode unshaded, cull_disabled, depth_draw_never;

        uniform sampler2D depth_texture : hint_depth_texture, repeat_disable, filter_nearest;
        uniform float encoded_range = 4.0;

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

    private SubViewport _viewport = null!;

    public override async void _Ready()
    {
        BuildScene();

        // Wait until the SubViewport has certainly drawn a complete frame.
        for (int i = 0; i < 5; i++)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }

        Image image = _viewport.GetTexture().GetImage();
        GD.Print($"Beeld: {image.GetWidth()} x {image.GetHeight()}, formaat {image.GetFormat()}");

        // Focal length in pixels, to convert a world position into a pixel.
        float focalPixels = (Height / 2f) / Mathf.Tan(Mathf.DegToRad(FovDegrees / 2f));
        int smallCubePixelX = Width / 2 + Mathf.RoundToInt(focalPixels * 0.2f / 0.7f);

        bool allPassed = true;
        allPassed &= Check(image, "Grote balk, midden", Width / 2, Height / 2, 0.900f);
        allPassed &= Check(image, "Kleine balk, uit het midden", smallCubePixelX, Height / 2, 0.700f);
        allPassed &= Check(image, "Vloer, hoek van het beeld", 20, 20, 1.500f);

        GD.Print(allPassed
            ? "RESULTAAT: GESLAAGD, alle metingen binnen 1 mm."
            : "RESULTAAT: NIET GESLAAGD, zie de afwijkingen hierboven.");

        ShowDepthImage(image);
    }

    private void BuildScene()
    {
        _viewport = new SubViewport
        {
            Size = new Vector2I(Width, Height),
            OwnWorld3D = true,
            UseHdr2D = true, // Float buffer without sRGB conversion.
            Msaa3D = Viewport.Msaa.Disabled,
            ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled,
            UseTaa = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        AddChild(_viewport);

        var camera = new Camera3D
        {
            Fov = FovDegrees,
            Near = 0.1f,
            Far = 10f,
            Position = new Vector3(0f, CameraHeight, 0f),
            RotationDegrees = new Vector3(-90f, 0f, 0f),
        };
        _viewport.AddChild(camera);
        camera.MakeCurrent();

        // Floor at y = 0: expected depth 1.5 m.
        AddBox(new Vector3(5f, 0.01f, 5f), new Vector3(0f, -0.005f, 0f));
        // Top at y = 0.6: expected depth 0.9 m.
        AddBox(new Vector3(0.2f, 0.6f, 0.2f), new Vector3(0f, 0.3f, 0f));
        // Top at y = 0.8: expected depth 0.7 m.
        AddBox(new Vector3(0.1f, 0.8f, 0.1f), new Vector3(0.2f, 0.4f, 0f));

        var material = new ShaderMaterial
        {
            Shader = new Shader { Code = DepthShaderCode },
        };
        material.SetShaderParameter("encoded_range", EncodedRange);

        // Full-screen quad that writes depth as colour.
        var fullscreenQuad = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(2f, 2f) },
            MaterialOverride = material,
            ExtraCullMargin = 16384f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        camera.AddChild(fullscreenQuad);
    }

    private void AddBox(Vector3 size, Vector3 position)
    {
        _viewport.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = position,
        });
    }

    private static float ReadDepth(Image image, int x, int y)
    {
        Color c = image.GetPixel(x, y);
        // Round to remove the small float error in the coarse channel.
        float coarse = Mathf.Round(c.R * 255f) / 255f;
        return EncodedRange * (coarse + c.G / 255f);
    }

    private static bool Check(Image image, string name, int x, int y, float expected)
    {
        float measured = ReadDepth(image, x, y);
        float errorMm = (measured - expected) * 1000f;
        bool passed = Mathf.Abs(measured - expected) <= ToleranceMeters;

        GD.Print($"{(passed ? "OK  " : "FOUT")} {name}: pixel ({x}, {y}), " +
                 $"verwacht {expected:F3} m, gemeten {measured:F4} m, afwijking {errorMm:F2} mm");
        return passed;
    }

    private void ShowDepthImage(Image source)
    {
        // Near is light and far is dark, between 0.5 m and 1.6 m.
        Image grayscale = Image.CreateEmpty(Width, Height, false, Image.Format.L8);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                float t = Mathf.Clamp((1.6f - ReadDepth(source, x, y)) / 1.1f, 0f, 1f);
                grayscale.SetPixel(x, y, new Color(t, t, t));
            }
        }

        AddChild(new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(grayscale),
        });
    }
}
