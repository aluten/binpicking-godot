# Sensor model

Godot delivers perfect depth. A real camera does not, especially on black parts. The sensor model turns perfect
depth into roughly what a structured-light camera would measure: a projector casts a pattern on the bin and a
camera next to it measures that pattern.

**The default values are illustrative and were not measured on a real camera.** The model shows the relative
influence of material, angle and shadow. It does not predict what a specific camera measures.

## Camera setup

The sensor is modelled as **one camera and one projector**, which is structured light. It is not stereo, and it does
not copy an existing product. All values below are choices for this demo, not the specifications of a real camera.

```text
      camera      projector
        [C]---15 cm---[P]        both looking straight down
         |             |
         |   1.5 m     |
         v             v
    ======= rim of the bin =======
     |     links, up to 720      |
    ==== floor, 1 m below the rim ====
```

| Property | Value | Set in |
|---|---|---|
| Camera position | Above the middle of the pallet, 1.5 m above the rim, looking straight down | `SensorMount` |
| Distance to the floor | 2.5 m | `SensorMount`, `PalletDimensions` |
| Projector position | 15 cm from the camera, along the pallet length | `DepthSensorSettings.ProjectorBaseline` |
| Image | 1280 x 960 pixels | `DepthSensorRig` |
| Field of view | About 45 degrees horizontal and 35 degrees vertical | `SensorMount` |

In the overview (key 1), `SensorRigModel` shows the named bodies. The blue and orange lines show the area that the
camera and the projector cover at rim height. The sensor itself does not see this model.

### What is actually simulated

No pattern is projected and depth is not triangulated. Godot renders three images, and the sensor model then adds
holes and noise:

| Image | Used for |
|---|---|
| Depth from the camera | The perfect depth |
| Depth from the projector position | Where projector shadow falls |
| Colour without lighting | How much light a surface returns |

The "projector" is therefore a second virtual camera that only serves to find shadow. The holes resemble those of
structured light, but they come from a simplified formula, not from simulating light.

### Compared with active stereo

| | Structured light (this model) | Active stereo |
|---|---|---|
| Hardware | Camera + projector | Two cameras, often with a projector |
| Depth from | Deformation of the pattern | Difference between two camera images |
| Holes | Where camera or projector cannot reach | Where one of the two cameras cannot reach |

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
| Black | 57% | 43% | 8% | 5% | 0.5 mm |
| Light | 15% | 1% | 8% | 5% | 0.3 mm |

Wood and background: 4% without measurement. These depend on the fill: a higher pile is closer to the camera and returns more light. Because of rounding, the percentages do not always add up exactly.
Flat surfaces facing the camera keep the most measurements, and those are exactly the surfaces a suction cup
fits on.
