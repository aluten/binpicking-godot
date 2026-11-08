# Experiment: measuring with projected light

**Status:** experiment on the branch `experiment/structured-light`. In the demo, key **L** switches between this and
the formula in [sensor-model.md](sensor-model.md); the demo starts with the formula. The sensor values below are
illustrative, and this does not predict what a real camera measures.

## What it does

1. A Godot `SpotLight3D` projects a pattern with `light_projector`, and a virtual camera takes an image.
2. `CameraNoiseModel` adds shot noise, read noise, clipping and rounding.
3. `PatternDecoder` decodes all images into a projector position per pixel.
4. `ProjectorModel` triangulates that position into depth.

The result has the same form as the sensor model: depth in metres with NaN for a hole. The algorithms are in
`src/BinPicking.Core/StructuredLight`, tested without Godot.

## Patterns

One measurement takes 11 images, in a pattern 2048 columns wide:

| Images | Pattern | Gives |
|---|---|---|
| 4 | Sine with a period of 32 columns, shifted a quarter period each time | Precise position within a period, and the signal strength |
| 7 | Gray code stripes, finest stripe 16 columns | Which period it is |

The stripes alone would be too coarse. One stripe of 16 columns is about 16 mm at 1.5 m, which with a 15 cm baseline is
roughly 160 mm in depth.

## Running it

In the demo: open `Scenes/pallet_bin.tscn`, press F6 and then **L**. The status line names the sensor, and key **4**
shows the measured image. Every new measurement, also after a pick, uses projected light until L is pressed again.
Holes are magenta for too little signal (or shadow), yellow where the patterns disagree and orange for overexposed.
For an automated series: `-- --light --picks=20`.

| Class | Role |
|---|---|
| `ProjectedLightRig` | Godot: a world of its own with only the pallet and links, a camera and the spot light |
| `SensorMeasurement` | Chooses the formula or the light, adds the noise and decodes; the rest of the demo sees no difference |
| `SensorKind` | The two choices |

The rig has its own world because the lights of the demo view would otherwise also light the bin in the images.
Key **E** steps through the exposure settings, and key **K** shows the camera images one by one; see below.

The separate test scene stays: open `Scenes/structured_light_test.tscn` and press F6. The Output shows the numbers, and the window then shows the
measured black bin. `Scenes/projected_light_test.tscn` is the earlier test of the light itself; it only writes to the
Output and closes.

## Results

Measured with a software renderer on the fixed reference fill, one seed. "Exposure" is `ElectronsPerLightUnit`.

Flat plate, deviation from the known depth. The average error rounds to 0.0 mm everywhere, so the geometry is right:

| Distance | Exposure | White: spread | Black: spread |
|---|---:|---:|---:|
| 1.5 m | 4,000 | 0.8 mm | 3.0 mm |
| 1.5 m | 16,000 | overexposed | 1.5 mm |
| 1.5 m | 64,000 | overexposed | 0.9 mm |
| 2.5 m | 4,000 | 2.8 mm | 16.4 mm |
| 2.5 m | 16,000 | 69% holes, 2.0 mm | 6.7 mm |
| 2.5 m | 64,000 | overexposed | 3.5 mm |

Full bin, on the pixels that show a link:

| Links | Exposure | Holes | Median error | Over 5 mm | Pick points passed | Best candidates that seal |
|---|---:|---:|---:|---:|---:|---:|
| Black | 4,000 | 7% | 2.5 mm | 22% | 2 | 1 of 1 |
| Black | 16,000 | 5% | 1.2 mm | 3% | 230 | 16 of 16 |
| Black | 64,000 | 9% | 0.8 mm | 0% | 464 | 17 of 20 |
| White | 4,000 | 5% | 0.8 mm | 0% | 623 | 18 of 20 |
| White | 16,000 | 84% | 0.7 mm | 1% | 0 | none |
| White | 64,000 | 98% | 0.9 mm | 2% | 0 | none |

On a laptop with an RTX 4070, one image of the fill in the first light test took about 50 ms for 3 frames. Eleven images
would then take well under a second, but the full measurement has not been timed there.

### Picks on this measurement

Twenty picks on the reference fill with black links, exposure 16,000, measuring again after every pick. The formula
column is the earlier series with the same code for picking.

| | Projected light | Formula |
|---|---:|---:|
| Placed | 14 of 20 | 12 of 20 |
| Lost while lifting | 6 | 7 |
| No seal | 0 | 1 |
| Links on the measured surfaces, without measurement | 5% each time | 58% |
| Average depth deviation per measurement | 2.3 to 6.9 mm | 0.5 mm |
| Moved before lifting (artefact), on average | 3.1 | 2.8 |

Every chosen point sealed, and in each measurement nearly all best candidates sealed when checked against perfect depth.
The losses have the same cause as before, contact with neighbours while lifting; see
[picking.md](picking.md#why-links-come-off-during-lifting). Twenty picks on one fill is too few to call 14 better than 12.

## Exposure and HDR

Key **E** steps through four settings and measures again. The numbers are `ElectronsPerLightUnit`, own choices and not
settings of a real camera.

| Setting | Exposure | Images |
|---|---|---:|
| Short | 4,000 | 11 |
| Medium (start) | 16,000 | 11 |
| Long | 64,000 | 11 |
| HDR short + long | 4,000 and 64,000 | 22 |

HDR (`HdrCombiner`) decodes each exposure and takes per pixel the longest exposure that gave a valid depth. The light is
rendered once and each exposure gets its own noise, because the scene does not change between them. A real camera
takes all 22 images.

Reference fill, one measurement each, links only:

| Setting | Black: holes, deviation | Black: pick points | White: holes, deviation | White: pick points |
|---|---|---:|---|---:|
| Short | 7%, 5.7 mm | 3 | 5%, 1.4 mm | 608 |
| Medium | 5%, 2.6 mm | 269 | 84% (79% overexposed) | 0 |
| Long | 9% (4% overexposed), 1.3 mm | 467 | 98% (94% overexposed) | 0 |
| HDR | 5%, 1.5 mm | 451 | 5%, 1.1 mm | 616 |

Only HDR works well for both colours. It costs twice as many images, so in a real cell twice the capture time. In
every working setting, 17 or 18 of the best candidates sealed when checked against perfect depth. The wooden rim is
overexposed at medium and long exposure, which is visible in orange.

## Camera images

Key **K** shows the images that the decoder received, one by one, with the name of the pattern: first the four shifted
sines, then the seven Gray code stripes from coarse to fine. A further press returns to the normal view. The brightness
is stretched so the brightest 1% is white, otherwise black links would be hardly visible; overexposed pixels are
orange. With HDR, the images of the long exposure are shown.

## What this shows

- **Black mainly gives noise here, not holes.** The holes are 5 to 7%, and appear mostly where the projector does not
  reach. The old formula gave 58%, but that number was set by hand. With too little exposure, black fails because the
  surface no longer looks flat: only 2 pick points pass.
- **Exposure is a real trade-off.** Black needs about four times more light than white can take. A setting that suits
  one overexposes the other, which is why real cameras combine several exposures (HDR).

## What is not in it

- **Holes are probably too few.** `MinModulation` is low and is a first choice. Nothing filters by confidence, and a real
  camera does. Light that reflects between links, which disturbs real measurements in a bin, is not simulated either.
- **No lens blur or distortion**, and the camera and projector are perfectly calibrated.
- **Too little light can give wrong depth instead of a hole.** The decoder only rejects a pixel below a fixed
  amplitude and when stripes and sine disagree. With weak signal the period can still be chosen wrongly, which gives an
  error of many centimetres; a test with a dark plate and a short exposure averaged 463 mm. A threshold that depends on
  the noise would catch this, and is not built.
- **Holes in shadow count as low signal**, because the images alone cannot tell the two apart.
- **One fill, one exposure, one seed.** White links and other exposures were not run as a pick series.
