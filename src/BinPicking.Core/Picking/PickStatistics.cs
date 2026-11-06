namespace BinPicking.Core.Picking;

/// <summary>Collects pick results. A few picks give a rough indication, not a reliable success rate.</summary>
public sealed class PickStatistics
{
    private readonly List<PickResult> _results = new();

    public int Attempts => _results.Count;

    public int Placed => Count(PickOutcome.Placed);

    public double SuccessRate => Attempts == 0 ? 0.0 : (double)Placed / Attempts;

    public int WithPulledAlong => _results.Count(r => r.PulledAlong > 0);

    public double AverageDisturbed => Attempts == 0 ? 0.0 : _results.Average(r => r.Disturbed);

    public void Add(PickResult result) => _results.Add(result);

    public int Count(PickOutcome outcome) => _results.Count(r => r.Outcome == outcome);
}
