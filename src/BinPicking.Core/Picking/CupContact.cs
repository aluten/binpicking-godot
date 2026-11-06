namespace BinPicking.Core.Picking;

/// <summary>
/// Evaluates the contact ring of the cup: a seal requires the rim to touch one part all the way round.
/// This follows the idea of the contact ring in Dex-Net 3.0, without its spring model.
/// </summary>
public static class CupContact
{
    /// <summary>Returns the share of ring points that lie within the tolerance of the cup plane.</summary>
    /// <param name="ringDistances">Distance from each ring point to the part along the cup axis; null for a miss.</param>
    public static float RingShare(IReadOnlyList<float?> ringDistances, float tolerance)
    {
        if (ringDistances.Count == 0)
        {
            return 0f;
        }

        int touching = ringDistances.Count(d => d is { } distance && MathF.Abs(distance) <= tolerance);
        return touching / (float)ringDistances.Count;
    }

    /// <summary>Tells whether enough of the ring touches the part for the cup to seal.</summary>
    public static bool Seals(IReadOnlyList<float?> ringDistances, SuctionCupSpecification cup) =>
        RingShare(ringDistances, cup.ContactTolerance) >= cup.MinRingContact;
}
