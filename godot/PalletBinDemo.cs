#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
	private double _batchElapsed;
	private double _extraSettleSeconds;
	private double _pourSeconds;
	private ulong _pourStartedMs;
	private int _spawned;
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
			_ = CaptureIfRequested();
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
			case Key.R when !_pouring:
				DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(FillPath));
				GetTree().ReloadCurrentScene();
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
		if (_spawned >= TargetLinks)
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
		_status.Text = $"Bak vullen: {_spawned} van {TargetLinks} schakels, {_active.Count} bewegend";
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
		_status.Text = $"{transforms.Count} schakels in de bak";
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

		GD.Print($"{(transforms.Count == TargetLinks ? "OK  " : "FOUT")} Schakels: {transforms.Count} van {TargetLinks}");
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
		_camera = new OrbitCamera { Fov = 45f, Near = 0.02f, Far = 30f };
		AddChild(_camera);
		_camera.MakeCurrent();
		ShowOverview();
	}

	private void ShowOverview() => _camera.SetView(new Vector3(0f, 0.5f, 0f), 28f, 55f, 2.8f);

	private void ShowBinCamera() => _camera.SetView(new Vector3(0f, RimHeight, 0f), 0f, 90f, 2.2f);

	private void BuildHelp()
	{
		var layer = new CanvasLayer();
		AddChild(layer);
		var panel = new PanelContainer { Position = new Vector2(12f, 12f) };
		layer.AddChild(panel);
		var box = new VBoxContainer();
		panel.AddChild(box);
		_status = new Label();
		box.AddChild(_status);
		box.AddChild(new Label
		{
			Text = "Muis: slepen = draaien, middelste knop = verschuiven, wiel = zoomen\n" +
				   "1 = overzicht, 2 = camera boven de bak, 3 = dichtbij, R = opnieuw vullen",
		});
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

		for (int i = 0; i < 10; i++)
		{
			await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		}
		GetViewport().GetTexture().GetImage().SavePng(path);
		GetTree().Quit();
	}
}
