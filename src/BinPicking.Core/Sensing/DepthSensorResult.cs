namespace BinPicking.Core.Sensing;

/// <summary>Output of the sensor model. Pixels without a measurement have a depth of NaN.</summary>
public sealed record DepthSensorResult(float[] Depth, MissingReason[] Missing);
