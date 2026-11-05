namespace BinPicking.Core.Sensing;

/// <summary>Why a pixel has no depth measurement.</summary>
public enum MissingReason : byte
{
    None,
    LowSignal,
    Shadow,
    Edge,
}
