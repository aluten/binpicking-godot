#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BinPicking.Core.Grasping;
using BinPicking.Core.Sensing;
using Godot;

/// <summary>
/// Europallet met opzetranden, gevuld met schakels.
/// Ontbreekt de opgeslagen vulling, dan wordt de bak eerst gevuld met de physics-engine.
/// Daarna laadt de scène de opgeslagen posities, zodat starten direct gaat.
/// </summary>
public partial class PalletBinDemo : Node3D
{
	private const string FillPath = "res://generated/pallet_vulling.json";

	// Buitenmaten volgens Fabory 8W372370. De plankdikte is een aanname.
	private const float CollarOuterLength = 1.2f;
	private const float CollarOuterWidth = 0.8f;
	private const float CollarHeight = 0.2f;
	private const float PlankThickness = 0.02f;
	private const int CollarCount = 5;
	private const float PalletHeight = 0.144f;

	private const float InnerLength = CollarOuterLength - 2f * PlankThickness;
	private const float InnerWidth = CollarOuterWidth - 2f * PlankThickness;
	private const float RimHeight = CollarCount * CollarHeight;

	private const int TargetLinks = 720;
	private const int RandomSeed = 1;
	private const int BatchLinks = 24;
	private const int SpawnColumns = 6;
	private const int SpawnRows = 4;
	private const float LinkMassKg = 0.15f; // Aanname, geen igus-waarde.

	private const float FreezeDepth = 0.12f;
	private const float SettledSpeed = 0.02f;
	private const double MaxExtraSettleSeconds = 30.0;
	private const float MaxOverlapWhenFrozen = 0.003f;
	private const double MinBatchSeconds = 0.8;
	private const double MaxBatchSeconds = 4.0;
	private const float PourWallHeight = 3f;
	private const float SpawnClearance = 0.14f;

	// Sensor boven de bak. De kijkhoek past de hele rand in beeld.
	private const float SensorAboveRim = 1.5f;
	private const float RimHalfViewMeters = 0.47f;
	private const float PartReflectanceLimit = 0.2f;
	private const float LightPartReflectance = 0.5f;
	private const float GoodSealShare = 0.9f;

	// Dunner dan een platliggende schakel, zodat de lege bodem geen pickpunt wordt.
	private const float MinPickHeightAboveFloor = 0.03f;

	// De wanden zelf zijn geen kandidaten.
	private const float WallMargin = 0.01f;

	private static readonly Color WoodColor = new(0.72f, 0.56f, 0.36f);
	private static readonly Color MetalColor = new(0.55f, 0.57f, 0.6f);

	private readonly List<RigidBody3D> _active = new();
	private readonly List<RigidBody3D> _frozen = new();
	private readonly RandomNumberGenerator _random = new() { Seed = RandomSeed };
	private List<(Shape3D Shape, Transform3D Offset)> _shapes = new();
	private Mesh? _lod;
	private Vector3[] _surfacePoints = System.Array.Empty<Vector3>();
	private OrbitCamera _camera = null!;
	private Label _status = null!;
	private bool _pouring;
	private bool _stopRequested;
	private double _batchElapsed;
	private double _extraSettleSeconds;
	private double _pourSeconds;
	private ulong _pourStartedMs;
	private int _spawned;
	private readonly DepthSensorSettings _sensorSettings = new();
	private DepthSensorRig? _rig;
	private TextureRect _sensorView = null!;
	private float[] _idealDepth = System.Array.Empty<float>();
	private float[] _projectorDepth = System.Array.Empty<float>();
	private float[] _reflectance = System.Array.Empty<float>();
	private int _viewMode;
	private bool _lightParts;
	private int _droppedAgain;

	public override void _Ready()
	{
		_lod = LinkAssets.LoadSingleMesh(LinkAssets.LodPath);
		if (_lod == null)
		{
			GD.PrintErr("Schakelbestanden ontbreken. Zie docs/cad-pijplijn.md.");
			return;
		}

		BuildEnvironment();
		BuildPallet();
		BuildCamera();
		BuildHelp();

		if (FileAccess.FileExists(FillPath))
		{
			ShowStoredFill();
			_ = MeasureAndCapture();
		}
		else
		{
			StartPour();
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
				ShowBinCamera();
				break;
			case Key.Key3:
				_camera.SetView(new Vector3(0f, RimHeight - 0.05f, 0f), 30f, 45f, 1.1f);
				break;
			case Key.Key4:
				CycleSensorView();
				break;
			case Key.B:
				_lightParts = !_lightParts;
				ShowSensorResult();
				break;
			case Key.R when _pouring:
				// Stoppen: er vallen geen nieuwe schakels meer bij, de laatste landen nog.
				_stopRequested = true;
				_status.Text = $"Stoppen bij {_spawned} schakels: de laatste schakels landen...";
				break;
			case Key.R:
				DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(FillPath));
				GetTree().ReloadCurrentScene();
				break;
			case Key.P:
				CyclePickDisplay();
				break;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_pouring)
		{
			return;
		}

		_batchElapsed += delta;
		_pourSeconds += delta;
		if (_batchElapsed < MinBatchSeconds)
		{
			return;
		}

		bool settled = _active.All(link => link.LinearVelocity.Length() < SettledSpeed);
		if (!settled && _batchElapsed < MaxBatchSeconds)
		{
			return;
		}

		FreezeDeepLinks();
		if (_spawned >= TargetLinks || _stopRequested)
		{
			List<RigidBody3D> stuck = _active.Where(link => !IsClearOfBin(link, _surfacePoints)).ToList();
			if (stuck.Count > 0 && _extraSettleSeconds < MaxExtraSettleSeconds)
			{
				stuck.ForEach(DropAgain);
				_extraSettleSeconds += _batchElapsed;
				_batchElapsed = 0.0;
				return;
			}
			FinishPour();
		}
		else
		{
			SpawnBatch();
		}
	}

	private void StartPour()
	{
		_shapes = LinkAssets.LoadCollisionShapes();
		_surfacePoints = _lod!.GetFaces().Distinct().ToArray();
		BuildPourCollision();
		_pouring = true;
		_pourStartedMs = Time.GetTicksMsec();
		SpawnBatch();
	}

	private void BuildPourCollision()
	{
		const float thick = 2f; // Dik, zodat een weggedrukte schakel terug de bak in wordt geduwd.
		float halfX = InnerLength / 2f + thick / 2f;
		float halfZ = InnerWidth / 2f + thick / 2f;
		float y = PourWallHeight / 2f;

		AddStaticBox(new Vector3(8f, 2f, 8f), new Vector3(0f, -1f, 0f));
		AddStaticBox(new Vector3(thick, PourWallHeight, 8f), new Vector3(halfX, y, 0f));
		AddStaticBox(new Vector3(thick, PourWallHeight, 8f), new Vector3(-halfX, y, 0f));
		AddStaticBox(new Vector3(8f, PourWallHeight, thick), new Vector3(0f, y, halfZ));
		AddStaticBox(new Vector3(8f, PourWallHeight, thick), new Vector3(0f, y, -halfZ));
	}

	private void AddStaticBox(Vector3 size, Vector3 position)
	{
		var body = new StaticBody3D { Position = position };
		body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
		AddChild(body);
	}

	private void SpawnBatch()
	{
		float startY = HighestLinkY() + SpawnClearance;
		float stepX = InnerLength / SpawnColumns;
		float stepZ = InnerWidth / SpawnRows;
		int cells = SpawnColumns * SpawnRows;
		int count = Mathf.Min(BatchLinks, TargetLinks - _spawned);

		for (int i = 0; i < count; i++)
		{
			int cell = i % cells;
			int layer = i / cells;
			var position = new Vector3(
				(cell % SpawnColumns + 0.5f) * stepX - InnerLength / 2f,
				startY + layer * 0.2f,
				(cell / SpawnColumns + 0.5f) * stepZ - InnerWidth / 2f);

			var body = new RigidBody3D
			{
				Mass = LinkMassKg,
				ContinuousCd = true,
				Position = position,
				Rotation = new Vector3(
					_random.RandfRange(-Mathf.Pi, Mathf.Pi),
					_random.RandfRange(-Mathf.Pi, Mathf.Pi),
					_random.RandfRange(-Mathf.Pi, Mathf.Pi)),
			};
			body.AddChild(new MeshInstance3D { Mesh = _lod });
			foreach ((Shape3D shape, Transform3D offset) in _shapes)
			{
				body.AddChild(new CollisionShape3D { Shape = shape, Transform = offset });
			}
			AddChild(body);
			_active.Add(body);
		}

		_spawned += count;
		_batchElapsed = 0.0;
		_status.Text = $"Bak vullen: {_spawned} van {TargetLinks} schakels, {_active.Count} bewegend. R = stoppen";
		GD.Print($"Storten: {_spawned} geplaatst, {_active.Count} actief, {_pourSeconds:F0} s gesimuleerd");
	}

	private float HighestLinkY()
	{
		float highest = 0f;
		foreach (RigidBody3D link in _active.Concat(_frozen))
		{
			highest = Mathf.Max(highest, link.GlobalPosition.Y);
		}
		return highest;
	}

	private void FreezeDeepLinks()
	{
		float limit = HighestLinkY() - FreezeDepth;
		foreach (RigidBody3D link in _active
			.Where(link => link.GlobalPosition.Y < limit && link.LinearVelocity.Length() < SettledSpeed).ToList())
		{
			if (IsClearOfBin(link, _surfacePoints))
			{
				link.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
				link.Freeze = true;
				_active.Remove(link);
				_frozen.Add(link);
			}
			else
			{
				DropAgain(link);
			}
		}
	}

	/// <summary>Een schakel die vastzit in de wand of bodem komt niet meer los, dus die valt opnieuw.</summary>
	private void DropAgain(RigidBody3D link)
	{
		link.GlobalPosition = new Vector3(
			_random.RandfRange(-InnerLength / 2f + 0.1f, InnerLength / 2f - 0.1f),
			HighestLinkY() + SpawnClearance,
			_random.RandfRange(-InnerWidth / 2f + 0.1f, InnerWidth / 2f - 0.1f));
		link.LinearVelocity = Vector3.Zero;
		link.AngularVelocity = Vector3.Zero;
		_droppedAgain++;
	}

	/// <summary>Een schakel die nog in de wand of bodem steekt, moet eerst door de physics worden teruggeduwd.</summary>
	private static bool IsClearOfBin(RigidBody3D link, Vector3[] points)
	{
		Transform3D transform = link.GlobalTransform;
		foreach (Vector3 point in points)
		{
			Vector3 world = transform * point;
			if (world.Y < -MaxOverlapWhenFrozen
				|| Mathf.Abs(world.X) > InnerLength / 2f + MaxOverlapWhenFrozen
				|| Mathf.Abs(world.Z) > InnerWidth / 2f + MaxOverlapWhenFrozen)
			{
				return false;
			}
		}
		return true;
	}

	private void FinishPour()
	{
		_pouring = false;
		var transforms = _active.Concat(_frozen).Select(link => link.GlobalTransform).ToList();
		SaveFill(transforms);
		ulong seconds = (Time.GetTicksMsec() - _pourStartedMs) / 1000;
		GD.Print($"Vulling opgeslagen in {seconds} s rekentijd, {_pourSeconds:F0} s gesimuleerd, {_droppedAgain} keer opnieuw laten vallen.");

		if (DisplayServer.GetName() == "headless")
		{
			Report(transforms);
			GetTree().Quit();
		}
		else
		{
			GetTree().ReloadCurrentScene();
		}
	}

	private static void SaveFill(List<Transform3D> transforms)
	{
		var rows = new Godot.Collections.Array();
		foreach (Transform3D t in transforms)
		{
			Quaternion q = t.Basis.GetRotationQuaternion();
			rows.Add(new Godot.Collections.Array
			{
				t.Origin.X, t.Origin.Y, t.Origin.Z, q.X, q.Y, q.Z, q.W,
			});
		}

		var data = new Godot.Collections.Dictionary { { "seed", RandomSeed }, { "links", rows } };
		using FileAccess file = FileAccess.Open(FillPath, FileAccess.ModeFlags.Write);
		file.StoreString(Json.Stringify(data));
	}

	private static List<Transform3D> LoadFill()
	{
		using FileAccess file = FileAccess.Open(FillPath, FileAccess.ModeFlags.Read);
		var data = Json.ParseString(file.GetAsText()).AsGodotDictionary();
		var result = new List<Transform3D>();
		foreach (Variant row in data["links"].AsGodotArray())
		{
			var v = row.AsGodotArray();
			var rotation = new Quaternion(v[3].AsSingle(), v[4].AsSingle(), v[5].AsSingle(), v[6].AsSingle());
			var origin = new Vector3(v[0].AsSingle(), v[1].AsSingle(), v[2].AsSingle());
			result.Add(new Transform3D(new Basis(rotation.Normalized()), origin));
		}
		return result;
	}

	private void ShowStoredFill()
	{
		List<Transform3D> transforms = LoadFill();
		var multiMesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			Mesh = _lod,
			InstanceCount = transforms.Count,
		};
		for (int i = 0; i < transforms.Count; i++)
		{
			multiMesh.SetInstanceTransform(i, transforms[i]);
		}
		AddChild(new MultiMeshInstance3D { Multimesh = multiMesh });
		_sensorStatus = $"{transforms.Count} schakels in de bak";
		_status.Text = _sensorStatus;
		Report(transforms);
	}

	private void Report(List<Transform3D> transforms)
	{
		Vector3[] points = _lod!.GetFaces().Distinct().ToArray();
		int outside = 0;
		float deepestWall = 0f;
		int aboveRim = 0;
		float top = 0f;
		float deepest = 0f;
		foreach (Transform3D t in transforms)
		{
			float low = float.MaxValue;
			float high = float.MinValue;
			float reachX = 0f;
			float reachZ = 0f;
			foreach (Vector3 point in points)
			{
				Vector3 world = t * point;
				low = Mathf.Min(low, world.Y);
				high = Mathf.Max(high, world.Y);
				reachX = Mathf.Max(reachX, Mathf.Abs(world.X));
				reachZ = Mathf.Max(reachZ, Mathf.Abs(world.Z));
			}
			top = Mathf.Max(top, high);
			deepest = Mathf.Max(deepest, -low);
			deepestWall = Mathf.Max(deepestWall, Mathf.Max(reachX - InnerLength / 2f, reachZ - InnerWidth / 2f));
			if (reachX > InnerLength / 2f + 0.005f || reachZ > InnerWidth / 2f + 0.005f)
			{
				outside++;
			}
			if (high > RimHeight)
			{
				aboveRim++;
			}
		}

		GD.Print($"INFO Schakels: {transforms.Count} (een volle bak is {TargetLinks})");
		GD.Print($"{(deepestWall < PlankThickness ? "OK  " : "FOUT")} Diepst in de wand: {deepestWall * 1000f:F1} mm " +
				 $"(plank {PlankThickness * 1000f:F0} mm); {outside} schakels meer dan 5 mm");
		GD.Print($"{(deepest <= 0.005f ? "OK  " : "FOUT")} Diepst in de bodem: {deepest * 1000f:F1} mm (maximaal 5 mm)");
		GD.Print($"INFO Hoogste punt: {top * 1000f:F0} mm, rand op {RimHeight * 1000f:F0} mm; boven de rand: {aboveRim} schakels");
	}

	private void BuildEnvironment()
	{
		AddChild(new WorldEnvironment
		{
			Environment = new Godot.Environment
			{
				BackgroundMode = Godot.Environment.BGMode.Color,
				BackgroundColor = new Color(0.78f, 0.8f, 0.83f),
				AmbientLightSource = Godot.Environment.AmbientSource.Color,
				AmbientLightColor = Colors.White,
				AmbientLightEnergy = 0.45f,
			},
		});
		AddChild(new DirectionalLight3D
		{
			RotationDegrees = new Vector3(-65f, -30f, 0f),
			LightEnergy = 1.6f,
			ShadowEnabled = true,
		});
		AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-40f, 150f, 0f), LightEnergy = 0.6f });

		AddVisualBox(new Vector3(8f, 0.02f, 8f), new Vector3(0f, -PalletHeight - 0.01f, 0f), new Color(0.62f, 0.63f, 0.65f));
	}

	private void BuildPallet()
	{
		// Dek met planken, met daaronder klossen.
		const float deckHeight = 0.022f;
		const int boards = 7;
		float boardWidth = CollarOuterWidth / boards * 0.86f;
		for (int i = 0; i < boards; i++)
		{
			float z = ((i + 0.5f) / boards - 0.5f) * CollarOuterWidth;
			AddVisualBox(new Vector3(CollarOuterLength, deckHeight, boardWidth), new Vector3(0f, -deckHeight / 2f, z), WoodColor);
		}
		// Dichte bodem, want de schakels liggen op het dek.
		AddVisualBox(new Vector3(InnerLength, 0.002f, InnerWidth), new Vector3(0f, -0.001f, 0f), WoodColor.Darkened(0.25f));

		float blockHeight = PalletHeight - deckHeight;
		foreach (float x in new[] { -0.55f, 0f, 0.55f })
		{
			foreach (float z in new[] { -0.35f, 0f, 0.35f })
			{
				AddVisualBox(new Vector3(0.1f, blockHeight, 0.1f), new Vector3(x, -deckHeight - blockHeight / 2f, z), WoodColor.Darkened(0.15f));
			}
		}

		for (int i = 0; i < CollarCount; i++)
		{
			BuildCollar(i * CollarHeight);
		}
	}

	private void BuildCollar(float bottom)
	{
		const float gap = 0.004f; // Zichtbare naad tussen de opzetranden.
		float height = CollarHeight - gap;
		float y = bottom + gap + height / 2f;
		float offsetX = (CollarOuterLength - PlankThickness) / 2f;
		float offsetZ = (CollarOuterWidth - PlankThickness) / 2f;

		AddVisualBox(new Vector3(CollarOuterLength, height, PlankThickness), new Vector3(0f, y, offsetZ), WoodColor);
		AddVisualBox(new Vector3(CollarOuterLength, height, PlankThickness), new Vector3(0f, y, -offsetZ), WoodColor);
		AddVisualBox(new Vector3(PlankThickness, height, InnerWidth), new Vector3(offsetX, y, 0f), WoodColor.Darkened(0.08f));
		AddVisualBox(new Vector3(PlankThickness, height, InnerWidth), new Vector3(-offsetX, y, 0f), WoodColor.Darkened(0.08f));

		// Metalen hoekbeslag aan de buitenkant.
		const float plate = 0.09f;
		const float metal = 0.003f;
		foreach (float sx in new[] { -1f, 1f })
		{
			foreach (float sz in new[] { -1f, 1f })
			{
				float faceX = sx * (CollarOuterLength / 2f + metal / 2f);
				float faceZ = sz * (CollarOuterWidth / 2f + metal / 2f);
				AddVisualBox(new Vector3(metal, height * 0.75f, plate),
					new Vector3(faceX, y, sz * (CollarOuterWidth / 2f - plate / 2f)), MetalColor);
				AddVisualBox(new Vector3(plate, height * 0.75f, metal),
					new Vector3(sx * (CollarOuterLength / 2f - plate / 2f), y, faceZ), MetalColor);
			}
		}
	}

	private void AddVisualBox(Vector3 size, Vector3 position, Color color)
	{
		AddChild(new MeshInstance3D
		{
			Position = position,
			Mesh = new BoxMesh
			{
				Size = size,
				Material = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.85f },
			},
		});
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

	private void ShowOverview() => _camera.SetView(new Vector3(0f, 0.5f, 0f), 28f, 55f, 2.8f);

	private void ShowBinCamera() => _camera.SetView(new Vector3(0f, RimHeight, 0f), 0f, 90f, SensorAboveRim - 0.03f);

	private void BuildHelp()
	{
		var layer = new CanvasLayer();
		AddChild(layer);
		_sensorView = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_sensorView.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(_sensorView);
		var panel = new PanelContainer { Position = new Vector2(12f, 12f) };
		layer.AddChild(panel);
		var box = new VBoxContainer();
		panel.AddChild(box);
		_status = new Label();
		box.AddChild(_status);
		box.AddChild(new Label
		{
			Text = "Muis: slepen = draaien, middelste knop = verschuiven, wiel = zoomen\n" +
				   "1 = overzicht, 2 = vanaf de sensor, 3 = dichtbij\n" +
				   "R = vullen starten, nog een keer R = stoppen op deze hoogte\n" +
				   "4 = 3D / perfecte diepte / sensor, B = zwarte of lichte schakels\n" +
				   "P = kandidaten (blauw), nog een keer = keuze (groen), nog een keer = verbergen",
		});
	}

	private async Task MeasureAndCapture()
	{
		if (DisplayServer.GetName() == "headless")
		{
			return;
		}

		_rig = new DepthSensorRig();
		AddChild(_rig);
		float height = RimHeight + SensorAboveRim;
		// Hoogte van het beeld: de korte kant van de pallet bij de rand, met marge.
		float verticalFov = 2f * Mathf.RadToDeg(Mathf.Atan(RimHalfViewMeters / SensorAboveRim));
		_rig.Configure(new Vector3(0f, height, 0f), verticalFov, _sensorSettings.ProjectorBaseline);

		(_idealDepth, _projectorDepth, _reflectance) = await _rig.CaptureAsync();
		CheckRimDistance();
		ShowSensorResult();
		await CaptureIfRequested();
	}

	/// <summary>De bovenkant van de rand ligt op een bekende hoogte: dat controleert de hele keten.</summary>
	private void CheckRimDistance()
	{
		PinholeCamera camera = _rig!.Camera;
		float rimX = InnerLength / 2f + PlankThickness / 2f;
		int x = Mathf.RoundToInt(camera.Cx + camera.Fx * rimX / SensorAboveRim);
		int y = camera.Height / 2;
		float measured = _idealDepth[y * camera.Width + x];
		bool passed = Mathf.Abs(measured - SensorAboveRim) <= 0.005f;
		GD.Print($"{(passed ? "OK  " : "FOUT")} Afstand tot de rand: {measured * 1000f:F1} mm, " +
				 $"verwacht {SensorAboveRim * 1000f:F0} mm");
	}

	private void ShowSensorResult()
	{
		if (_rig == null || _idealDepth.Length == 0)
		{
			return;
		}

		float[] reflectance = _reflectance;
		if (_lightParts)
		{
			reflectance = reflectance.Select(r => r < PartReflectanceLimit ? LightPartReflectance : r).ToArray();
		}

		PinholeCamera camera = _rig.Camera;
		DepthSensorResult result = DepthSensorModel.Apply(camera, _idealDepth, _projectorDepth, reflectance, _sensorSettings);
		ReportSensor(result, reflectance);
		_sensorResult = result;
		_grasps.Clear();
		if (_pickMode != PickMode.Hidden)
		{
			FindGrasps(result);
		}
		ShowPickDisplay();
	}

	private DepthSensorResult? _sensorResult;
	private enum PickMode { Hidden, Candidates, Best }

	private PickMode _pickMode = PickMode.Hidden;
	private string _sensorStatus = string.Empty;
	private string _pickStatus = string.Empty;
	private string _bestPickStatus = string.Empty;
	private readonly GraspSettings _graspSettings = new();
	private List<GraspCandidate> _grasps = new();
	private readonly List<Node3D> _graspMarkers = new();

	private void ReportSensor(DepthSensorResult result, float[] reflectance)
	{
		int parts = 0, partLow = 0, partShadow = 0, partEdge = 0, other = 0, otherMissing = 0;
		double errorSum = 0;
		int errorCount = 0;
		for (int i = 0; i < result.Depth.Length; i++)
		{
			if (_reflectance[i] < PartReflectanceLimit)
			{
				parts++;
				switch (result.Missing[i])
				{
					case MissingReason.LowSignal: partLow++; break;
					case MissingReason.Shadow: partShadow++; break;
					case MissingReason.Edge: partEdge++; break;
					default:
						double error = result.Depth[i] - _idealDepth[i];
						errorSum += error * error;
						errorCount++;
						break;
				}
			}
			else
			{
				other++;
				if (result.Missing[i] != MissingReason.None)
				{
					otherMissing++;
				}
			}
		}

		double Percent(int value, int total) => total == 0 ? 0.0 : 100.0 * value / total;
		string kind = _lightParts ? "lichte schakels" : "zwarte schakels";
		int partMissing = partLow + partShadow + partEdge;
		double noiseMm = errorCount == 0 ? 0.0 : System.Math.Sqrt(errorSum / errorCount) * 1000.0;
		string text = $"{kind}: {Percent(partMissing, parts):F0}% zonder meting " +
					  $"(weinig licht {Percent(partLow, parts):F0}%, schaduw {Percent(partShadow, parts):F0}%, " +
					  $"rand {Percent(partEdge, parts):F0}%), ruis {noiseMm:F1} mm";
		GD.Print($"INFO Sensor, {text}; hout en achtergrond {Percent(otherMissing, other):F0}% zonder meting");
		_sensorStatus = text;
	}

	/// <summary>Toets P: niets, dan alle kandidaten, dan ook het beste pickpunt.</summary>
	private void CyclePickDisplay()
	{
		if (_rig == null || _sensorResult == null)
		{
			return;
		}

		_pickMode = _pickMode switch
		{
			PickMode.Hidden => PickMode.Candidates,
			PickMode.Candidates => PickMode.Best,
			_ => PickMode.Hidden,
		};
		if (_pickMode == PickMode.Candidates)
		{
			FindGrasps(_sensorResult);
		}
		ShowPickDisplay();
	}

	private void ShowPickDisplay()
	{
		string line = _pickMode switch
		{
			PickMode.Hidden => "Druk op P om pickpunten te zoeken",
			PickMode.Candidates => _pickStatus,
			_ => _grasps.Count > 0 ? _bestPickStatus : _pickStatus,
		};
		_status.Text = $"{_sensorStatus}\n{line}";
		ShowGraspMarkers();
		UpdateSensorView();
	}

	private void FindGrasps(DepthSensorResult result)
	{
		PinholeCamera camera = _rig!.Camera;
		// De bak vanuit de camera; camera-Y wijst naar wereld-Z.
		var bin = new BinRegion(
			-InnerLength / 2f + WallMargin, InnerLength / 2f - WallMargin,
			-InnerWidth / 2f + WallMargin, InnerWidth / 2f - WallMargin,
			SensorAboveRim - 0.05f, SensorAboveRim + RimHeight - MinPickHeightAboveFloor);

		ulong started = Time.GetTicksMsec();
		(_grasps, GraspSearchReport report) = GraspPlanner.FindGrasps(camera, result.Depth, bin, _graspSettings);
		ulong milliseconds = Time.GetTicksMsec() - started;

		GD.Print($"INFO Pickpunten: {report.Evaluated} beoordeeld in {milliseconds} ms; " +
				 $"te weinig metingen {report.TooFewPoints}, niet vlak {report.NotFlat}, te schuin {report.TooTilted}, " +
				 $"houder raakt {report.ToolCollision}, buiten opening {report.OutsideOpening}, goed {report.Accepted}");

		if (_grasps.Count == 0)
		{
			GD.Print("INFO Geen pickpunt gevonden.");
			_pickStatus = "Geen pickpunt gevonden";
			return;
		}

		GraspCandidate best = _grasps[0];
		float bestSeal = GraspPlanner.TrueSealShare(camera, _idealDepth, best, _graspSettings);
		int good = _grasps.Count(g => GraspPlanner.TrueSealShare(camera, _idealDepth, g, _graspSettings) >= GoodSealShare);
		GD.Print($"INFO Beste pickpunt: {best.ValidShare * 100f:F0}% gemeten, {best.InlierShare * 100f:F0}% in het vlak, " +
				 $"{best.TiltDegrees:F0} graden schuin; werkelijke afdichting {bestSeal * 100f:F0}%");
		GD.Print($"INFO Controle met perfecte diepte: {good} van de {_grasps.Count} beste kandidaten dichten minstens " +
				 $"{GoodSealShare * 100f:F0}% af");
		_pickStatus = $"{report.Accepted} plekken goedgekeurd, de {_grasps.Count} beste getoond; " +
					  $"{good} dichten echt af. Druk op P voor de keuze";
		_bestPickStatus = $"Gekozen: {best.ValidShare * 100f:F0}% gemeten, {best.TiltDegrees:F0} graden schuin, " +
						  $"werkelijke afdichting {bestSeal * 100f:F0}%";
	}

	private void ShowGraspMarkers()
	{
		foreach (Node3D marker in _graspMarkers)
		{
			marker.QueueFree();
		}
		_graspMarkers.Clear();

		for (int i = 0; i < _grasps.Count; i++)
		{
			GraspCandidate grasp = _grasps[i];
			bool best = i == 0 && _pickMode == PickMode.Best;
			// Camera-X = wereld-X, camera-Y = wereld-Z, camera-Z wijst omlaag.
			float height = RimHeight + SensorAboveRim;
			var point = new Vector3(grasp.X, height - grasp.Z, grasp.Y);
			var normal = new Vector3(grasp.NormalX, -grasp.NormalZ, grasp.NormalY).Normalized();
			Color color = best ? new Color(0.1f, 1f, 0.2f) : new Color(0.1f, 0.8f, 1f);
			var material = new StandardMaterial3D
			{
				AlbedoColor = color,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			};

			var marker = new Node3D { Position = point };
			Vector3 helper = Mathf.Abs(normal.Dot(Vector3.Forward)) > 0.9f ? Vector3.Right : Vector3.Forward;
			Vector3 x = helper.Cross(normal).Normalized();
			marker.Basis = new Basis(x, normal, x.Cross(normal));
			float radius = _graspSettings.CupDiameter / 2f;
			float stem = best ? 0.25f : 0.06f;
			marker.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.003f, Material = material },
				Position = Vector3.Up * 0.003f,
				Layers = 1u << (DepthSensorRig.ModelLayer - 1),
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			});
			marker.AddChild(new MeshInstance3D
			{
				Mesh = new CylinderMesh { TopRadius = 0.003f, BottomRadius = 0.003f, Height = stem, Material = material },
				Position = Vector3.Up * stem / 2f,
				Layers = 1u << (DepthSensorRig.ModelLayer - 1),
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			});
			AddChild(marker);
			_graspMarkers.Add(marker);
		}
	}

	private void DrawGraspsOnImage(byte[] pixels, int width, int height)
	{
		for (int i = _grasps.Count - 1; i >= 0; i--)
		{
			GraspCandidate grasp = _grasps[i];
			bool isBest = i == 0 && _pickMode == PickMode.Best;
			(byte r, byte g, byte b) = isBest ? ((byte)20, (byte)255, (byte)40) : ((byte)30, (byte)210, (byte)255);
			float radius = _rig!.Camera.Fx * (_graspSettings.CupDiameter / 2f) / grasp.Z;
			int steps = 128;
			for (int s = 0; s < steps; s++)
			{
				float angle = s * Mathf.Tau / steps;
				for (float extra = 0f; extra < (isBest ? 3f : 2f); extra += 1f)
				{
					int x = grasp.PixelX + Mathf.RoundToInt((radius + extra) * Mathf.Cos(angle));
					int y = grasp.PixelY + Mathf.RoundToInt((radius + extra) * Mathf.Sin(angle));
					if (x >= 0 && y >= 0 && x < width && y < height)
					{
						int at = (y * width + x) * 3;
						pixels[at] = r;
						pixels[at + 1] = g;
						pixels[at + 2] = b;
					}
				}
			}
		}
	}

	private void CycleSensorView()
	{
		if (_rig == null || _sensorResult == null)
		{
			return;
		}
		_viewMode = (_viewMode + 1) % 3;
		UpdateSensorView();
	}

	private void UpdateSensorView()
	{
		if (_rig == null || _sensorResult == null)
		{
			return;
		}

		_sensorView.Visible = _viewMode != 0;
		if (_viewMode != 0)
		{
			byte[] pixels = _viewMode == 1 ? DrawDepth(_idealDepth, null) : DrawDepth(_sensorResult.Depth, _sensorResult.Missing);
			PinholeCamera camera = _rig.Camera;
			DrawGraspsOnImage(pixels, camera.Width, camera.Height);
			Image image = Image.CreateFromData(camera.Width, camera.Height, false, Image.Format.Rgb8, pixels);
			_sensorView.Texture = ImageTexture.CreateFromImage(image);
		}
	}

	/// <summary>Dichtbij is licht en ver weg donker. Gaten: magenta = weinig licht, blauw = schaduw, geel = rand.</summary>
	private byte[] DrawDepth(float[] depth, MissingReason[]? missing)
	{
		const float near = SensorAboveRim - 0.05f;
		float far = SensorAboveRim + RimHeight + 0.05f;
		var pixels = new byte[depth.Length * 3];
		for (int i = 0; i < depth.Length; i++)
		{
			byte r, g, b;
			if (missing != null && missing[i] != MissingReason.None)
			{
				(r, g, b) = missing[i] switch
				{
					MissingReason.LowSignal => ((byte)230, (byte)30, (byte)160),
					MissingReason.Shadow => ((byte)40, (byte)90, (byte)230),
					_ => ((byte)240, (byte)210, (byte)30),
				};
			}
			else
			{
				float t = Mathf.Clamp((far - depth[i]) / (far - near), 0f, 1f);
				r = g = b = (byte)(t * 255f);
			}
			pixels[i * 3] = r;
			pixels[i * 3 + 1] = g;
			pixels[i * 3 + 2] = b;
		}
		return pixels;
	}

	private async Task CaptureIfRequested()
	{
		// Alleen voor controles: godot ... -- --screenshot=pad.png --view=2
		string[] args = OS.GetCmdlineUserArgs();
		string? path = args.FirstOrDefault(a => a.StartsWith("--screenshot="))?.Split('=')[1];
		if (path == null)
		{
			return;
		}

		string view = args.FirstOrDefault(a => a.StartsWith("--view="))?.Split('=')[1] ?? "1";
		if (view == "2")
		{
			ShowBinCamera();
		}
		else if (view == "3")
		{
			_camera.SetView(new Vector3(0f, RimHeight - 0.05f, 0f), 30f, 45f, 1.1f);
		}
		else if (view == "kand")
		{
			CyclePickDisplay();
		}
		else if (view == "pick")
		{
			CyclePickDisplay();
			CyclePickDisplay();
			GraspCandidate? best = _grasps.FirstOrDefault();
			if (best != null)
			{
				float height = RimHeight + SensorAboveRim;
				_camera.SetView(new Vector3(best.X, height - best.Z, best.Y), 30f, 55f, 0.8f);
			}
		}
		else if (view == "ideaal" || view == "sensor" || view == "licht")
		{
			_viewMode = view == "ideaal" ? 1 : 2;
			_lightParts = view == "licht";
			ShowSensorResult();
			CyclePickDisplay();
			CyclePickDisplay();
		}

		for (int i = 0; i < 6; i++)
		{
			await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		}
		GetViewport().GetTexture().GetImage().SavePng(path);
		GetTree().Quit();
	}
}
