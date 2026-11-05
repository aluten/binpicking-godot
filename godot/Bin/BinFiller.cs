#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static PalletDimensions;

/// <summary>
/// Fills the bin with the physics engine, in batches, because simulating hundreds of links at once
/// is too heavy. Settled links deep in the pile are frozen; links stuck in a wall fall again.
/// </summary>
public partial class BinFiller : Node
{
    public const int FullBinLinks = 720;

    public const int RandomSeed = 1;
    private const int BatchLinks = 24;
    private const int SpawnColumns = 6;
    private const int SpawnRows = 4;
    private const float SpawnClearance = 0.14f;
    private const float LinkMassKg = 0.15f; // Assumption, not an igus value.

    private const float FreezeDepth = 0.12f;
    private const float SettledSpeed = 0.02f;
    private const float MaxOverlapWhenFrozen = 0.003f;
    private const double MinBatchSeconds = 0.8;
    private const double MaxBatchSeconds = 4.0;
    private const double MaxExtraSettleSeconds = 30.0;

    private const float WallThickness = 2f; // Thick, so a link pushed into it is pushed back into the bin.
    private const float WallHeight = 3f;

    private readonly Mesh _linkMesh;
    private readonly List<(Shape3D Shape, Transform3D Offset)> _shapes;
    private readonly Vector3[] _surfacePoints;
    private readonly List<RigidBody3D> _active = new();
    private readonly List<RigidBody3D> _frozen = new();
    private readonly RandomNumberGenerator _random = new() { Seed = RandomSeed };

    private double _batchElapsed;
    private double _extraSettleSeconds;
    private double _simulatedSeconds;
    private ulong _startedMs;
    private int _spawned;
    private int _droppedAgain;
    private bool _stopRequested;
    private bool _finished;

    public BinFiller(Mesh linkMesh, List<(Shape3D Shape, Transform3D Offset)> shapes)
    {
        _linkMesh = linkMesh;
        _shapes = shapes;
        _surfacePoints = linkMesh.GetFaces().Distinct().ToArray();
    }

    /// <summary>Raised with a status text while filling.</summary>
    public event Action<string>? Progress;

    /// <summary>Raised once with the final link transforms.</summary>
    public event Action<List<Transform3D>>? Finished;

    /// <summary>Stops adding links. The links already falling land first.</summary>
    public void RequestStop()
    {
        _stopRequested = true;
        Progress?.Invoke($"Stoppen bij {_spawned} schakels: de laatste schakels landen...");
    }

    public override void _Ready()
    {
        BuildCollision();
        _startedMs = Time.GetTicksMsec();
        SpawnBatch();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_finished)
        {
            return;
        }

        _batchElapsed += delta;
        _simulatedSeconds += delta;
        if (_batchElapsed < MinBatchSeconds)
        {
            return;
        }

        bool settled = _active.All(link => link.LinearVelocity.Length() < SettledSpeed);
        if (!settled && _batchElapsed < MaxBatchSeconds)
        {
            return;
        }

        FreezeDeepLinks();
        if (_spawned >= FullBinLinks || _stopRequested)
        {
            List<RigidBody3D> stuck = _active.Where(link => !IsClearOfBin(link)).ToList();
            if (stuck.Count > 0 && _extraSettleSeconds < MaxExtraSettleSeconds)
            {
                stuck.ForEach(DropAgain);
                _extraSettleSeconds += _batchElapsed;
                _batchElapsed = 0.0;
                return;
            }
            Finish();
        }
        else
        {
            SpawnBatch();
        }
    }

    private void BuildCollision()
    {
        float halfX = InnerLength / 2f + WallThickness / 2f;
        float halfZ = InnerWidth / 2f + WallThickness / 2f;
        float y = WallHeight / 2f;

        AddStaticBox(new Vector3(8f, 2f, 8f), new Vector3(0f, -1f, 0f));
        AddStaticBox(new Vector3(WallThickness, WallHeight, 8f), new Vector3(halfX, y, 0f));
        AddStaticBox(new Vector3(WallThickness, WallHeight, 8f), new Vector3(-halfX, y, 0f));
        AddStaticBox(new Vector3(8f, WallHeight, WallThickness), new Vector3(0f, y, halfZ));
        AddStaticBox(new Vector3(8f, WallHeight, WallThickness), new Vector3(0f, y, -halfZ));
    }

    private void AddStaticBox(Vector3 size, Vector3 position)
    {
        var body = new StaticBody3D { Position = position };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(body);
    }

    private void SpawnBatch()
    {
        float startY = HighestLinkY() + SpawnClearance;
        float stepX = InnerLength / SpawnColumns;
        float stepZ = InnerWidth / SpawnRows;
        int cells = SpawnColumns * SpawnRows;
        int count = Mathf.Min(BatchLinks, FullBinLinks - _spawned);

        for (int i = 0; i < count; i++)
        {
            int cell = i % cells;
            int layer = i / cells;
            var position = new Vector3(
                (cell % SpawnColumns + 0.5f) * stepX - InnerLength / 2f,
                startY + layer * 0.2f,
                (cell / SpawnColumns + 0.5f) * stepZ - InnerWidth / 2f);
            AddChild(CreateLink(position));
        }

        _spawned += count;
        _batchElapsed = 0.0;
        Progress?.Invoke($"Bak vullen: {_spawned} van {FullBinLinks} schakels, {_active.Count} bewegend. R = stoppen");
        GD.Print($"Storten: {_spawned} geplaatst, {_active.Count} actief, {_simulatedSeconds:F0} s gesimuleerd");
    }

    private RigidBody3D CreateLink(Vector3 position)
    {
        var body = new RigidBody3D
        {
            Mass = LinkMassKg,
            ContinuousCd = true,
            Position = position,
            Rotation = new Vector3(
                _random.RandfRange(-Mathf.Pi, Mathf.Pi),
                _random.RandfRange(-Mathf.Pi, Mathf.Pi),
                _random.RandfRange(-Mathf.Pi, Mathf.Pi)),
        };
        body.AddChild(new MeshInstance3D { Mesh = _linkMesh });
        foreach ((Shape3D shape, Transform3D offset) in _shapes)
        {
            body.AddChild(new CollisionShape3D { Shape = shape, Transform = offset });
        }
        _active.Add(body);
        return body;
    }

    private float HighestLinkY()
    {
        float highest = 0f;
        foreach (RigidBody3D link in _active.Concat(_frozen))
        {
            highest = Mathf.Max(highest, link.GlobalPosition.Y);
        }
        return highest;
    }

    private void FreezeDeepLinks()
    {
        float limit = HighestLinkY() - FreezeDepth;
        foreach (RigidBody3D link in _active
            .Where(link => link.GlobalPosition.Y < limit && link.LinearVelocity.Length() < SettledSpeed).ToList())
        {
            if (IsClearOfBin(link))
            {
                link.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
                link.Freeze = true;
                _active.Remove(link);
                _frozen.Add(link);
            }
            else
            {
                DropAgain(link);
            }
        }
    }

    /// <summary>A link stuck in a wall or the floor does not come loose by itself, so it falls again.</summary>
    private void DropAgain(RigidBody3D link)
    {
        link.GlobalPosition = new Vector3(
            _random.RandfRange(-InnerLength / 2f + 0.1f, InnerLength / 2f - 0.1f),
            HighestLinkY() + SpawnClearance,
            _random.RandfRange(-InnerWidth / 2f + 0.1f, InnerWidth / 2f - 0.1f));
        link.LinearVelocity = Vector3.Zero;
        link.AngularVelocity = Vector3.Zero;
        _droppedAgain++;
    }

    private bool IsClearOfBin(RigidBody3D link)
    {
        Transform3D transform = link.GlobalTransform;
        foreach (Vector3 point in _surfacePoints)
        {
            Vector3 world = transform * point;
            if (world.Y < -MaxOverlapWhenFrozen
                || Mathf.Abs(world.X) > InnerLength / 2f + MaxOverlapWhenFrozen
                || Mathf.Abs(world.Z) > InnerWidth / 2f + MaxOverlapWhenFrozen)
            {
                return false;
            }
        }
        return true;
    }

    private void Finish()
    {
        _finished = true;
        ulong seconds = (Time.GetTicksMsec() - _startedMs) / 1000;
        GD.Print($"Vulling opgeslagen in {seconds} s rekentijd, {_simulatedSeconds:F0} s gesimuleerd, " +
                 $"{_droppedAgain} keer opnieuw laten vallen.");
        Finished?.Invoke(_active.Concat(_frozen).Select(link => link.GlobalTransform).ToList());
    }
}
