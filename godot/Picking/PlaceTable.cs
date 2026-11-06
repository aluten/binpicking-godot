using Godot;

/// <summary>The table next to the pallet where picked links are placed. Only for the eye.</summary>
public partial class PlaceTable : Node3D
{
    /// <summary>Height of the table top in metres. The table has no physics.</summary>
    public const float TopHeight = 0.4f;

    private const float Thickness = 0.04f;
    private static readonly Vector2 Size = new(0.6f, 0.6f);

    private readonly Vector3 _center;

    /// <param name="placePosition">The middle of the table, seen from above.</param>
    public PlaceTable(Vector3 placePosition)
    {
        _center = placePosition;
    }

    public override void _Ready()
    {
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = new Vector3(Size.X, Thickness, Size.Y),
                Material = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.5f, 0.4f) },
            },
            Position = new Vector3(_center.X, TopHeight - Thickness / 2f, _center.Z),
        });
    }
}
