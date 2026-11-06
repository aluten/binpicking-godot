#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BinPicking.Core.Grasping;
using BinPicking.Core.Picking;
using Godot;
using static PalletDimensions;

/// <summary>
/// Composition root of the demo: connects the bin, sensor, pick search and presentation, and handles
/// the keys. The behaviour itself lives in those modules. Without a stored fill the bin is filled first.
/// </summary>
public partial class PalletBinDemo : Node3D
{
	private enum SensorView
	{
		Scene3D,
		PerfectDepth,
		Measured,
	}

	private readonly PickSelection _pick = new();
	private LinkAppearance _appearance = null!;
	private bool _measuring;
	private Mesh? _linkMesh;
	private OrbitCamera _camera = null!;
	private DemoHud _hud = null!;
	private GraspMarkers _markers = null!;
	private BinFiller? _filler;
	private SensorMeasurement? _sensor;
	private SensorView _view = SensorView.Scene3D;

	private const int AutoPickCount = 10;
	private const float RetryExclusionRadius = 0.03f;

	private readonly SuctionCupSpecification _cup = new();
	private readonly PickStatistics _statistics = new();
	private readonly List<Vector3> _failedPoints = new();
	private List<Transform3D> _transforms = new();
	private BinFillView? _fillView;
	private Mesh? _fullMesh;
	private List<(Shape3D Shape, Transform3D Offset)>? _shapes;
	private PickCycle? _cycle;
	private int _autoPicksLeft;
	private string _lastPickSummary = string.Empty;
	private string? _liftScreenshot;

	public override void _Ready()
	{
		_linkMesh = LinkAssets.LoadSingleMesh(LinkAssets.LodPath);
		if (_linkMesh == null)
		{
			GD.PrintErr("Schakelbestanden ontbreken. Zie docs/cad-pipeline.md.");
			return;
		}

		_appearance = new LinkAppearance(_linkMesh);
		AddChild(new DemoEnvironment());
		AddChild(new PalletBin());
		_markers = new GraspMarkers();
		AddChild(_markers);
		_hud = new DemoHud();
		AddChild(_hud);
		BuildCamera();

		if (BinFillStore.Exists())
		{
			ShowStoredFill();
		}
		else
		{
			StartFilling();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key)
		{
			return;
		}

		switch (key.Keycode)
		{
			case Key.Key1:
				ShowOverview();
				break;
			case Key.Key2:
				ShowFromSensor();
				break;
			case Key.Key3:
				ShowCloseUp();
				break;
			case Key.H:
				_hud.ToggleLegend();
				break;
			case Key.Key4:
				NextSensorView();
				break;
			case Key.B when _sensor != null && _cycle == null && !_measuring:
				_ = ToggleColorAsync();
				break;
			case Key.P when _cycle == null && !_measuring:
				NextPickMode();
				break;
			case Key.G when _cycle == null && !_measuring:
				StartPick();
				break;
			case Key.A when _cycle == null && !_measuring:
				_autoPicksLeft = AutoPickCount;
				StartPick();
				break;
			case Key.R when _filler != null:
				_filler.RequestStop();
				break;
			case Key.R:
				BinFillStore.Delete();
				GetTree().ReloadCurrentScene();
				break;
		}
	}

	private void StartFilling()
	{
		_filler = new BinFiller(_linkMesh!, LinkAssets.LoadCollisionShapes());
		_filler.Progress += _hud.SetStatus;
		_filler.Finished += OnFillFinished;
		AddChild(_filler);
	}

	private void OnFillFinished(List<Transform3D> transforms)
	{
		BinFillStore.Save(transforms, BinFiller.RandomSeed);
		if (IsHeadless)
		{
			BinFillInspector.Report(transforms, _linkMesh!);
			GetTree().Quit();
		}
		else
		{
			GetTree().ReloadCurrentScene();
		}
	}

	private void ShowStoredFill()
	{
		_transforms = BinFillStore.Load();
		_fillView = new BinFillView(_linkMesh!, _transforms);
		AddChild(_fillView);
		AddChild(new PlaceTable(PlacePosition));
		_hud.SetStatus($"{_transforms.Count} schakels in de bak");
		BinFillInspector.Report(_transforms, _linkMesh!);
		_ = StartSensorAsync();
	}

	private async Task StartSensorAsync()
	{
		if (IsHeadless)
		{
			return;
		}

		var rig = new DepthSensorRig();
		AddChild(rig);
		var sensor = new SensorMeasurement(rig);
		_sensor = sensor;
		await MeasureAsync();
		await CaptureScreenshotIfRequested();
	}

	/// <summary>The sensor measures the scene again, because something in it has changed.</summary>
	private async Task MeasureAsync()
	{
		_measuring = true;
		await _sensor!.MeasureAsync(_appearance.IsWhite ? "witte schakels" : "zwarte schakels");
		_measuring = false;
		_pick.Clear();
		if (_pick.Mode != PickDisplayMode.Hidden)
		{
			SearchPicks();
		}
		RefreshDisplay();
	}

	private async Task ToggleColorAsync()
	{
		_appearance.SetWhite(!_appearance.IsWhite);
		await MeasureAsync();
	}

	private void NextPickMode()
	{
		if (_sensor?.Result == null)
		{
			return;
		}

		_pick.NextMode();
		if (_pick.Mode == PickDisplayMode.Candidates)
		{
			SearchPicks();
		}
		RefreshDisplay();
	}

	private void SearchPicks() => _pick.Search(_sensor!.Camera, _sensor.Result!.Depth, _sensor.IdealDepth);

	private void NextSensorView()
	{
		if (_sensor?.Result == null)
		{
			return;
		}

		_view = (SensorView)(((int)_view + 1) % 3);
		RefreshDisplay();
	}

	private void RefreshDisplay()
	{
		string pickLine = _lastPickSummary.Length > 0 ? $"\n{_lastPickSummary}" : string.Empty;
		_hud.SetStatus($"{_sensor!.Summary}\n{_pick.Summary}{pickLine}");
		_markers.Show(_pick);

		if (_view == SensorView.Scene3D)
		{
			_hud.ShowImage(null);
			return;
		}

		byte[] pixels = _view == SensorView.PerfectDepth
			? SensorImage.Draw(_sensor.IdealDepth, null)
			: SensorImage.Draw(_sensor.Result!.Depth, _sensor.Result.Missing);
		GraspImageOverlay.Draw(pixels, _sensor.Camera, _pick);
		_hud.ShowImage(SensorImage.ToImage(pixels, _sensor.Camera));
	}

	private static Vector3 PlacePosition => new(CollarOuterLength / 2f + 0.55f, PlaceTable.TopHeight + 0.09f, 0f);

	/// <summary>Spreads placed links over the table, in rows of three.</summary>
	private Vector3 NextPlacePosition()
	{
		int slot = _statistics.Placed % 9;
		return PlacePosition + new Vector3((slot % 3 - 1) * 0.18f, 0f, (slot / 3 - 1) * 0.18f);
	}

	private void StartPick()
	{
		if (_sensor?.Result == null)
		{
			return;
		}

		if (_pick.Candidates.Count == 0)
		{
			SearchPicks();
		}

		GraspCandidate? grasp = _pick.Candidates.FirstOrDefault(c => !WasTried(c));
		if (grasp == null)
		{
			GD.Print("INFO Pick: geen bruikbaar pickpunt meer.");
			_autoPicksLeft = 0;
			_lastPickSummary = "Geen bruikbaar pickpunt meer";
			ReportStatistics();
			RefreshDisplay();
			FinishAutomaticRun();
			return;
		}

		_view = SensorView.Scene3D;
		_hud.ShowImage(null);
		_pick.HideDisplay();
		_markers.Show(_pick);
		_fullMesh ??= LinkAssets.LoadSingleMesh(LinkAssets.VisualPath);
		_shapes ??= LinkAssets.LoadCollisionShapes();

		_cycle = new PickCycle(_transforms, _linkMesh!, _fullMesh!, _shapes, grasp, _cup, _pick.Settings.ToolDiameter, NextPlacePosition());
		_cycle.RegionReady += included => _fillView!.Hide(included, _transforms);
		_cycle.Lifting += OnLifting;
		_cycle.Finished += (result, remaining, placed) => _ = OnPickFinishedAsync(grasp, result, remaining, placed);
		AddChild(_cycle);
		_hud.SetStatus($"Pick {_statistics.Attempts + 1} bezig...");
	}

	private bool WasTried(GraspCandidate candidate)
	{
		Vector3 point = SensorMount.ToWorldPoint(candidate.X, candidate.Y, candidate.Z);
		return _failedPoints.Any(failed => failed.DistanceTo(point) < RetryExclusionRadius);
	}

	private async Task OnPickFinishedAsync(GraspCandidate grasp, PickResult result, List<Transform3D> remaining, Transform3D? placed)
	{
		_statistics.Add(result);
		if (result.Outcome != PickOutcome.Placed)
		{
			_failedPoints.Add(SensorMount.ToWorldPoint(grasp.X, grasp.Y, grasp.Z));
		}

		_lastPickSummary = $"Pick {_statistics.Attempts}: {DescribeOutcome(result.Outcome)}, contactring {result.RingShare * 100f:F0}%; " +
						   $"{_statistics.Placed} van {_statistics.Attempts} geplaatst";
		GD.Print($"INFO Pick {_statistics.Attempts}: {DescribeOutcome(result.Outcome)}; contactring {result.RingShare * 100f:F0}%, " +
				 $"{result.CenterOffset * 1000f:F0} mm uit het midden, meegetrokken {result.PulledAlong}, verstoord {result.Disturbed}; vóór het tillen bewogen {result.MovedBeforeLift}" +
				 (result.LostBeforeLift ? ", verloren vóór het tillen" : string.Empty));

		_cycle!.QueueFree();
		_cycle = null;
		_transforms = remaining;
		_fillView!.Show(_transforms);
		if (placed is { } placedTransform)
		{
			AddChild(new MeshInstance3D { Mesh = _linkMesh, Transform = placedTransform });
		}

		// Measure again, because the bin has changed. Wait until the removed bodies are gone.
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		_pick.HideDisplay();
		await MeasureAsync();

		if (_autoPicksLeft > 0)
		{
			_autoPicksLeft--;
			if (_autoPicksLeft > 0)
			{
				StartPick();
				return;
			}
		}
		ReportStatistics();
		FinishAutomaticRun();
	}

	private void ReportStatistics()
	{
		GD.Print($"INFO Reeks: {_statistics.Placed} van {_statistics.Attempts} geplaatst " +
				 $"({_statistics.SuccessRate * 100.0:F0}%); geen afdichting {_statistics.Count(PickOutcome.NoSeal)}, " +
				 $"verloren tijdens tillen {_statistics.Count(PickOutcome.LostDuringLift)}, " +
				 $"geen schakel {_statistics.Count(PickOutcome.NoPartAtPoint)}; met meegetrokken schakels {_statistics.WithPulledAlong}, " +
				 $"gemiddeld {_statistics.AverageDisturbed:F1} verstoord; schijnbewegingen: gemiddeld {_statistics.AverageMovedBeforeLift:F1} bewogen vóór het tillen, " +
				 $"{_statistics.LostBeforeLift} verloren vóór het tillen");
	}

	private static string DescribeOutcome(PickOutcome outcome) => outcome switch
	{
		PickOutcome.Placed => "geplaatst",
		PickOutcome.NoSeal => "geen afdichting",
		PickOutcome.LostDuringLift => "verloren tijdens tillen",
		_ => "geen schakel gevonden",
	};

	private void OnLifting()
	{
		if (_liftScreenshot != null)
		{
			_ = CaptureLiftAsync(_liftScreenshot);
			_liftScreenshot = null;
		}
	}

	private async Task CaptureLiftAsync(string path)
	{
		// A moment later, so the part is visibly in the air.
		await ToSignal(GetTree().CreateTimer(1.6), SceneTreeTimer.SignalName.Timeout);
		GetViewport().GetTexture().GetImage().SavePng(path);
	}

	private void FinishAutomaticRun()
	{
		if (_quitAfterRun)
		{
			string? path = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--screenshot="))?.Split('=')[1];
			if (path != null)
			{
				GetViewport().GetTexture().GetImage().SavePng(path);
			}
			GetTree().Quit();
		}
	}

	private bool _quitAfterRun;

	private void BuildCamera()
	{
		_camera = new OrbitCamera
		{
			Fov = 45f,
			Near = 0.02f,
			Far = 30f,
			CullMask = 1u | (1u << (DepthSensorRig.ModelLayer - 1)),
		};
		AddChild(_camera);
		_camera.MakeCurrent();
		ShowOverview();
	}

	private void ShowOverview() => _camera.SetView(new Vector3(0f, 1.75f, 0f), -28f, 35f, 5.2f, zoomFocus: new Vector3(0f, 0.5f, 0f));

	private void ShowFromSensor() => _camera.SetView(new Vector3(0f, RimHeight, 0f), 0f, 90f, SensorMount.AboveRim - 0.03f);

	private void ShowCloseUp() => _camera.SetView(new Vector3(0f, RimHeight - 0.05f, 0f), 30f, 45f, 1.1f);

	private static bool IsHeadless => DisplayServer.GetName() == "headless";

	/// <summary>For automated checks: godot ... -- --screenshot=path.png --view=name</summary>
	private async Task CaptureScreenshotIfRequested()
	{
		string[] args = OS.GetCmdlineUserArgs();
		string? runCount = args.FirstOrDefault(a => a.StartsWith("--picks="))?.Split('=')[1];
		if (runCount != null)
		{
			_liftScreenshot = args.FirstOrDefault(a => a.StartsWith("--lift-screenshot="))?.Split('=')[1];
			_quitAfterRun = true;
			_autoPicksLeft = int.Parse(runCount);
			if (args.Contains("--white"))
			{
				await ToggleColorAsync();
			}
			StartPick();
			return;
		}

		string? path = args.FirstOrDefault(a => a.StartsWith("--screenshot="))?.Split('=')[1];
		if (path == null)
		{
			return;
		}

		await ApplyScreenshotViewAsync(args.FirstOrDefault(a => a.StartsWith("--view="))?.Split('=')[1] ?? "overview");
		for (int i = 0; i < 6; i++)
		{
			await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		}
		GetViewport().GetTexture().GetImage().SavePng(path);
		GetTree().Quit();
	}

	private async Task ApplyScreenshotViewAsync(string view)
	{
		switch (view)
		{
			case "sensor-camera":
				ShowFromSensor();
				break;
			case "close":
				ShowCloseUp();
				break;
			case "legend":
				_hud.ToggleLegend();
				break;
			case "zoom-half":
				_camera.Zoom(8);
				break;
			case "zoom-max":
				_camera.Zoom(40);
				break;
			case "candidates":
				NextPickMode();
				break;
			case "white":
				await ToggleColorAsync();
				break;
			case "white-black":
				await ToggleColorAsync();
				await ToggleColorAsync();
				break;
			case "best":
				NextPickMode();
				NextPickMode();
				break;
			case "perfect":
			case "measured":
			case "light":
				_view = view == "perfect" ? SensorView.PerfectDepth : SensorView.Measured;
				if (view == "light")
				{
					await ToggleColorAsync();
				}
				NextPickMode();
				NextPickMode();
				break;
		}
	}
}
