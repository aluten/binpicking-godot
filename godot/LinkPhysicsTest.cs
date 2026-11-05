#nullable enable

using System.Collections.Generic;
using Godot;

/// <summary>
/// Controleert de CAD-stap in Godot: import, schaal, botsingsvorm en stapelgedrag.
/// Laat schakels in een kleine bak vallen en tilt daarna de bovenste schakel op.
/// Start deze scène met F6. Resultaten staan in de Output.
/// </summary>
public partial class LinkPhysicsTest : Node3D
{
    private const string VisualPath = "res://generated/schakel_visual.glb";
    private const string LodPath = "res://generated/schakel_lod.glb";
    private const string CollisionPath = "res://generated/schakel_collision.glb";

    private const int LinkCount = 30;
    private const int RandomSeed = 1;
    private const float BinInnerSize = 0.5f;
    private const float WallHeight = 0.5f;
    private const float WallThickness = 0.3f;
    private const float FloorThickness = 0.5f; // Dik, zodat een schakel er niet doorheen kan worden gedrukt.
    private const float SpawnSpacing = 0.17f;
    private const float LinkMassKg = 0.15f; // Aanname voor de test, geen igus-waarde.
    private const float ShapeMargin = 0.001f;

    private static readonly Vector3 ExpectedSizeMm = new(110.2f, 54f, 122f);
    private const float SizeToleranceMm = 1f;

    private const double MaxSettleSeconds = 20.0;
    private const float SettledSpeed = 0.01f;
    private const float LiftHeight = 0.3f;
    private const float LiftSpeed = 0.1f;
    private const float MovedThreshold = 0.005f;

    // Zo ver mag het zichtbare model in de vloer zakken. De botsingsvorm wijkt tot 3 mm af van het
    // model, daar komt 1 mm speling bovenop. Meer dan dit wijst op te losse physics-instellingen.
    private const float MaxFloorPenetration = 0.004f;

    private enum Phase { Falling, Lifting, Holding, Done }

    private readonly List<RigidBody3D> _links = new();
    private readonly List<Vector3> _restPositions = new();
    private Phase _phase = Phase.Falling;
    private double _elapsed;
    private double _physicsMilliseconds;
    private double _worstPhysicsMilliseconds;
    private int _physicsSteps;
    private RigidBody3D? _lifted;
    private Vector3 _liftTarget;
    private bool _importPassed;
    private bool _settled;
    private Vector3[] _surfacePoints = System.Array.Empty<Vector3>();

    public override void _Ready()
    {
        GD.Print($"Physics-engine: {ProjectSettings.GetSetting("physics/3d/physics_engine")}, " +
                 $"penetration slop: {ProjectSettings.GetSetting("physics/jolt_physics_3d/simulation/penetration_slop")} m");

        Mesh? visual = LoadSingleMesh(VisualPath);
        Mesh? lod = LoadSingleMesh(LodPath);
        List<(Shape3D Shape, Transform3D Offset)> shapes = LoadCollisionShapes();

        if (visual == null || lod == null || shapes.Count == 0)
        {
            GD.PrintErr("RESULTAAT: NIET GESLAAGD, bestanden ontbreken. Zie docs/cad-pijplijn.md.");
            Finish();
            return;
        }

        _surfacePoints = visual.GetFaces();
        _importPassed = CheckImport(visual, shapes.Count);
        BuildEnvironment();
        SpawnLinks(lod, shapes);
    }

    public override void _PhysicsProcess(double delta)
    {
        _elapsed += delta;

        switch (_phase)
        {
            case Phase.Falling:
                if (_links.Count > 0 && (AllLinksSlow() || _elapsed > MaxSettleSeconds))
                {
                    FinishFalling();
                }
                break;
            case Phase.Lifting:
                MoveLiftedLink(delta);
                break;
            case Phase.Holding:
                if (_elapsed > 2.0)
                {
                    ReportLift();
                }
                break;
        }

        // Tijd van de vorige physics-stap volgens de engine.
        double step = Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0;
        _physicsMilliseconds += step;
        _worstPhysicsMilliseconds = Mathf.Max(_worstPhysicsMilliseconds, step);
        _physicsSteps++;
    }

    private static Mesh? LoadSingleMesh(string path)
    {
        var scene = ResourceLoader.Load<PackedScene>(path);
        if (scene == null)
        {
            GD.PrintErr($"Kan {path} niet laden.");
            return null;
        }

        Node root = scene.Instantiate();
        var found = new List<(Mesh Mesh, Transform3D Offset)>();
        CollectMeshes(root, Transform3D.Identity, found);
        root.Free();
        return found.Count > 0 ? found[0].Mesh : null;
    }

    private static List<(Shape3D Shape, Transform3D Offset)> LoadCollisionShapes()
    {
        var shapes = new List<(Shape3D, Transform3D)>();
        var scene = ResourceLoader.Load<PackedScene>(CollisionPath);
        if (scene == null)
        {
            GD.PrintErr($"Kan {CollisionPath} niet laden.");
            return shapes;
        }

        Node root = scene.Instantiate();
        var meshes = new List<(Mesh Mesh, Transform3D Offset)>();
        CollectMeshes(root, Transform3D.Identity, meshes);
        root.Free();

        foreach ((Mesh mesh, Transform3D offset) in meshes)
        {
            ConvexPolygonShape3D shape = mesh.CreateConvexShape(true, false);
            shape.Margin = ShapeMargin;
            shapes.Add((shape, offset));
        }
        return shapes;
    }

    private static void CollectMeshes(Node node, Transform3D parent, List<(Mesh, Transform3D)> result)
    {
        Transform3D current = parent;
        if (node is Node3D node3D)
        {
            current = parent * node3D.Transform;
        }
        if (node is MeshInstance3D { Mesh: not null } instance)
        {
            result.Add((instance.Mesh, current));
        }
        foreach (Node child in node.GetChildren())
        {
            CollectMeshes(child, current, result);
        }
    }

    private static bool CheckImport(Mesh visual, int hullCount)
    {
        Vector3 sizeMm = visual.GetAabb().Size * 1000f;
        Vector3 difference = (sizeMm - ExpectedSizeMm).Abs();
        bool passed = difference.X <= SizeToleranceMm
            && difference.Y <= SizeToleranceMm
            && difference.Z <= SizeToleranceMm;

        GD.Print($"{(passed ? "OK  " : "FOUT")} Afmeting: {sizeMm.X:F1} x {sizeMm.Y:F1} x {sizeMm.Z:F1} mm, " +
                 $"verwacht {ExpectedSizeMm.X:F1} x {ExpectedSizeMm.Y:F1} x {ExpectedSizeMm.Z:F1} mm");
        GD.Print($"INFO Botsingsdelen per schakel: {hullCount}");
        return passed;
    }

    private void BuildEnvironment()
    {
        AddBoxBody(new Vector3(1.5f, FloorThickness, 1.5f), new Vector3(0f, -FloorThickness / 2f, 0f));

        float offset = (BinInnerSize + WallThickness) / 2f;
        float length = BinInnerSize + 2f * WallThickness;
        float y = WallHeight / 2f;
        AddBoxBody(new Vector3(WallThickness, WallHeight, length), new Vector3(offset, y, 0f));
        AddBoxBody(new Vector3(WallThickness, WallHeight, length), new Vector3(-offset, y, 0f));
        AddBoxBody(new Vector3(length, WallHeight, WallThickness), new Vector3(0f, y, offset));
        AddBoxBody(new Vector3(length, WallHeight, WallThickness), new Vector3(0f, y, -offset));

        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55f, -35f, 0f), LightEnergy = 1.8f });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50f, 140f, 0f), LightEnergy = 0.8f });

        var camera = new Camera3D { Position = new Vector3(0.9f, 1.0f, 1.1f) };
        AddChild(camera);
        camera.LookAtFromPosition(camera.Position, new Vector3(0f, 0.2f, 0f), Vector3.Up);
        camera.MakeCurrent();
    }

    private void AddBoxBody(Vector3 size, Vector3 position)
    {
        var body = new StaticBody3D { Position = position };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        body.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = size,
                Material = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.6f, 0.62f) },
            },
        });
        AddChild(body);
    }

    private void SpawnLinks(Mesh lod, List<(Shape3D Shape, Transform3D Offset)> shapes)
    {
        var random = new RandomNumberGenerator { Seed = RandomSeed };
        const int perLayer = 4;

        for (int i = 0; i < LinkCount; i++)
        {
            int layer = i / perLayer;
            int cell = i % perLayer;
            var position = new Vector3(
                (cell % 2 - 0.5f) * SpawnSpacing,
                0.2f + layer * SpawnSpacing,
                (cell / 2 - 0.5f) * SpawnSpacing);

            var body = new RigidBody3D
            {
                Mass = LinkMassKg,
                ContinuousCd = true,
                Position = position,
                Rotation = new Vector3(
                    random.RandfRange(-Mathf.Pi, Mathf.Pi),
                    random.RandfRange(-Mathf.Pi, Mathf.Pi),
                    random.RandfRange(-Mathf.Pi, Mathf.Pi)),
            };
            body.AddChild(new MeshInstance3D { Mesh = lod });
            foreach ((Shape3D shape, Transform3D offset) in shapes)
            {
                body.AddChild(new CollisionShape3D { Shape = shape, Transform = offset });
            }
            AddChild(body);
            _links.Add(body);
        }
    }

    private bool AllLinksSlow()
    {
        if (_elapsed < 1.0)
        {
            return false;
        }
        foreach (RigidBody3D link in _links)
        {
            if (link.LinearVelocity.Length() > SettledSpeed)
            {
                return false;
            }
        }
        return true;
    }

    private void FinishFalling()
    {
        _settled = _elapsed <= MaxSettleSeconds;

        int outside = 0;
        float lowest = float.MaxValue;
        float highest = float.MinValue;
        foreach (RigidBody3D link in _links)
        {
            Vector3 p = link.GlobalPosition;
            if (Mathf.Abs(p.X) > BinInnerSize / 2f || Mathf.Abs(p.Z) > BinInnerSize / 2f)
            {
                outside++;
            }
            lowest = Mathf.Min(lowest, p.Y);
            highest = Mathf.Max(highest, p.Y);
            _restPositions.Add(p);
        }

        double average = _physicsSteps > 0 ? _physicsMilliseconds / _physicsSteps : 0.0;
        GD.Print($"{(_settled ? "OK  " : "FOUT")} Tot rust na {_elapsed:F1} s (maximaal {MaxSettleSeconds:F0} s)");
        GD.Print($"{(outside == 0 ? "OK  " : "FOUT")} Schakels met hun midden buiten de bak: {outside} van {_links.Count}");
        float penetration = DeepestFloorPenetration();
        bool aboveFloor = penetration <= MaxFloorPenetration;
        GD.Print($"{(aboveFloor ? "OK  " : "FOUT")} Diepst in de vloer: {penetration * 1000f:F1} mm " +
                 $"(maximaal {MaxFloorPenetration * 1000f:F0} mm)");
        GD.Print($"INFO Hoogte middens: laagste {lowest * 1000f:F0} mm, hoogste {highest * 1000f:F0} mm");
        GD.Print($"INFO Physics-tijd per stap: gemiddeld {average:F2} ms, hoogste {_worstPhysicsMilliseconds:F2} ms");

        _importPassed &= _settled && outside == 0 && aboveFloor;
        StartLift();
    }

    private float DeepestFloorPenetration()
    {
        float deepest = 0f;
        foreach (RigidBody3D link in _links)
        {
            Transform3D transform = link.GlobalTransform;
            foreach (Vector3 point in _surfacePoints)
            {
                deepest = Mathf.Max(deepest, -(transform * point).Y);
            }
        }
        return deepest;
    }

    private void StartLift()
    {
        RigidBody3D top = _links[0];
        foreach (RigidBody3D link in _links)
        {
            if (link.GlobalPosition.Y > top.GlobalPosition.Y)
            {
                top = link;
            }
        }

        _lifted = top;
        _lifted.FreezeMode = RigidBody3D.FreezeModeEnum.Kinematic;
        _lifted.Freeze = true;
        _liftTarget = top.GlobalPosition + Vector3.Up * LiftHeight;
        _phase = Phase.Lifting;
        _elapsed = 0.0;
    }

    private void MoveLiftedLink(double delta)
    {
        Vector3 next = _lifted!.GlobalPosition.MoveToward(_liftTarget, LiftSpeed * (float)delta);
        _lifted.GlobalPosition = next;
        if (next.IsEqualApprox(_liftTarget))
        {
            _phase = Phase.Holding;
            _elapsed = 0.0;
        }
    }

    private void ReportLift()
    {
        int moved = 0;
        float largest = 0f;
        float raised = 0f;
        for (int i = 0; i < _links.Count; i++)
        {
            if (_links[i] == _lifted)
            {
                continue;
            }
            Vector3 change = _links[i].GlobalPosition - _restPositions[i];
            if (change.Length() > MovedThreshold)
            {
                moved++;
            }
            largest = Mathf.Max(largest, change.Length());
            raised = Mathf.Max(raised, change.Y);
        }

        GD.Print($"INFO Optillen: {moved} van {_links.Count - 1} andere schakels bewogen meer dan " +
                 $"{MovedThreshold * 1000f:F0} mm, grootste verplaatsing {largest * 1000f:F1} mm, " +
                 $"hoogste stijging {raised * 1000f:F1} mm");
        GD.Print(_importPassed
            ? "RESULTAAT: GESLAAGD, import, schaal en stapelen werken."
            : "RESULTAAT: NIET GESLAAGD, zie de regels met FOUT.");
        Finish();
    }

    private void Finish()
    {
        _phase = Phase.Done;
        if (DisplayServer.GetName() == "headless")
        {
            GetTree().Quit();
        }
    }
}
