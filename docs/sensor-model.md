# Sensor model

Godot delivers perfect depth. A real camera does not, especially on black parts. The sensor model turns perfect
depth into roughly what a structured-light camera would measure: a projector casts a pattern on the bin and a
camera next to it measures that pattern.

**The default values are illustrative and were not measured on a real camera.** The model shows the relative
influence of material, angle and shadow. It does not predict what a specific camera measures.

## Structure

```text
Godot: DepthSensorRig                 BinPicking.Core: DepthSensorModel
  depth from the camera     --+
  depth from the projector  --+-->  holes and noise  -->  measured depth + reason per hole
  reflectance per pixel     --+
```

## What the model does

| Effect | Calculation | Why |
|---|---|---|
| Low light | Signal = reflectance x cos(angle) x (reference distance / distance)². A low signal gives a chance of a hole | Black and oblique surfaces return little of the pattern |
| Shadow | If the projector sees something closer at the same spot, no pattern lands there | Camera and projector stand side by side |
| Edges | The measurement often fails at a depth jump | The pattern breaks at an edge |
| Noise | Grows with distance and with a weaker signal | A weaker pattern is less certain |

The colour in the image shows the reason: **magenta** is low light, **blue** is shadow and **yellow** is an edge.
The settings are in `DepthSensorSettings.cs`.

## Viewing

Open `Scenes/pallet_bin.tscn` and press F6. Key **4** switches between 3D, perfect depth and sensor. Key **B**
switches between black and light links. The sensor hangs 1.5 m above the rim, with a 1280 x 960 image.

## Result on the full reference fill

Rendered with a software renderer on the Linux cloud computer:

| Links | Without measurement | Low light | Shadow | Edge | Noise |
|---|---:|---:|---:|---:|---:|
| Black | 67% | 54% | 7% | 5% | 0.6 mm |
| Light | 14% | 1% | 7% | 5% | 0.4 mm |

Wood and background: 3% without measurement. Because of rounding, the percentages do not always add up exactly.
Flat surfaces facing the camera keep the most measurements, and those are exactly the surfaces a suction cup
fits on.
