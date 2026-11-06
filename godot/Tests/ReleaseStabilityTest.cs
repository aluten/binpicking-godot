#nullable enable

using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Measures physics artefacts: releases links around several points without touching anything.
/// A stable pile hardly moves, so any movement here comes from the simulation and not from a pick.
/// Run this scene with F6. Results appear in the Output panel.
/// </summary>
public partial class ReleaseStabilityTest : Node3D
{
    private const int PointCount = 8;
    private const float MinPointDistance = 0.2f;
    private const double DampedSeconds = 0.5;
    private const double MeasureAfterSeconds = 2.5;
    private const float MovedLimit = 0.005f;

    private readonly List<int> _movedCounts = new();
    private readonly List<float> _largestMoves = new();
    private readonly List<int> _activeCounts = new();
    private List<Transform3D> _transforms = new();
    private List<Vector3> _points = new();
    private List<(Shape3D Shape, Transform3D Offset)> _shapes = new();
    private Mesh _mesh = null!;
    private PickRegion? _region;
    private double _elapsed;
    private bool _dampingEnded;
    private int _pointIndex;

    public override void _Ready()
    {
        _mesh = LinkAssets.LoadSingleMesh(LinkAssets.LodPath)!;
        _shapes = LinkAssets.LoadCollisionShapes();
        _transforms = BinFillStore.Load();
        _points = ChoosePoints();
        StartPoint();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_region == null)
        {
            return;
        }

        _elapsed += delta;
        if (!_dampingEnded && _elapsed >= DampedSeconds)
        {
            _region.EndDamping();
            _dampingEnded = true;
        }

        if (_elapsed >= MeasureAfterSeconds)
        {
            FinishPoint();
        }
    }

    /// <summary>The highest links, spread over the bin, as a pick would prefer them.</summary>
    private List<Vector3> ChoosePoints()
    {
        var points = new List<Vector3>();
        foreach (Transform3D t in _transforms.OrderByDescending(t => t.Origin.Y))
        {
            if (points.All(p => p.DistanceTo(t.Origin) >= MinPointDistance))
            {
                points.Add(t.Origin);
                if (points.Count == PointCount)
                {
                    break;
                }
            }
        }
        return points;
    }

    private void StartPoint()
    {
        _region = new PickRegion(_transforms, _mesh, _shapes, _points[_pointIndex]);
        AddChild(_region);
        _region.Release();
        _elapsed = 0.0;
        _dampingEnded = false;
    }

    private void FinishPoint()
    {
        PickRegion region = _region!;
        List<float> moves = region.Active.Keys.Select(region.MovedDistance).ToList();
        int moved = moves.Count(m => m > MovedLimit);
        float largest = moves.Count > 0 ? moves.Max() : 0f;
        _movedCounts.Add(moved);
        _largestMoves.Add(largest);
        _activeCounts.Add(moves.Count);
        GD.Print($"Punt {_pointIndex + 1}: {moved} van {moves.Count} schakels meer dan 5 mm bewogen, grootste {largest * 1000f:F1} mm");

        region.QueueFree();
        _region = null;
        _pointIndex++;
        if (_pointIndex < _points.Count)
        {
            StartPoint();
        }
        else
        {
            Report();
        }
    }

    private void Report()
    {
        GD.Print($"RESULTAAT schijnbewegingen: gemiddeld {_movedCounts.Average():F1} van {_activeCounts.Average():F0} schakels bewogen, " +
                 $"grootste verplaatsing {_largestMoves.Max() * 1000f:F1} mm, gemiddeld grootste {_largestMoves.Average() * 1000f:F1} mm");
        if (DisplayServer.GetName() == "headless")
        {
            GetTree().Quit();
        }
    }
}
