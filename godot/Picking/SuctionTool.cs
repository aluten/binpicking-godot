using Godot;

/// <summary>
/// The suction cup with its holder. Its origin is the contact point, and its Y axis points away from the part.
/// The holder pushes links aside in the physics; the sensor does not see the tool.
/// </summary>
public partial class SuctionTool : AnimatableBody3D
{
    private const float CupHeight = 0.03f;
    private const float HolderLength = 0.3f;

    private readonly float _cupRadius;
    private readonly float _holderRadius;

    public SuctionTool(float cupDiameter, float holderDiameter)
    {
        _cupRadius = cupDiameter / 2f;
        _holderRadius = holderDiameter / 2f;
        SyncToPhysics = false;
    }

    public override void _Ready()
    {
        AddVisual(_cupRadius, CupHeight, new Color(0.95f, 0.75f, 0.1f), CupHeight / 2f);
        AddVisual(_holderRadius, HolderLength, new Color(0.7f, 0.72f, 0.75f), CupHeight + HolderLength / 2f);
        AddChild(new CollisionShape3D
        {
            Shape = new CylinderShape3D { Radius = _holderRadius, Height = HolderLength },
            Position = Vector3.Up * (CupHeight + HolderLength / 2f),
        });
    }

    private void AddVisual(float radius, float height, Color color, float y)
    {
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = radius,
                BottomRadius = radius,
                Height = height,
                Material = new StandardMaterial3D { AlbedoColor = color },
            },
            Position = Vector3.Up * y,
            Layers = 1u << (DepthSensorRig.ModelLayer - 1),
        });
    }
}
