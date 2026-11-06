namespace BinPicking.Core.Sensing;

/// <summary>Why a pixel has no depth measurement.</summary>
public enum MissingReason : byte
{
    /// <summary>The pixel has a measurement.</summary>
    None,

    /// <summary>Too little of the pattern returned: black, oblique or distant surfaces.</summary>
    LowSignal,

    /// <summary>The projector cannot reach this point, because something closer blocks it.</summary>
    Shadow,

    /// <summary>The pattern breaks at a depth jump.</summary>
    Edge,

    /// <summary>The pixel clipped, so the pattern cannot be read. Only a real pattern measurement gives this.</summary>
    Overexposed,
}
