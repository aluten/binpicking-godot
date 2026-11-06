namespace BinPicking.Core.StructuredLight;

/// <summary>
/// The image sensor of the virtual camera. The values are illustrative and were not measured on a real camera.
/// The unit of light is whatever the renderer gives; <see cref="ElectronsPerLightUnit"/> ties it to the sensor.
/// </summary>
public sealed record CameraNoiseSettings
{
    /// <summary>Stands for exposure time x aperture x gain together: more means a brighter image.</summary>
    public float ElectronsPerLightUnit { get; init; } = 4000f;

    /// <summary>What one pixel can hold. Above this the image clips.</summary>
    public float FullWellElectrons { get; init; } = 10000f;

    /// <summary>Noise of the electronics, whatever the amount of light (1 sigma).</summary>
    public float ReadNoiseElectrons { get; init; } = 5f;

    /// <summary>The number of levels of the image is 2 to this power.</summary>
    public int Bits { get; init; } = 12;
}
