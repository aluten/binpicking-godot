namespace BinPicking.Core.StructuredLight;

/// <summary>
/// The light patterns of one measurement: first the shifted sines, then the Gray code stripes, coarsest first.
/// Every pattern only varies per column, because camera and projector stand side by side along the columns.
/// </summary>
public sealed class PatternGenerator
{
    private readonly StructuredLightSettings _settings;
    private readonly int _grayBits;

    public PatternGenerator(StructuredLightSettings settings)
    {
        _settings = settings;
        _grayBits = settings.GrayBits;
    }

    /// <summary>
    /// The brightness of one pattern at a position, from 0 to 1. The position is continuous: column 3 of a
    /// texture covers positions 3 to 4, so sample a texture at its column + 0.5.
    /// </summary>
    public float Value(int image, float position)
    {
        if (image < _settings.SineSteps)
        {
            return 0.5f + 0.5f * MathF.Cos(MathF.Tau * position / _settings.SinePeriod + image * MathF.Tau / _settings.SineSteps);
        }

        int stripe = (int)(position / (_settings.SinePeriod / 2f));
        int bit = _grayBits - 1 - (image - _settings.SineSteps);
        return (ToGray(stripe) >> bit & 1) == 1 ? 1f : 0f;
    }

    /// <summary>Gray code: neighbouring stripes differ in one bit, so a blurred boundary costs at most one stripe.</summary>
    internal static int ToGray(int value) => value ^ (value >> 1);

    /// <summary>The way back from Gray code to the stripe number.</summary>
    internal static int FromGray(int gray)
    {
        int value = gray;
        while ((gray >>= 1) != 0)
        {
            value ^= gray;
        }
        return value;
    }
}
