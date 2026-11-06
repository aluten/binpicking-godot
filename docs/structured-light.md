# Experiment: measuring with projected light

**Status:** experiment on the branch `experiment/structured-light`. The demo still uses the formula in
[sensor-model.md](sensor-model.md). The sensor values below are illustrative, and this does not predict what a real
camera measures.

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

Open `Scenes/structured_light_test.tscn` and press F6. The Output shows the numbers, and the window then shows the
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
- **Holes in shadow count as low signal**, because the images alone cannot tell the two apart.
- **The picks were not run on this measurement.** Only the search was. Connecting it to the demo is the next step.
