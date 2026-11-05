namespace BinPicking.Core.Sensing;

/// <summary>
/// Settings of the sensor model. The defaults are illustrative and were not measured on a real camera.
/// They show the effect of black material, oblique surfaces and distance.
/// </summary>
public sealed record DepthSensorSettings
{
    /// <summary>Distance at which the signal depends only on reflectance and angle, in metres.</summary>
    public float ReferenceDistance { get; init; } = 2f;

    /// <summary>Below this signal the measurement is almost always missing.</summary>
    public float SignalAlwaysMissing { get; init; } = 0.005f;

    /// <summary>Above this signal the measurement is almost always present.</summary>
    public float SignalAlwaysValid { get; init; } = 0.06f;

    /// <summary>Noise on a bright surface at the reference distance, in metres (1 sigma).</summary>
    public float BaseNoise { get; init; } = 0.0005f;

    /// <summary>At or above this signal there is only base noise; a lower signal adds noise.</summary>
    public float NoiseReferenceSignal { get; init; } = 0.1f;

    /// <summary>Upper limit of the noise in metres.</summary>
    public float MaxNoise { get; init; } = 0.01f;

    /// <summary>Distance between camera and projector in metres, towards camera +X.</summary>
    public float ProjectorBaseline { get; init; } = 0.15f;

    /// <summary>A pixel is shadowed if the projector sees something this much closer, in metres.</summary>
    public float ShadowTolerance { get; init; } = 0.008f;

    /// <summary>Depth jump to a neighbouring pixel above which a pixel counts as an edge, in metres.</summary>
    public float EdgeJump { get; init; } = 0.015f;

    /// <summary>Chance that an edge pixel has no measurement.</summary>
    public float EdgeMissingChance { get; init; } = 0.7f;

    /// <summary>Size in pixels of the blocks that make holes cluster.</summary>
    public int ClusterSize { get; init; } = 4;

    public int Seed { get; init; } = 1;
}
