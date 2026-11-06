namespace BinPicking.Core.Picking;

/// <summary>Collects pick results. A few picks give a rough indication, not a reliable success rate.</summary>
public sealed class PickStatistics
{
    private readonly List<PickResult> _results = new();

    /// <summary>All attempts so far.</summary>
    public int Attempts => _results.Count;

    /// <summary>Attempts that ended with the part on the table.</summary>
    public int Placed => Count(PickOutcome.Placed);

    /// <summary>Share of the attempts that was placed, from 0 to 1.</summary>
    public double SuccessRate => Attempts == 0 ? 0.0 : (double)Placed / Attempts;

    /// <summary>Attempts in which at least one other part rose along.</summary>
    public int WithPulledAlong => _results.Count(r => r.PulledAlong > 0);

    /// <summary>Average number of other parts moved per attempt.</summary>
    public double AverageDisturbed => Attempts == 0 ? 0.0 : _results.Average(r => r.Disturbed);

    /// <summary>Attempts that lost the part before lifting, which points to a simulation artefact.</summary>
    public int LostBeforeLift => _results.Count(r => r.LostBeforeLift);

    /// <summary>Average number of parts moved before lifting, which measures simulation artefacts.</summary>
    public double AverageMovedBeforeLift => Attempts == 0 ? 0.0 : _results.Average(r => r.MovedBeforeLift);

    /// <summary>Records one attempt.</summary>
    public void Add(PickResult result) => _results.Add(result);

    /// <summary>The number of attempts with this outcome.</summary>
    public int Count(PickOutcome outcome) => _results.Count(r => r.Outcome == outcome);
}
