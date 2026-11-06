using Godot;

/// <summary>
/// Camera that orbits a point with the mouse.
/// The left or right button rotates, the middle button pans and the wheel zooms.
/// A view can have a separate zoom focus: the orbit point moves towards it while zooming in, so an
/// overview can frame a large scene while zooming still ends at the point of interest.
/// </summary>
public partial class OrbitCamera : Camera3D
{
    private const float RotateSpeed = 0.2f;
    private const float ZoomFactor = 1.1f;
    private const float MinPitch = 5f;
    private const float MaxPitch = 90f;
    private const float MinDistance = 0.3f;
    private const float MaxDistance = 10f;

    private Vector3 _viewTarget;
    private Vector3 _zoomFocus;
    private float _viewDistance;
    private Vector3 _pan;
    private float _yawDegrees;
    private float _pitchDegrees;
    private float _distance;

    /// <param name="zoomFocus">Where zooming in ends. Defaults to the target.</param>
    /// <summary>Jumps to a view and resets what the mouse has changed.</summary>
    /// <param name="target">The point to orbit.</param>
    /// <param name="yawDegrees">Rotation around the vertical axis.</param>
    /// <param name="pitchDegrees">Angle above the horizon; 90 looks straight down.</param>
    /// <param name="distance">Distance to the target in metres.</param>
    public void SetView(Vector3 target, float yawDegrees, float pitchDegrees, float distance, Vector3? zoomFocus = null)
    {
        _viewTarget = target;
        _zoomFocus = zoomFocus ?? target;
        _viewDistance = distance;
        _pan = Vector3.Zero;
        _yawDegrees = yawDegrees;
        _pitchDegrees = pitchDegrees;
        _distance = distance;
        ApplyView();
    }

    /// <summary>Zooms in for positive steps and out for negative steps, as one wheel click per step.</summary>
    public void Zoom(int steps)
    {
        float factor = Mathf.Pow(ZoomFactor, steps);
        _distance = Mathf.Clamp(_distance / factor, MinDistance, MaxDistance);
        ApplyView();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion motion when (motion.ButtonMask & (MouseButtonMask.Left | MouseButtonMask.Right)) != 0:
                _yawDegrees -= motion.Relative.X * RotateSpeed;
                _pitchDegrees = Mathf.Clamp(_pitchDegrees + motion.Relative.Y * RotateSpeed, MinPitch, MaxPitch);
                ApplyView();
                break;
            case InputEventMouseMotion motion when (motion.ButtonMask & MouseButtonMask.Middle) != 0:
                float scale = _distance * 0.0015f;
                _pan += GlobalBasis * new Vector3(-motion.Relative.X, motion.Relative.Y, 0f) * scale;
                ApplyView();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                Zoom(1);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                Zoom(-1);
                break;
        }
    }

    /// <summary>Places the camera from the current angles and distance.</summary>
    private void ApplyView()
    {
        Rotation = new Vector3(-Mathf.DegToRad(_pitchDegrees), Mathf.DegToRad(_yawDegrees), 0f);
        float zoomedOut = Mathf.Clamp(_distance / _viewDistance, 0f, 1f);
        Position = _zoomFocus.Lerp(_viewTarget, zoomedOut) + _pan + Basis.Z * _distance;
    }
}
