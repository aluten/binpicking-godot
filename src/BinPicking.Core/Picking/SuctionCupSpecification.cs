namespace BinPicking.Core.Picking;

/// <summary>
/// A suction cup and what it can hold. The vacuum and efficiency are assumptions for this demo,
/// not the specifications of a real cup. Sizes in metres, pressure in pascal, force in newton.
/// </summary>
public sealed record SuctionCupSpecification
{
    /// <summary>Outer diameter of the cup.</summary>
    public float Diameter { get; init; } = 0.03f;

    /// <summary>Pressure difference between the outside air and the cup.</summary>
    public float VacuumPascal { get; init; } = 40_000f;

    /// <summary>Share of the theoretical force that is usable, because of leaks and deformation.</summary>
    public float Efficiency { get; init; } = 0.5f;

    /// <summary>A ring point makes contact if the part lies within this distance of the cup plane.</summary>
    public float ContactTolerance { get; init; } = 0.003f;

    /// <summary>Minimum share of the ring that must touch the part for a seal.</summary>
    public float MinRingContact { get; init; } = 0.9f;

    /// <summary>Area of the cup opening.</summary>
    public float Area => MathF.PI * Diameter * Diameter / 4f;

    /// <summary>Pull force: pressure difference x area x efficiency.</summary>
    public float HoldingForce => VacuumPascal * Area * Efficiency;

    /// <summary>
    /// Rough limit for a tipping moment: the pull force acting at half the cup radius.
    /// A real cup also depends on its stiffness, which is not modelled.
    /// </summary>
    public float HoldingTorque => HoldingForce * Diameter / 4f;
}
