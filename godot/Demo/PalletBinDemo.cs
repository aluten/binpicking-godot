#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
	private Mesh? _linkMesh;
	private OrbitCamera _camera = null!;
	private DemoHud _hud = null!;
	private GraspMarkers _markers = null!;
	private BinFiller? _filler;
	private SensorMeasurement? _sensor;
	private SensorView _view = SensorView.Scene3D;

	public override void _Ready()
	{
		_linkMesh = LinkAssets.LoadSingleMesh(LinkAssets.LodPath);
		if (_linkMesh == null)
		{
			GD.PrintErr("Schakelbestanden ontbreken. Zie docs/cad-pipeline.md.");
			return;
		}

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
			case Key.Key4:
				NextSensorView();
				break;
			case Key.B when _sensor != null:
				MeasureAndRefresh(!_sensor.LightParts);
				break;
			case Key.P:
				NextPickMode();
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
		List<Transform3D> transforms = BinFillStore.Load();
		AddChild(new BinFillView(_linkMesh!, transforms));
		_hud.SetStatus($"{transforms.Count} schakels in de bak");
		BinFillInspector.Report(transforms, _linkMesh!);
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
		await sensor.CaptureAsync();
		_sensor = sensor;
		MeasureAndRefresh(lightParts: false);
		await CaptureScreenshotIfRequested();
	}

	private void MeasureAndRefresh(bool lightParts)
	{
		_sensor!.Measure(lightParts);
		_pick.Clear();
		if (_pick.Mode != PickDisplayMode.Hidden)
		{
			SearchPicks();
		}
		RefreshDisplay();
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
		_hud.SetStatus($"{_sensor!.Summary}\n{_pick.Summary}");
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
		string? path = args.FirstOrDefault(a => a.StartsWith("--screenshot="))?.Split('=')[1];
		if (path == null)
		{
			return;
		}

		ApplyScreenshotView(args.FirstOrDefault(a => a.StartsWith("--view="))?.Split('=')[1] ?? "overview");
		for (int i = 0; i < 6; i++)
		{
			await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		}
		GetViewport().GetTexture().GetImage().SavePng(path);
		GetTree().Quit();
	}

	private void ApplyScreenshotView(string view)
	{
		switch (view)
		{
			case "sensor-camera":
				ShowFromSensor();
				break;
			case "close":
				ShowCloseUp();
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
					MeasureAndRefresh(lightParts: true);
				}
				NextPickMode();
				NextPickMode();
				break;
		}
	}
}
