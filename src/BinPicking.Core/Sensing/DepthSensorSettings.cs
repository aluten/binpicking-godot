namespace BinPicking.Core.Sensing;

/// <summary>
/// Instellingen van het sensormodel. De standaardwaarden zijn illustratief en niet gemeten aan een
/// echte camera. Ze laten zien welk effect zwart materiaal, schuine vlakken en afstand hebben.
/// </summary>
public sealed record DepthSensorSettings
{
    /// <summary>Afstand waarop het signaal alleen van reflectie en hoek afhangt, in meters.</summary>
    public float ReferenceDistance { get; init; } = 2f;

    /// <summary>Onder dit signaal ontbreekt de meting vrijwel altijd.</summary>
    public float SignalAlwaysMissing { get; init; } = 0.005f;

    /// <summary>Boven dit signaal is de meting vrijwel altijd aanwezig.</summary>
    public float SignalAlwaysValid { get; init; } = 0.06f;

    /// <summary>Ruis bij een helder vlak op de referentieafstand, in meters (1 sigma).</summary>
    public float BaseNoise { get; init; } = 0.0005f;

    /// <summary>Bij dit signaal of hoger is er alleen basisruis; lager signaal geeft meer ruis.</summary>
    public float NoiseReferenceSignal { get; init; } = 0.1f;

    /// <summary>Bovengrens van de ruis in meters.</summary>
    public float MaxNoise { get; init; } = 0.01f;

    /// <summary>Afstand tussen camera en projector in meters, naar +X in het camerastelsel.</summary>
    public float ProjectorBaseline { get; init; } = 0.15f;

    /// <summary>Een pixel is beschaduwd als de projector dit veel dichterbij iets anders ziet, in meters.</summary>
    public float ShadowTolerance { get; init; } = 0.008f;

    /// <summary>Dieptesprong met het buurpixel waarboven een pixel als rand telt, in meters.</summary>
    public float EdgeJump { get; init; } = 0.015f;

    /// <summary>Kans dat een randpixel geen meting geeft.</summary>
    public float EdgeMissingChance { get; init; } = 0.7f;

    /// <summary>Grootte in pixels van de blokken die gaten laten samenklonteren.</summary>
    public int ClusterSize { get; init; } = 4;

    public int Seed { get; init; } = 1;
}
