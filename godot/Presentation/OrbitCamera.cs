using Godot;

/// <summary>
/// Camera that orbits a point with the mouse.
/// The left or right button rotates, the middle button pans and the wheel zooms.
/// </summary>
public partial class OrbitCamera : Camera3D
{
	private const float RotateSpeed = 0.2f;
	private const float ZoomFactor = 1.1f;
	private const float MinPitch = 5f;
	private const float MaxPitch = 90f;
	private const float MinDistance = 0.3f;
	private const float MaxDistance = 10f;

	private Vector3 _target;
	private float _yawDegrees;
	private float _pitchDegrees;
	private float _distance;

	public void SetView(Vector3 target, float yawDegrees, float pitchDegrees, float distance)
	{
		_target = target;
		_yawDegrees = yawDegrees;
		_pitchDegrees = pitchDegrees;
		_distance = distance;
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
				_target += GlobalBasis * new Vector3(-motion.Relative.X, motion.Relative.Y, 0f) * scale;
				ApplyView();
				break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
				_distance = Mathf.Max(_distance / ZoomFactor, MinDistance);
				ApplyView();
				break;
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
				_distance = Mathf.Min(_distance * ZoomFactor, MaxDistance);
				ApplyView();
				break;
		}
	}

	private void ApplyView()
	{
		Rotation = new Vector3(-Mathf.DegToRad(_pitchDegrees), Mathf.DegToRad(_yawDegrees), 0f);
		Position = _target + Basis.Z * _distance;
	}
}
