using BinPicking.Core.Picking;
using Godot;

/// <summary>
/// Holds a link against the cup with a limited force and torque, instead of attaching it rigidly.
/// A hooked or off-centre link can therefore come off, as with a real vacuum.
/// </summary>
public sealed class VacuumHold
{
    private const float PositionGain = 0.3f;
    private const float RotationGain = 0.1f;
    private const float LostDistance = 0.012f;
    private const float LostAngleRadians = 0.21f;
    private const double LostAfterSeconds = 0.1;

    private readonly RigidBody3D _link;
    private readonly Transform3D _linkInTool;
    private readonly Vector3 _cupInLink;
    private readonly float _force;
    private readonly float _torque;
    private Vector3 _previousToolPosition;
    private double _overLimitSeconds;

    public VacuumHold(RigidBody3D link, Transform3D tool, SuctionCupSpecification cup)
    {
        _link = link;
        _linkInTool = tool.AffineInverse() * link.GlobalTransform;
        _cupInLink = link.GlobalTransform.AffineInverse() * tool.Origin;
        _previousToolPosition = tool.Origin;
        _force = cup.HoldingForce;
        _torque = cup.HoldingTorque;
    }

    /// <summary>Applies one physics step. Returns false once the vacuum is lost.</summary>
    public bool Step(Transform3D tool, double delta)
    {
        float dt = (float)delta;
        Transform3D desired = tool * _linkInTool;
        Transform3D current = _link.GlobalTransform;
        Vector3 positionError = desired.Origin - current.Origin;

        // Velocity servo: stable for a light body, unlike a stiff spring.
        Vector3 toolVelocity = (tool.Origin - _previousToolPosition) / dt;
        _previousToolPosition = tool.Origin;
        Vector3 wantedVelocity = toolVelocity + positionError * PositionGain / dt;
        Vector3 gravityCompensation = Vector3.Up * (float)_link.Mass * 9.8f * dt;
        Vector3 impulse = (wantedVelocity - _link.LinearVelocity) * (float)_link.Mass + gravityCompensation;
        impulse = impulse.LimitLength(_force * dt);
        _link.ApplyCentralImpulse(impulse);

        Quaternion rotationError = (desired.Basis * current.Basis.Inverse()).GetRotationQuaternion().Normalized();
        float angle = rotationError.GetAngle();
        if (angle > Mathf.Pi)
        {
            angle = Mathf.Tau - angle;
        }
        Vector3 axis = angle > 1e-5f ? rotationError.GetAxis() : Vector3.Up;

        // A force away from the centre of the link uses up part of the available torque.
        Vector3 lever = current * _cupInLink - current.Origin;
        float usedTorque = lever.Cross(impulse / dt).Length();
        float availableTorque = Mathf.Max(_torque - usedTorque, 0f);

        // Use the inertia of the physics engine: an estimate makes the servo oscillate.
        Basis inertia = PhysicsServer3D.BodyGetDirectState(_link.GetRid()).InverseInertiaTensor.Inverse();
        Vector3 wantedSpin = axis * angle * RotationGain / dt;
        Vector3 torqueImpulse = (inertia * (wantedSpin - _link.AngularVelocity)).LimitLength(availableTorque * dt);
        _link.ApplyTorqueImpulse(torqueImpulse);

        bool overLimit = positionError.Length() > LostDistance || angle > LostAngleRadians;
        _overLimitSeconds = overLimit ? _overLimitSeconds + delta : 0.0;
        return _overLimitSeconds < LostAfterSeconds;
    }
}
