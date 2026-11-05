namespace BinPicking.Core.Grasping;

/// <summary>Requirements for a suction pick point. Sizes in metres, angles in degrees.</summary>
public sealed record GraspSettings
{
    public float CupDiameter { get; init; } = 0.03f;

    /// <summary>Width of the holder above the cup, which must stay clear of other parts.</summary>
    public float ToolDiameter { get; init; } = 0.05f;

    /// <summary>A measured point belongs to the plane within this distance. Wider than the seal because measurements are noisy.</summary>
    public float PlaneTolerance { get; init; } = 0.003f;

    /// <summary>When verifying with perfect depth, the cup must lie within this distance of the plane.</summary>
    public float SealTolerance { get; init; } = 0.002f;

    /// <summary>Points further than this above the plane would hit the holder.</summary>
    public float ToolClearance { get; init; } = 0.005f;

    /// <summary>Minimum share of the cup area that must have a measurement.</summary>
    public float MinValidShare { get; init; } = 0.15f;

    /// <summary>Minimum share of the measured points that must lie in the plane.</summary>
    public float MinInlierShare { get; init; } = 0.9f;

    public float MaxTiltDegrees { get; init; } = 35f;

    /// <summary>Radius around the cup in which the flat surface is measured.</summary>
    public float SurfaceSearchRadius { get; init; } = 0.07f;

    /// <summary>Maximum distance between the cup and the centre of the flat surface.</summary>
    public float MaxCenterOffset { get; init; } = 0.02f;

    /// <summary>Evaluate a candidate every this many pixels.</summary>
    public int SearchStep { get; init; } = 6;

    public int MaxCandidates { get; init; } = 20;
}
