namespace BinPicking.Core.StructuredLight;

/// <summary>
/// How the patterns are built and judged. A sine pattern gives the precise position within a period, and Gray
/// code stripes say which period it is. The defaults are choices for this experiment.
/// </summary>
public sealed record StructuredLightSettings
{
    /// <summary>Width of the patterns in columns. Divided by half a sine period, this must be a power of two.</summary>
    public int Columns { get; init; } = 2048;

    /// <summary>Length of one sine period, in columns. A shorter period is more precise, until the lens blurs it.</summary>
    public int SinePeriod { get; init; } = 32;

    /// <summary>The number of shifted sine images. More images cost more time and average out more noise.</summary>
    public int SineSteps { get; init; } = 4;

    /// <summary>Below this sine amplitude a pixel has no usable signal, as a share of the full scale of the camera.</summary>
    public float MinModulation { get; init; } = 0.002f;

    /// <summary>
    /// Below this ratio of sine amplitude to image noise, a pixel is rejected. Noise can flip a stripe bit, which
    /// picks the wrong period and gives a depth that is centimetres off without any warning. Only used when the
    /// decoder knows the image sensor. 5 means the stripes stand about five noise widths above the threshold.
    /// </summary>
    public float MinSignalToNoise { get; init; } = 5f;

    /// <summary>A pixel at or above this share of the full scale counts as overexposed.</summary>
    public float SaturationLevel { get; init; } = 0.98f;

    /// <summary>
    /// The largest allowed difference between sine and stripes, in radians. Stripes and sine agree within
    /// a quarter period (pi / 2) on a clean pixel; more means the pixel straddles an edge or has a wrong bit.
    /// </summary>
    public float MaxPhaseDisagreement { get; init; } = 2.2f;

    /// <summary>The number of Gray code images. The finest stripe is half a sine period wide.</summary>
    public int GrayBits
    {
        get
        {
            int stripes = Columns / (SinePeriod / 2);
            if (stripes * (SinePeriod / 2) != Columns || (stripes & (stripes - 1)) != 0)
            {
                throw new InvalidOperationException("Columns divided by half a sine period must be a power of two.");
            }
            return System.Numerics.BitOperations.Log2((uint)stripes);
        }
    }

    /// <summary>The total number of images in one measurement.</summary>
    public int ImageCount => SineSteps + GrayBits;
}
