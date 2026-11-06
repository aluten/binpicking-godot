/// <summary>What the demo shows of the pick search, so it can be explained step by step.</summary>
public enum PickDisplayMode
{
    /// <summary>No pick points, so the bin and the measurement can be explained first.</summary>
    Hidden,

    /// <summary>All candidates that remain after the search.</summary>
    Candidates,

    /// <summary>The candidates, with the chosen one highlighted.</summary>
    Best,
}
