# Architecture

The code has one rule: **algorithms do not know Godot**. They live in a plain .NET library that can be tested
without the engine. Godot code handles the scene, rendering and physics, and calls into that library.

```text
tools/cad_pipeline  ->  godot/generated  ->  godot (scene, rendering, physics)
                                                |
                                                v
                                      src/BinPicking.Core (algorithms)
                                                ^
                                                |
                                      tests/BinPicking.Core.Tests
```

The dependency only points one way. `BinPicking.Core` references nothing else, so a real camera can later feed
the same algorithms.

## Repository

| Folder | Contents | In Git |
|---|---|---|
| `src/BinPicking.Core/` | Algorithms without Godot | Yes |
| `tests/BinPicking.Core.Tests/` | Tests of those algorithms | Yes |
| `godot/` | Godot project | Yes |
| `godot/generated/` | Generated meshes and the stored fill | No |
| `tools/cad_pipeline/` | Scripts from STEP to Godot | Yes |
| `cad/` | The downloaded igus model | No |
| `docs/` | Documentation | Yes |

## `BinPicking.Core`

| Module | Class | Responsibility |
|---|---|---|
| `Sensing` | `PinholeCamera` | Converts between pixels and camera-space points |
| | `DepthSensorModel` | Turns perfect depth into a realistic measurement |
| | `DepthSensorSettings` | Settings of the sensor model |
| | `DepthSensorResult`, `MissingReason` | Measured depth, and why a pixel has none |
| | `DepthSensorStatistics` | Summarises a measurement |
| `Grasping` | `GraspEvaluator` | **Judges one location**: names the failed requirement or returns a candidate |
| | `GraspPlanner` | Scans the image, counts outcomes and ranks the candidates |
| | `SealVerifier` | Checks a candidate against perfect depth, in simulation only |
| | `PlaneFitter`, `Plane`, `PointSet` | Internal plane fit |
| | `GraspSettings`, `BinRegion` | Requirements, and the bin in camera space |
| | `GraspCandidate`, `GraspEvaluation`, `GraspRejection`, `GraspSearchReport` | Results |

## `godot`

| Module | Class | Responsibility |
|---|---|---|
| `Bin` | `PalletDimensions` | All measurements of the pallet |
| | `PalletBin` | The visible pallet |
| | `BinFiller` | Fills the bin with physics |
| | `BinFillStore` | Saves and loads a fill |
| | `BinFillView` | Draws a stored fill |
| | `BinFillInspector` | Checks a fill geometrically |
| `Links` | `LinkAssets` | Loads the generated link files |
| `Sensor` | `DepthSensorRig` | Renders perfect depth and reflectance |
| | `SensorMount` | Sensor position and the camera-to-world mapping |
| | `SensorMeasurement` | Applies the sensor model to a capture |
| | `SensorImage` | Draws depth as an image |
| `Picking` | `PickSelection` | Runs the search and tracks what is displayed |
| | `GraspMarkers` | Shows pick points in 3D |
| | `GraspImageOverlay` | Shows pick points on a sensor image |
| | `PickDisplayMode` | The display steps |
| `Presentation` | `OrbitCamera`, `DemoHud`, `DemoEnvironment` | Camera, on-screen text, lights and ground |
| `Demo` | `PalletBinDemo` | Composition root: connects the modules and handles the keys |
| `Tests` | `DepthTest`, `LinkPhysicsTest` | Scripts of the test scenes |
| `Scenes` | `*.tscn` | The scenes to start |

## Where does new code go?

| New code | Location |
|---|---|
| An algorithm that needs no engine | `BinPicking.Core`, with a test |
| A change to what makes a good pick point | `GraspEvaluator` and `GraspSettings` |
| A pallet measurement | `PalletDimensions` |
| A key or a connection between modules | `PalletBinDemo`; its behaviour belongs in a module |

## Language

Documentation, code comments and test names are in English. Text on screen and in the Godot Output panel,
as well as `convert_link.py` output names such as `schakel_visual.glb`, is still in Dutch.

## Not done yet

The modules are folders in one Godot project. They could become Godot addons with a `plugin.cfg`. There is no
need yet, because nothing uses them outside this project.
