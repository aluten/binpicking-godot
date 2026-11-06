#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using BinPicking.Core.Grasping;
using BinPicking.Core.Picking;
using Godot;
using static PalletDimensions;

/// <summary>
/// Performs one pick: approach, check the seal, lift, carry and release. Reports the outcome and the
/// new link transforms once everything has settled.
/// </summary>
public partial class PickCycle : Node3D
{
    private const float ApproachDistance = 0.12f;
    private const float DescendSpeed = 0.15f;
    private const float LiftSpeed = 0.15f;
    private const float CarrySpeed = 0.45f;
    private const float ClearAboveRim = 0.3f;
    private const float SearchRadius = 0.14f;
    private const float PulledAlongRise = 0.02f;
    private const float DisturbedDistance = 0.005f;
    private const float SettledSpeed = 0.02f;
    private const double MaxSettleSeconds = 3.0;
    private const double EngageSeconds = 0.5;

    private enum Stage
    {
        Descend,
        CheckSeal,
        Engage,
        Lift,
        Carry,
        Retreat,
        Settle,
        Done,
    }

    private readonly List<Transform3D> _transforms;
    private readonly Mesh _lod;
    private readonly Shape3D _fullShape;
    private readonly List<(Shape3D Shape, Transform3D Offset)> _shapes;
    private readonly GraspCandidate _grasp;
    private readonly SuctionCupSpecification _cup;
    private readonly float _toolDiameter;
    private readonly Vector3 _point;
    private readonly Vector3 _normal;
    private readonly Vector3 _placePosition;

    private PickRegion _region = null!;
    private SuctionTool _tool = null!;
    private Basis _toolBasis;
    private Stage _stage = Stage.Descend;
    private Vector3 _toolTarget;
    private float _toolSpeed = DescendSpeed;
    private int _target = -1;
    private VacuumHold? _hold;
    private float _ringShare;
    private int _pulledAlong;
    private double _settleSeconds;
    private double _engageSeconds;
    private PickOutcome _outcome;
    private readonly List<Vector3> _carryPath = new();

    public PickCycle(
        List<Transform3D> transforms, Mesh lod, Mesh full, List<(Shape3D Shape, Transform3D Offset)> shapes,
        GraspCandidate grasp, SuctionCupSpecification cup, float toolDiameter, Vector3 placePosition)
    {
        _transforms = transforms;
        _lod = lod;
        _fullShape = full.CreateTrimeshShape();
        _shapes = shapes;
        _grasp = grasp;
        _cup = cup;
        _toolDiameter = toolDiameter;
        _placePosition = placePosition;
        _point = SensorMount.ToWorldPoint(grasp.X, grasp.Y, grasp.Z);
        _normal = SensorMount.ToWorldDirection(grasp.NormalX, grasp.NormalY, grasp.NormalZ).Normalized();
    }

    /// <summary>Raised when the region has been built, with the links the fill view must hide.</summary>
    public event Action<HashSet<int>>? RegionReady;

    /// <summary>Raised at the moment the link is lifted, which is useful for a screenshot.</summary>
    public event Action? Lifting;

    /// <summary>Raised at the end with the result and the transforms of the links left in the bin.</summary>
    public event Action<PickResult, List<Transform3D>, Transform3D?>? Finished;

    public override void _Ready()
    {
        _region = new PickRegion(_transforms, _lod, _shapes, _point);
        AddChild(_region);
        RegionReady?.Invoke(_region.Included);

        Vector3 helper = Mathf.Abs(_normal.Dot(Vector3.Forward)) > 0.9f ? Vector3.Right : Vector3.Forward;
        Vector3 x = helper.Cross(_normal).Normalized();
        _toolBasis = new Basis(x, _normal, x.Cross(_normal));
        _tool = new SuctionTool(_cup.Diameter, _toolDiameter);
        AddChild(_tool);
        _tool.GlobalTransform = new Transform3D(_toolBasis, _point + _normal * ApproachDistance);
        _toolTarget = _point;
    }

    public override void _PhysicsProcess(double delta)
    {
        switch (_stage)
        {
            case Stage.Descend:
                if (MoveTool(delta))
                {
                    _stage = Stage.CheckSeal;
                }
                break;
            case Stage.CheckSeal:
                CheckSeal();
                break;
            case Stage.Engage:
                HoldLink(delta);
                _engageSeconds += delta;
                if (_stage == Stage.Engage && _engageSeconds >= EngageSeconds)
                {
                    StartLift();
                }
                break;
            case Stage.Lift:
                HoldLink(delta);
                TrackPulledAlong();
                if (_stage == Stage.Lift && MoveTool(delta))
                {
                    StartCarry();
                }
                break;
            case Stage.Carry:
                HoldLink(delta);
                if (_stage == Stage.Carry && MoveTool(delta))
                {
                    NextCarryPoint();
                }
                break;
            case Stage.Retreat:
                if (MoveTool(delta))
                {
                    _stage = Stage.Settle;
                    _settleSeconds = 0.0;
                }
                break;
            case Stage.Settle:
                _settleSeconds += delta;
                int? except = _outcome == PickOutcome.Placed ? _target : null;
                if (_settleSeconds > 0.5 && (_region.AllSettled(SettledSpeed, except) || _settleSeconds > MaxSettleSeconds))
                {
                    Finish();
                }
                break;
        }
    }

    /// <summary>
    /// Finds the link under the cup: the nearby link whose full mesh touches most of the contact ring.
    /// The simulation knows this; a real system only finds out whether the vacuum holds.
    /// </summary>
    private (int Link, List<float?> Ring) FindLinkUnderCup()
    {
        int best = -1;
        float bestShare = -1f;
        List<float?> bestRing = new();
        foreach (int index in _region.Included)
        {
            Transform3D transform = _transforms[index];
            if (transform.Origin.DistanceTo(_point) > SearchRadius)
            {
                continue;
            }

            List<float?> ring = CupContactProbe.MeasureRing(this, _fullShape, transform, _point, _normal, _cup.Diameter / 2f);
            float share = CupContact.RingShare(ring, _cup.ContactTolerance);
            if (share > bestShare)
            {
                best = index;
                bestShare = share;
                bestRing = ring;
            }
        }
        return (best, bestRing);
    }

    private bool MoveTool(double delta)
    {
        Vector3 next = _tool.GlobalPosition.MoveToward(_toolTarget, _toolSpeed * (float)delta);
        _tool.GlobalPosition = next;
        return next.IsEqualApprox(_toolTarget);
    }

    private void CheckSeal()
    {
        (int found, List<float?> ring) = FindLinkUnderCup();
        _target = found;
        _ringShare = CupContact.RingShare(ring, _cup.ContactTolerance);
        if (_target < 0 || _ringShare <= 0f)
        {
            FailAndRetreat(PickOutcome.NoPartAtPoint);
            return;
        }

        if (!CupContact.Seals(ring, _cup))
        {
            FailAndRetreat(PickOutcome.NoSeal);
            return;
        }

        if (!_region.Active.TryGetValue(_target, out RigidBody3D? link))
        {
            // The link is static support in this region, so it cannot be lifted here.
            FailAndRetreat(PickOutcome.LostDuringLift);
            return;
        }

        _region.Release();
        _tool.AddCollisionExceptionWith(link);
        _hold = new VacuumHold(link, _tool.GlobalTransform, _cup);
        _stage = Stage.Engage;
    }

    private void StartLift()
    {
        _region.EndDamping();
        _stage = Stage.Lift;
        _toolSpeed = LiftSpeed;
        _toolTarget = new Vector3(_point.X, RimHeight + ClearAboveRim, _point.Z);
        Lifting?.Invoke();
    }

    private void HoldLink(double delta)
    {
        if (!_hold!.Step(_tool.GlobalTransform, delta))
        {
            FailAndRetreat(PickOutcome.LostDuringLift);
        }
    }

    private void TrackPulledAlong()
    {
        foreach (int index in _region.Active.Keys)
        {
            if (index != _target && _region.RaisedDistance(index) > PulledAlongRise)
            {
                _pulledIndices.Add(index);
            }
        }
        _pulledAlong = _pulledIndices.Count;
    }

    private readonly HashSet<int> _pulledIndices = new();

    private void StartCarry()
    {
        _stage = Stage.Carry;
        _toolSpeed = CarrySpeed;
        _carryPath.Add(new Vector3(_placePosition.X, RimHeight + ClearAboveRim, _placePosition.Z));
        _carryPath.Add(_placePosition);
        NextCarryPoint();
    }

    private void NextCarryPoint()
    {
        if (_carryPath.Count == 0)
        {
            Release();
            return;
        }
        _toolTarget = _carryPath[0];
        _carryPath.RemoveAt(0);
    }

    private void Release()
    {
        // The table is only for the eye, so the link stays where it was put down.
        RigidBody3D link = _region.Active[_target];
        link.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
        link.Freeze = true;
        _hold = null;
        _outcome = PickOutcome.Placed;
        StartRetreat();
    }

    private void FailAndRetreat(PickOutcome outcome)
    {
        _hold = null;
        _outcome = outcome;
        StartRetreat();
    }

    private void StartRetreat()
    {
        _stage = Stage.Retreat;
        _toolSpeed = CarrySpeed;
        _toolTarget = _tool.GlobalPosition + Vector3.Up * ApproachDistance;
    }

    private void Finish()
    {
        _stage = Stage.Done;
        var remaining = new List<Transform3D>();
        Transform3D? placed = null;
        int disturbed = 0;
        for (int i = 0; i < _transforms.Count; i++)
        {
            if (!_region.Active.TryGetValue(i, out RigidBody3D? body))
            {
                remaining.Add(_transforms[i]);
                continue;
            }

            if (i == _target && _outcome == PickOutcome.Placed)
            {
                placed = body.GlobalTransform;
                continue;
            }

            remaining.Add(body.GlobalTransform);
            if (i != _target && _region.MovedDistance(i) > DisturbedDistance)
            {
                disturbed++;
            }
        }

        var result = new PickResult(_outcome, _ringShare, _pulledAlong, disturbed, _grasp.CenterOffset);
        Finished?.Invoke(result, remaining, placed);
    }
}
