# Testing

| Test | Verifies | Passes when |
|---|---|---|
| `Scenes/depth_test.tscn` | Does depth arrive accurately in C#? | Three measurements deviate at most 1 mm |
| `Scenes/link_physics_test.tscn` | Do import, scale and stacking work? | No line with `FOUT` |
| `Scenes/pallet_bin.tscn` | Does the fill fit and is the sensor distance right? | No line with `FOUT` |
| `tests/BinPicking.Core.Tests` | Are the sensor model and pick search correct? | `RESULT: PASSED` |

For a scene: open it in Godot, click the hammer and press F6. Results appear in the Output panel, in Dutch: `OK`
or `FOUT` (failed), and `RESULTAAT: GESLAAGD` (result: passed).

For the library:

```powershell
dotnet run --project tests/BinPicking.Core.Tests
```

These tests use no extra packages. They cover the sensor model, each reason `GraspEvaluator` can reject a
location for, the search, and the statistics.

## Depth test

A camera looks down on blocks of a known height. The test uses perfect depth, so it says nothing about a real
camera on black parts.

Result on an RTX 4070 Laptop GPU with Direct3D 12: 0.00 mm deviation on all three points.

## Physics test

The test drops 30 links into a 50 x 50 cm bin, waits until they lie still and then lifts the top link by 30 cm.

| Output line | Meaning |
|---|---|
| Afmeting | Are import and scale correct? |
| Tot rust | Does the pile come to rest within 20 s? |
| Buiten de bak | Did any links escape? |
| Diepst in de vloer | How far does the visible model sink into the floor? The limit is 4 mm |
| Physics-tijd | How much time does a step take? At 60 Hz, 16.7 ms is available |
| Optillen | How many links move along? This is information, not a requirement |

The 4 mm limit is the measured deviation of the collision shape (3 mm) plus 1 mm of slack. The mass of 0.15 kg per
link is an assumption, not an igus value.

### Physics settings

The Jolt defaults suit game objects, not parts of a few centimetres. `project.godot` therefore contains:

| Setting | Default | Here |
|---|---|---:|
| `penetration_slop` | 0.02 m | 0.0005 m |
| `speculative_contact_distance` | 0.02 m | 0.002 m |

With the defaults the model sank up to 20.3 mm into the floor, which makes stacking and hooking unreliable. A
floor of 5 cm also proved too weak: a link was once pushed through it. The floor is therefore 50 cm thick and the
walls are 30 cm in this test.

### Results

Measured with Godot 4.7.2 without a display on a Linux cloud computer:

| Seed | To rest | In the floor | Physics avg. / max | Moved of 29 | Highest rise |
|---|---|---|---|---|---|
| 1 | 6.9 s | 3.4 mm | 11.1 / 28.2 ms | 0 | 0.3 mm |
| 2 | 5.0 s | 2.7 mm | 6.3 / 8.7 ms | 1 | 1.0 mm |
| 3 | 2.7 s | 2.7 mm | 13.3 / 21.2 ms | 14 | 22.5 mm |

One run on the Windows laptop (RTX 4070 Laptop GPU) gave: at rest after 11.8 s, 2.0 mm in the floor, physics 3.3 ms
on average and at most 4.5 ms per step, and 13 of 29 links moved along.

- **Hooking occurs and varies widely.** This makes a failed pick visible and measurable.
- **Compute time is the limit.** The bin must therefore keep almost all links frozen.

Timings differ between runs, so do not compare single measurements.

## Pallet

| Output line | Meaning |
|---|---|
| Schakels | How many links are in the fill? |
| Diepst in de wand | Does a link stick through the 20 mm plank? |
| Diepst in de bodem | Does a link sink through the floor? The limit is 5 mm |
| Afstand tot de rand | Does the sensor measure the known distance to the rim? |
| Hoogste punt | How full is the bin? This is information, not a requirement |

The full reference fill, made on the Linux cloud computer: 720 links, 2.8 mm in the wall, 2.8 mm in the floor,
highest point at 962 mm, inside the 1000 mm rim. Overlap between links is not measured yet.

## After the refactor

The restructuring into modules (before the middle requirement was added) changed no behaviour on the reference fills: fill, sensor percentages, search
counts and best candidates were identical to before. Only `godot` paths and documentation changed.
