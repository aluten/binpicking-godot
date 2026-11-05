namespace BinPicking.Core.Grasping;

/// <summary>Eisen aan een pickpunt voor een zuignap. Maten in meters, hoeken in graden.</summary>
public sealed record GraspSettings
{
    public float CupDiameter { get; init; } = 0.03f;

    /// <summary>Breedte van de houder boven de nap. Die moet vrij blijven van andere onderdelen.</summary>
    public float ToolDiameter { get; init; } = 0.05f;

    /// <summary>Een gemeten punt hoort bij het vlak als het hier binnen ligt. Ruimer dan de afdichting, want de meting ruist.</summary>
    public float PlaneTolerance { get; init; } = 0.003f;

    /// <summary>Bij de controle met perfecte diepte moet de nap binnen deze afstand van het vlak liggen.</summary>
    public float SealTolerance { get; init; } = 0.002f;

    /// <summary>Punten die verder dan dit boven het vlak uitsteken raken de houder.</summary>
    public float ToolClearance { get; init; } = 0.005f;

    /// <summary>Minimaal deel van de nap waarvoor een meting bestaat.</summary>
    public float MinValidShare { get; init; } = 0.15f;

    /// <summary>Minimaal deel van de gemeten punten dat in het vlak moet liggen.</summary>
    public float MinInlierShare { get; init; } = 0.9f;

    public float MaxTiltDegrees { get; init; } = 35f;

    /// <summary>Om de hoeveel pixels een kandidaat wordt beoordeeld.</summary>
    public int SearchStep { get; init; } = 6;

    public int MaxCandidates { get; init; } = 20;
}
