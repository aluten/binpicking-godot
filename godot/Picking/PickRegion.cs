#nullable enable

using System.Collections.Generic;
using System.Linq;
using Godot;
using static PalletDimensions;

/// <summary>
/// The physics around one pick. Only links near the pick point can move; links further away are static
/// support; the rest are left out. This keeps a pick fast in a bin with hundreds of links.
/// Everything stays frozen until <see cref="Release"/>, so a link does not drift before the cup touches it.
/// </summary>
public partial class PickRegion : Node3D
{
    public const float DefaultActiveRadius = 0.25f;
    public const float DefaultActiveDepth = 0.15f;
    public const float DefaultSupportRadius = 0.55f;

    private const float LinkMassKg = 0.15f; // Assumption, not an igus value.
    private const float WallThickness = 0.3f;
    private const float ReleaseDamping = 30f;

    private readonly IReadOnlyList<Transform3D> _start;
    private readonly List<(Shape3D Shape, Transform3D Offset)> _shapes;
    private readonly Mesh _mesh;
    private readonly Vector3 _pickPoint;
    private readonly float _activeRadius;
    private readonly float _activeDepth;
    private readonly float _supportRadius;

    public PickRegion(
        IReadOnlyList<Transform3D> transforms,
        Mesh mesh,
        List<(Shape3D Shape, Transform3D Offset)> shapes,
        Vector3 pickPoint,
        float activeRadius = DefaultActiveRadius,
        float activeDepth = DefaultActiveDepth,
        float supportRadius = DefaultSupportRadius)
    {
        _activeRadius = activeRadius;
        _activeDepth = activeDepth;
        _supportRadius = supportRadius;
        _start = transforms;
        _mesh = mesh;
        _shapes = shapes;
        _pickPoint = pickPoint;
    }

    /// <summary>Bodies that can move, by index in the original transform list.</summary>
    public Dictionary<int, RigidBody3D> Active { get; } = new();

    /// <summary>Indices drawn by this region, which the fill view must hide.</summary>
    public HashSet<int> Included { get; } = new();

    public override void _Ready()
    {
        BuildBin();
        for (int i = 0; i < _start.Count; i++)
        {
            Vector3 origin = _start[i].Origin;
            float distance = new Vector2(origin.X - _pickPoint.X, origin.Z - _pickPoint.Z).Length();
            if (distance > _supportRadius)
            {
                continue;
            }
            bool active = distance <= _activeRadius && origin.Y >= _pickPoint.Y - _activeDepth;
            Node3D body = active ? CreateActive(i) : CreateSupport(i);
            AddChild(body);
            Included.Add(i);
        }
    }

    /// <summary>
    /// Lets the active links move. Restored links overlap slightly, which would make them jump when they
    /// are released, so they start heavily damped until <see cref="EndDamping"/>.
    /// </summary>
    public void Release()
    {
        foreach (RigidBody3D body in Active.Values)
        {
            body.LinearDamp = ReleaseDamping;
            body.AngularDamp = ReleaseDamping;
            body.Freeze = false;
        }
    }

    public void EndDamping()
    {
        foreach (RigidBody3D body in Active.Values)
        {
            body.LinearDamp = 0f;
            body.AngularDamp = 0f;
        }
    }

    public float MovedDistance(int index) => Active[index].GlobalPosition.DistanceTo(_start[index].Origin);

    public float RaisedDistance(int index) => Active[index].GlobalPosition.Y - _start[index].Origin.Y;

    public bool AllSettled(float speed, int? except) => Active
        .Where(pair => pair.Key != except)
        .All(pair => pair.Value.LinearVelocity.Length() < speed);

    private RigidBody3D CreateActive(int index)
    {
        var body = new RigidBody3D
        {
            Mass = LinkMassKg,
            ContinuousCd = true,
            Transform = _start[index],
            FreezeMode = RigidBody3D.FreezeModeEnum.Static,
            Freeze = true,
        };
        AddLinkParts(body);
        Active[index] = body;
        return body;
    }

    private StaticBody3D CreateSupport(int index)
    {
        var body = new StaticBody3D { Transform = _start[index] };
        AddLinkParts(body);
        return body;
    }

    private void AddLinkParts(Node3D body)
    {
        body.AddChild(new MeshInstance3D { Mesh = _mesh });
        foreach ((Shape3D shape, Transform3D offset) in _shapes)
        {
            body.AddChild(new CollisionShape3D { Shape = shape, Transform = offset });
        }
    }

    private void BuildBin()
    {
        float halfX = InnerLength / 2f + WallThickness / 2f;
        float halfZ = InnerWidth / 2f + WallThickness / 2f;
        float y = RimHeight / 2f;
        AddStaticBox(new Vector3(3f, 1f, 3f), new Vector3(0f, -0.5f, 0f));
        AddStaticBox(new Vector3(WallThickness, RimHeight, CollarOuterWidth + 2f * WallThickness), new Vector3(halfX, y, 0f));
        AddStaticBox(new Vector3(WallThickness, RimHeight, CollarOuterWidth + 2f * WallThickness), new Vector3(-halfX, y, 0f));
        AddStaticBox(new Vector3(CollarOuterLength + 2f * WallThickness, RimHeight, WallThickness), new Vector3(0f, y, halfZ));
        AddStaticBox(new Vector3(CollarOuterLength + 2f * WallThickness, RimHeight, WallThickness), new Vector3(0f, y, -halfZ));
    }

    private void AddStaticBox(Vector3 size, Vector3 position)
    {
        var body = new StaticBody3D { Position = position };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(body);
    }
}
