using BinPicking.Core.Sensing;
using Godot;

/// <summary>
/// Shows the sensor in the overview: named bodies for camera and projector, and lines to the area each one
/// covers at rim height. The sensor itself does not see this model.
/// </summary>
public partial class SensorRigModel : Node3D
{
    private const float BodySize = 0.07f;
    private const float LabelHeight = 0.13f;
    private const float LabelSideOffset = 0.1f;
    private const int LabelFontSize = 64;
    private const float LabelPixelSize = 0.0015f;

    private static readonly Color CameraColor = new(0.15f, 0.55f, 1f);
    private static readonly Color ProjectorColor = new(1f, 0.55f, 0.1f);
    private static readonly Color MountColor = new(0.25f, 0.25f, 0.28f);

    private readonly Vector3 _cameraPosition;
    private readonly Vector3 _projectorPosition;
    private readonly float _halfWidthAtRim;
    private readonly float _halfHeightAtRim;
    private readonly float _distanceToRim;

    /// <param name="camera">The camera model, which gives the covered area.</param>
    /// <param name="cameraPosition">Position of the camera in world space.</param>
    /// <param name="projectorBaseline">Distance from camera to projector, in metres.</param>
    /// <param name="distanceToRim">Distance from the camera down to the rim, in metres.</param>
    public SensorRigModel(PinholeCamera camera, Vector3 cameraPosition, float projectorBaseline, float distanceToRim)
    {
        _cameraPosition = cameraPosition;
        _projectorPosition = cameraPosition + Vector3.Right * projectorBaseline;
        _distanceToRim = distanceToRim;
        _halfWidthAtRim = camera.Cx / camera.Fx * distanceToRim;
        _halfHeightAtRim = camera.Cy / camera.Fy * distanceToRim;
    }

    public override void _Ready()
    {
        AddBody(_cameraPosition, CameraColor);
        AddBody(_projectorPosition, ProjectorColor);
        AddMount();
        AddLabel("Camera", _cameraPosition + new Vector3(-LabelSideOffset, LabelHeight, 0f), CameraColor);
        AddLabel("Projector", _projectorPosition + new Vector3(LabelSideOffset, LabelHeight, 0f), ProjectorColor);
        AddCoverageLines(_cameraPosition, CameraColor);
        AddCoverageLines(_projectorPosition, ProjectorColor);
    }

    private void AddBody(Vector3 position, Color color)
    {
        AddVisible(new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = Vector3.One * BodySize,
                Material = new StandardMaterial3D { AlbedoColor = color },
            },
            Position = position,
        });
    }

    private void AddMount()
    {
        float length = _projectorPosition.X - _cameraPosition.X;
        AddVisible(new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = new Vector3(length, 0.015f, 0.03f),
                Material = new StandardMaterial3D { AlbedoColor = MountColor },
            },
            Position = _cameraPosition + new Vector3(length / 2f, BodySize / 2f + 0.008f, 0f),
        });
    }

    private void AddLabel(string text, Vector3 position, Color color)
    {
        AddVisible(new Label3D
        {
            Text = text,
            Position = position,
            FontSize = LabelFontSize,
            PixelSize = LabelPixelSize,
            OutlineSize = 16,
            Modulate = color,
            OutlineModulate = Colors.White,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
        });
    }

    /// <summary>Draws lines from a body to the corners of the area at rim height, and that area's outline.</summary>
    private void AddCoverageLines(Vector3 origin, Color color)
    {
        float rimY = origin.Y - _distanceToRim;
        var corners = new[]
        {
            new Vector3(origin.X - _halfWidthAtRim, rimY, origin.Z - _halfHeightAtRim),
            new Vector3(origin.X + _halfWidthAtRim, rimY, origin.Z - _halfHeightAtRim),
            new Vector3(origin.X + _halfWidthAtRim, rimY, origin.Z + _halfHeightAtRim),
            new Vector3(origin.X - _halfWidthAtRim, rimY, origin.Z + _halfHeightAtRim),
        };

        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        for (int i = 0; i < corners.Length; i++)
        {
            mesh.SurfaceAddVertex(origin);
            mesh.SurfaceAddVertex(corners[i]);
            mesh.SurfaceAddVertex(corners[i]);
            mesh.SurfaceAddVertex(corners[(i + 1) % corners.Length]);
        }
        mesh.SurfaceEnd();

        AddVisible(new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        });
    }

    /// <summary>Adds an object on the layer that the overview camera sees and the sensor does not.</summary>
    private void AddVisible(VisualInstance3D instance)
    {
        instance.Layers = 1u << (DepthSensorRig.ModelLayer - 1);
        if (instance is GeometryInstance3D geometry)
        {
            geometry.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        AddChild(instance);
    }
}
