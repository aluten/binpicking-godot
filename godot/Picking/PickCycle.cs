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
/// <para>
/// This is the simulated counterpart of a robot program. With a real system the moves go to the robot, the seal
/// check comes from a vacuum sensor, and <see cref="PickRegion"/> and <see cref="VacuumHold"/> no longer exist.
/// </para>
/// </summary>
public partial class PickCycle : Node3D
{
    // Motion of the tool, in metres and metres per second. These are choices for the demo.
    private const float ApproachDistance = 0.12f; // Start this far above the pick point, along the normal.
    private const float DescendSpeed = 0.15f;
    private const float LiftSpeed = 0.15f;
    private const float CarrySpeed = 0.45f;
    private const float ClearAboveRim = 0.3f; // Lift this far above the rim before moving sideways.

    /// <summary>Links whose origin is this close to the pick point are tested for the contact ring.</summary>
    private const float SearchRadius = 0.14f;

    // When another link counts as pulled along or disturbed, in metres.
    private const float PulledAlongRise = 0.02f;
    private const float DisturbedDistance = 0.005f;

    // The bin counts as settled below this speed, but no sooner than the minimum and no later than the maximum.
    private const float SettledSpeed = 0.02f;
    private const double MinSettleSeconds = 0.5;
    private const double MaxSettleSeconds = 3.0;

    /// <summary>Time the links stay damped after release, so overlaps from the stored fill can relax.</summary>
    private const double EngageSeconds = 0.5;

    /// <summary>The stages of a pick, in order. A failure jumps from any stage to Retreat.</summary>
    private enum Stage
    {
        Descend,   // The tool moves to the pick point. Everything is still frozen.
        CheckSeal, // Is the rim of the cup touching one link all the way round?
        Engage,    // Links are released and damped; the vacuum holds, but the tool does not move yet.
        Lift,      // Straight up to above the rim.
        Carry,     // Sideways, and down to the table.
        Retreat,   // Up and away from the part.
        Settle,    // Waits until the disturbed links have stopped moving.
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
    private int _movedBeforeLift;
    private bool _lostBeforeLift;
    private PickOutcome _outcome;
    private readonly List<Vector3> _carryPath = new();
    private readonly HashSet<int> _pulledIndices = new();

    /// <param name="transforms">All links in the bin.</param>
    /// <param name="lod">The simplified link mesh, for drawing and for the physics region.</param>
    /// <param name="full">The full link mesh, which decides whether the contact ring touches.</param>
    /// <param name="shapes">The convex collision shapes of a link.</param>
    /// <param name="grasp">The pick point in camera space.</param>
    /// <param name="cup">What the cup can hold.</param>
    /// <param name="toolDiameter">Width of the holder above the cup.</param>
    /// <param name="placePosition">Where to put the link down.</param>
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

        // Tool Y lies along the surface normal. The helper only has to be not parallel to the normal.
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
                    CountMovedBeforeLift();
                    StartLift();
                }
                else if (_stage != Stage.Engage)
                {
                    CountMovedBeforeLift();
                    _lostBeforeLift = true;
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
                if (_settleSeconds > MinSettleSeconds && (_region.AllSettled(SettledSpeed, except) || _settleSeconds > MaxSettleSeconds))
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

    /// <summary>Moves the tool towards the target at the current speed. Returns true on arrival.</summary>
    private bool MoveTool(double delta)
    {
        Vector3 next = _tool.GlobalPosition.MoveToward(_toolTarget, _toolSpeed * (float)delta);
        _tool.GlobalPosition = next;
        return next.IsEqualApprox(_toolTarget);
    }

    /// <summary>Decides from the contact ring whether the cup seals, and if so releases the links and starts the vacuum.</summary>
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

    /// <summary>Nothing has pulled on the links yet, so what moved here is a simulation artefact.</summary>
    private void CountMovedBeforeLift()
    {
        _movedBeforeLift = _region.Active.Keys.Count(i => i != _target && _region.MovedDistance(i) > DisturbedDistance);
    }

    private void StartLift()
    {
        _region.EndDamping();
        _stage = Stage.Lift;
        _toolSpeed = LiftSpeed;
        _toolTarget = new Vector3(_point.X, RimHeight + ClearAboveRim, _point.Z);
        Lifting?.Invoke();
    }

    /// <summary>Lets the vacuum act for one step, and starts the retreat when it loses the link.</summary>
    private void HoldLink(double delta)
    {
        if (!_hold!.Step(_tool.GlobalTransform, delta))
        {
            FailAndRetreat(PickOutcome.LostDuringLift);
        }
    }

    /// <summary>Remembers every other link that has risen noticeably during lifting.</summary>
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

    /// <summary>Collects the new positions of the links and reports the result.</summary>
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

        var result = new PickResult(_outcome, _ringShare, _pulledAlong, disturbed, _grasp.CenterOffset, _movedBeforeLift, _lostBeforeLift);
        Finished?.Invoke(result, remaining, placed);
    }
}
