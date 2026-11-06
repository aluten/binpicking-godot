#nullable enable

using System.Collections.Generic;
using System.Linq;
using BinPicking.Core.Grasping;
using BinPicking.Core.Sensing;
using Godot;

/// <summary>Runs the pick search on a measurement, verifies the result and tracks what is displayed.</summary>
public sealed class PickSelection
{
	private const float GoodSealShare = 0.9f;

	private string _searchSummary = string.Empty;
	private string _bestSummary = string.Empty;

	public GraspSettings Settings { get; } = new();

	public PickDisplayMode Mode { get; private set; } = PickDisplayMode.Hidden;

	public List<GraspCandidate> Candidates { get; private set; } = new();

	public string Summary => Mode switch
	{
		PickDisplayMode.Hidden => "Druk op P om pickpunten te zoeken",
		PickDisplayMode.Best when Candidates.Count > 0 => _bestSummary,
		_ => _searchSummary,
	};

	/// <summary>Moves to the next display step: nothing, then all candidates, then also the best one.</summary>
	public void NextMode()
	{
		Mode = Mode switch
		{
			PickDisplayMode.Hidden => PickDisplayMode.Candidates,
			PickDisplayMode.Candidates => PickDisplayMode.Best,
			_ => PickDisplayMode.Hidden,
		};
	}

	public bool IsHighlighted(int index) => index == 0 && Mode == PickDisplayMode.Best;

	public void Clear() => Candidates = new List<GraspCandidate>();

	public void HideDisplay() => Mode = PickDisplayMode.Hidden;

	public void Search(PinholeCamera camera, float[] measuredDepth, float[] idealDepth)
	{
		ulong started = Time.GetTicksMsec();
		(Candidates, GraspSearchReport report) = GraspPlanner.FindGrasps(camera, measuredDepth, SensorMount.BinRegion, Settings);
		ulong milliseconds = Time.GetTicksMsec() - started;

		GD.Print($"INFO Pickpunten: {report.Evaluated} beoordeeld in {milliseconds} ms; " +
				 $"te weinig metingen {report.TooFewPoints}, niet vlak {report.NotFlat}, te schuin {report.TooTilted}, " +
				 $"niet in het midden {report.OffCenter}, " +
				 $"houder raakt {report.ToolCollision}, buiten opening {report.OutsideOpening}, goed {report.Accepted}");

		if (Candidates.Count == 0)
		{
			GD.Print("INFO Geen pickpunt gevonden.");
			_searchSummary = "Geen pickpunt gevonden";
			return;
		}

		GraspCandidate best = Candidates[0];
		float bestSeal = SealVerifier.TrueSealShare(camera, idealDepth, best, Settings);
		int good = Candidates.Count(c => SealVerifier.TrueSealShare(camera, idealDepth, c, Settings) >= GoodSealShare);
		GD.Print($"INFO Beste pickpunt: {best.ValidShare * 100f:F0}% gemeten, {best.InlierShare * 100f:F0}% in het vlak, " +
				 $"{best.TiltDegrees:F0} graden schuin, {best.CenterOffset * 1000f:F0} mm uit het midden; werkelijke afdichting {bestSeal * 100f:F0}%");
		GD.Print($"INFO Controle met perfecte diepte: {good} van de {Candidates.Count} beste kandidaten dichten minstens " +
				 $"{GoodSealShare * 100f:F0}% af");
		_searchSummary = $"{report.Accepted} plekken goedgekeurd, de {Candidates.Count} beste getoond; " +
						 $"{good} dichten echt af. Druk op P voor de keuze";
		_bestSummary = $"Gekozen: {best.ValidShare * 100f:F0}% gemeten, {best.TiltDegrees:F0} graden schuin, {best.CenterOffset * 1000f:F0} mm uit het midden, " +
					   $"werkelijke afdichting {bestSeal * 100f:F0}%";
	}
}
