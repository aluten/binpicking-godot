using BinPicking.Core.Grasping;
using Godot;
using static PalletDimensions;

/// <summary>Where the sensor hangs above the pallet, and how camera space maps to world space.</summary>
public static class SensorMount
{
    public const float AboveRim = 1.5f;

    private const float RimHalfViewMeters = 0.47f;
    private const float MinPickHeightAboveFloor = 0.03f; // Less than a link lying flat, so the empty floor is no pick point.
    private const float WallMargin = 0.01f; // The walls themselves are no candidates.
    private const float RimMargin = 0.05f;

    public static float Height => RimHeight + AboveRim;

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

    public static Vector3 ToWorldDirection(float x, float y, float z) => new(x, -z, y);
}
