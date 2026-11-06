using BinPicking.Core.Grasping;
using Godot;
using static PalletDimensions;

/// <summary>
/// Where the sensor hangs above the pallet, and how camera space maps to world space. With a real camera the
/// mapping comes from a hand-eye calibration instead; see docs/demo-questions.md.
/// </summary>
public static class SensorMount
{
    /// <summary>Height of the camera above the rim, in metres.</summary>
    public const float AboveRim = 1.5f;

    /// <summary>The image shows this far either side of the middle at rim height, in metres.</summary>
    private const float RimHalfViewMeters = 0.47f;

    private const float MinPickHeightAboveFloor = 0.03f; // Less than a link lying flat, so the empty floor is no pick point.
    private const float WallMargin = 0.01f; // The walls themselves are no candidates.
    private const float RimMargin = 0.05f; // Links may stick out a little above the rim.

    /// <summary>Height of the camera above the bin floor.</summary>
    public static float Height => RimHeight + AboveRim;

    /// <summary>Position of the camera in world space.</summary>
    public static Vector3 Position => new(0f, Height, 0f);

    /// <summary>The field of view fits the whole rim into the image.</summary>
    public static float VerticalFovDegrees => 2f * Mathf.RadToDeg(Mathf.Atan(RimHalfViewMeters / AboveRim));

    /// <summary>The bin as seen from the sensor. Camera Y points along world Z.</summary>
    public static BinRegion BinRegion => new(
        -InnerLength / 2f + WallMargin, InnerLength / 2f - WallMargin,
        -InnerWidth / 2f + WallMargin, InnerWidth / 2f - WallMargin,
        AboveRim - RimMargin, AboveRim + RimHeight - MinPickHeightAboveFloor);

    /// <summary>Camera X is world X, camera Y is world Z, and camera Z points down.</summary>
    public static Vector3 ToWorldPoint(float x, float y, float z) => new(x, Height - z, y);

    /// <summary>The same mapping for a direction, such as a surface normal.</summary>
    public static Vector3 ToWorldDirection(float x, float y, float z) => new(x, -z, y);
}
