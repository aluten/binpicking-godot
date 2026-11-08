/// <summary>How the sensor turns the scene into measured depth.</summary>
public enum SensorKind
{
    /// <summary>Perfect depth with holes and noise from a formula; see docs/sensor-model.md.</summary>
    Formula,

    /// <summary>Depth decoded from images of projected light patterns; see docs/structured-light.md.</summary>
    ProjectedLight,
}
