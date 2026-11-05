# From CAD to Godot

Godot cannot read STEP. STEP describes exact mathematical surfaces, while Godot works with triangles. A single
link also needs three different shapes, each for its own purpose.

```text
igus site -> cad/r4.32.stp -> convert_link.py -> godot/generated/ -> Godot
                  |                                     |
          inspect_step.py                        check_link.py
```

## Steps

| Step | Script | What happens | Why |
|---|---|---|---|
| 1. Inspect | `inspect_step.py` | Shows parts, sizes and unit | First know what the file contains |
| 2. Select | `convert_link.py` | Takes the largest watertight link from the chain | igus supplies at least 9 links; measuring volume needs a closed mesh |
| 3. Straighten | `convert_link.py` | Puts the link straight and centred | The links are bent in the chain |
| 4. Simplify | `convert_link.py` | Creates a light version | Thousands of full links are too heavy |
| 5. Collision shape | `convert_link.py` | Creates convex parts with `coacd` | Physics engines calculate quickly with convex shapes |
| 6. Measure suction surface | `convert_link.py` | Finds the largest circle on both flat faces | A suction cup needs a closed surface |
| 7. Verify | `check_link.py` | Measures the deviation of the simple models | Simplifying can change the shape too much |

## Result

Axes: X is the chain length, Y the top and Z the width. All sizes are in metres.

| File | Purpose | Contents | Measured quality |
|---|---|---|---|
| `schakel_visual.glb` | Exact image | 16,392 triangles | 110.2 x 54.0 x 122.0 mm |
| `schakel_lod.glb` | The links in the bin | 5,000 triangles | Median 0.3 mm, at most 2.1 mm deviation; 56.1 mm high |
| `schakel_collision.glb` | Physics | 24 convex parts | 129.2 cm³ against 123.3 cm³, at most 3.0 mm deviation |
| `schakel_rapport.json` | All measured values | | |

## Suction surface

| Face | Size | Largest circle |
|---|---|---|
| Top (Y+) | 47 x 100 mm | Ø 47 mm |
| Bottom (Y-) | 41 x 100 mm | Ø 41 mm |

Inside those circles the face is closed. Elsewhere the link protrudes up to 2 mm above the face, so the rim or
holder of the cup needs room. The provisional choice is a Ø 30 mm cup.

## Settings you can change

| Setting | Value | Effect |
|---|---|---|
| `LOD_FACES` | 5000 | 1,500 gave more than 5 mm deviation; 8,000 stayed under 1 mm |
| `--max-hulls` | 24 | More parts are more accurate but slower in physics |
| `--threshold` | 0.03 | Lower is more accurate, but quickly gives more parts |

The calculation stopped at the maximum of 24 parts, so this is a deliberate upper limit.

## After a new download

Run `inspect_step.py`, `convert_link.py` and `check_link.py` again and compare the sizes with the table above.
Part names change with each download, so they are not a fixed identifier.
