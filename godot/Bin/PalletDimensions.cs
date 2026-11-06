/// <summary>
/// Measurements of a euro pallet with stacked collars, in metres.
/// The outer size and collar height follow Fabory 8W372370; the plank thickness is an assumption.
/// The bin floor is at y = 0 and the pallet is centred on the origin.
/// </summary>
public static class PalletDimensions
{
    // A collar is a wooden frame that stacks on the pallet.
    public const float CollarOuterLength = 1.2f;
    public const float CollarOuterWidth = 0.8f;
    public const float CollarHeight = 0.2f;
    public const float PlankThickness = 0.02f;
    public const int CollarCount = 5;
    public const float PalletHeight = 0.144f;

    // The usable inside of the bin.
    public const float InnerLength = CollarOuterLength - 2f * PlankThickness;
    public const float InnerWidth = CollarOuterWidth - 2f * PlankThickness;
    public const float RimHeight = CollarCount * CollarHeight;
}
