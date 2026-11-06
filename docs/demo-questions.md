# Questions during a demonstration

Short answers to what people usually ask, based on what the code really does. Where the demo stops and a real system
starts is stated each time. Zivid is used as the example of a real camera; this project has not been tested with one.

## In one minute

- The demo has **no part recognition and no AI**. It looks for a place where a suction cup can seal.
- The CAD model is used for the simulation only: to fill the bin and to judge the contact. The search never sees it.
- The camera is **simulated without light**. It renders perfect depth and a formula then removes measurements.
- All thresholds are first choices for this demo. They were not tuned on real parts.

## Recognising the surface

**How does it recognise a surface?** It does not recognise a link, it tests locations. Every 6th pixel inside the bin
gets the same question: if the cup were here, would it have something to seal on?

1. Convert the depth pixels under the cup (Ø 30 mm) into 3D points.
2. Fit a plane through them with least squares, three rounds, dropping outliers each round. See `PlaneFitter`.
3. Count how many measured points lie within 3 mm of that plane.

**What if half the measurements are missing?** That is allowed. At least 15% of the cup area must be measured, and 90%
of those points must lie in the plane. Black parts leave many holes, so a stricter requirement finds almost nothing on
a black fill. The risk is a plane fitted on few points. That is why the seal is checked again at the cup.

**Can it tell two links apart?** No. Two touching links at the same height count as one surface in the search. The
contact ring catches this in simulation, because it tests one link at a time. A real system only finds out from
whether the vacuum builds up.

**How do you find the middle of a surface without knowing the part?** It takes the average of all nearby points in the
same plane (within 70 mm) and requires the cup to be within 20 mm of it. This approximates the middle of the visible
surface, not the centre of mass.

## Choosing the optimal pick

**Is it really the optimal pick?** No. It is the best under a rule of thumb. Nothing proves that the choice is optimal
or models the forces on the part. In the reference fill, 17 of the 20 best candidates had at least 90% true seal, and 12
of 20 picks were placed. Series this short give an indication, not a success rate.

**How does it choose?** First requirements, then a score.

| Step | Question | Requirement | Rejected (full, black) |
|---|---|---|---:|
| 1 | Enough measurements? | At least 15% of the cup area | 168 |
| 2 | Flat? | 90% within 3 mm | 4,096 |
| 3 | Not too tilted? | At most 35 degrees | 1,709 |
| 4 | Near the middle? | Within 20 mm | 1,083 |
| 5 | Room for the holder? | Within Ø 50 mm, nothing over 5 mm higher | 686 |
| 6 | Through the opening? | Holder stays inside the walls | 61 |

Of the 8,492 locations evaluated, 689 passed. These are then ranked:

```text
score = measured share x share in the plane x cos(tilt)
        x (0.5 + 0.5 x height in the bin) x (0.5 + 0.5 x centredness)
```

The best 20 remain, at least one cup width apart. The highest score is picked first. If a pick fails, that spot is not
retried. See [pick-point.md](pick-point.md); the origin of each requirement is in [theory.md](theory.md).

**Why these numbers?** They are first choices. The tolerance was widened from 2 to 3 mm after a stricter setting found
nothing on a low fill, while the verification stayed at 2 mm.

**Why do links sometimes come off?** Contact with neighbouring links while lifting, not a lack of vacuum force. With
that contact switched off, 15 of 15 picks were placed. The force stayed below half of the assumed limit. See
[picking.md](picking.md#why-links-come-off-during-lifting). The search does not look at this yet: it only checks that
nothing stands more than 5 mm above the plane within the holder width.

**Why no AI or CAD matching?** Each step stays visible and explainable, and one flat face is enough for a suction cup.
Literature goes further. Dex-Net 3.0 models the contact and forces, and Jiang et al. learn the choice. CAD matching
would also give the pose of the part, which matters when a part must be placed in a set orientation. That is not
built here.

## Taking the image and the lighting

**How is it lit in this demo?** It is not. No pattern is projected and nothing is triangulated. Godot renders depth from
the camera, depth from the projector position (for shadow) and an unlit colour image. `DepthSensorModel` then removes
pixels with a formula: reflectance x cos(angle) x (reference distance / distance)². The lights in the 3D view only
affect what you see on screen. See [sensor-model.md](sensor-model.md).

**How does a real structured-light camera do it? Several light patterns?** Yes, a series of them. A projector lights the
scene with one pattern after another, while the camera takes an image of each. For each pixel, the series gives a code
that says which projector column lit that point. Camera ray and projector column then intersect, which gives the depth.
No matching between two images is needed, and a surface without texture is no problem.

Two common pattern families ([Gerig, University of Utah](https://www.sci.utah.edu/~gerig/CS6320-S2013/Materials/CS6320-CV-S2013-StructuredLight-II.pdf)):

| Family | Idea | Number of images |
|---|---|---|
| Binary or Gray code | Black and white stripes, each image is one bit of the column number | log2 of the columns: 10 for 1024 columns |
| Phase shifting | A sine pattern, shifted a few times; precise within a period, but ambiguous about which period | Often combined with Gray code to remove the ambiguity |

**And Zivid specifically?** Zivid calls it [temporal structured light](https://support.zivid.com/support/faq.html): a
sequence of patterns. It does not publish, in the pages consulted, how many patterns or which types each engine uses.
A [Zivid blog post](https://blog.zivid.com/how-structured-light-works) uses seven binary images, but only as an
illustration in Blender. So do not quote a number for the product. What is documented:

| Term | Meaning |
|---|---|
| [Vision engine](https://support.zivid.com/en/latest/camera/reference-articles/settings/vision-engine.html) | Controls the patterns, imaging and processing: Phase (fastest), Stripe (balanced, for reflective parts), Omni and Sage (slowest). Availability differs per model |
| [Acquisition](https://support.zivid.com/en/latest/camera/reference-articles/settings/acquisition-settings.html) | One exposure setting: exposure time, aperture, projector brightness and gain. Zivid 3 has no adjustable aperture |
| [HDR](https://support.zivid.com/en/latest/camera/academy/camera/capturing-high-quality-point-clouds/getting-the-right-exposure-for-good-point-clouds.html) | Several acquisitions in one capture, for example one short for highlights and one long for dark areas |
| [Sampling](https://support.zivid.com/en/latest/camera/reference-articles/settings/sampling.html) | Which light colour is projected and which pixels are used. Subsampling is faster and gives fewer points, not less depth resolution |

**What about black parts?** They return little light, so they [need a high exposure](https://support.zivid.com/en/latest/camera/academy/camera/capturing-high-quality-point-clouds/getting-the-right-exposure-for-good-point-clouds.html).
That costs capture time. Repeating an acquisition raises the signal-to-noise ratio if there is time. Zivid's
[Manufacturing Diffuse preset](https://support.zivid.com/en/latest/camera/reference-articles/presets-settings.html) is
for scenes without reflections or high dynamic range and is described as giving great coverage on dark reflective
objects. Whether it suits these links must be tested on the parts; the preset page itself says to try presets in your
own application.

**Does the hall lighting matter?** The [projector is the only signal needed](https://support.zivid.com/en/latest/camera/academy/camera/capturing-high-quality-point-clouds/dealing-with-strong-ambient-light.html),
and ambient light acts as noise. Zivid reports degradation above roughly 1500 to 3000 lux, and direct sunlight is
generally not workable.

**Must everything stand still?** A series of images only fits together if the scene does not change during it, so as a
principle yes: the robot stays out of view and the bin stands still during a capture. The exact timing depends on the
settings; [Zivid mentions](https://support.zivid.com/support/faq.html) captures under 100 ms for some applications. This
demo has no capture time at all. Several cameras can
[disturb each other](https://support.zivid.com/support/faq.html) when their projections overlap, so they may need to
capture one after the other.

## Connecting a real camera

**What stays and what goes?** The search is already separate. `BinPicking.Core` knows no Godot and no camera.

```text
Now:   DepthSensorRig + DepthSensorModel -> SensorMeasurement -> depth (m, NaN) -> GraspPlanner -> PickCycle (physics)
Real:  Zivid SDK -> point cloud -> same data                  -> depth (m, NaN) -> GraspPlanner -> robot + vacuum
```

| Part | With a real camera |
|---|---|
| `GraspPlanner`, `GraspEvaluator`, `PlaneFitter`, `GraspSettings` | Stay; retune the thresholds on real data |
| `DepthSensorRig`, `DepthSensorModel`, `SensorMeasurement` | Replaced by the camera |
| `SealVerifier`, `IdealDepth` | Gone: there is no perfect depth |
| `SensorMount` | Replaced by a hand-eye calibration |
| `PickCycle`, `PickRegion`, `VacuumHold`, `CupContactProbe` | Replaced by robot control and a vacuum sensor |

**Why does this fit well?** The data is nearly the same. Zivid gives an
[organised point cloud](https://support.zivid.com/reference-articles/point-cloud-structure-and-output-formats.html): a
grid with one point per pixel, X, Y and Z in millimetres, NaN for points without information, plus colour and a
signal-to-noise value. The
[camera axes](https://support.zivid.com/reference-articles/camera-coordinate-system.html) are also the same as here: X to
the right towards the projector, Y down, Z away from the camera.

**What is the code like?** Zivid has an official .NET SDK ([Capture Tutorial](https://support.zivid.com/academy/camera/capture-tutorial.html),
[C# samples](https://github.com/zivid/zivid-csharp-samples)). A sketch from that documentation. **It has not been
compiled or run**, and details differ per SDK version:

```csharp
var zivid = new Zivid.NET.Application();              // Keep alive as long as the camera is used.
var camera = zivid.ConnectCamera();                   // Or zivid.CreateFileCamera("....zfc"), without hardware.
var settings = new Zivid.NET.Settings("Preset.yml");  // A preset exported from Zivid Studio.

using (var frame = camera.Capture3D(settings))
{
    float[,,] xyz = frame.PointCloud.CopyPointsXYZ(); // [height, width, 3], in millimetres.
    int height = xyz.GetLength(0), width = xyz.GetLength(1);
    var depth = new float[width * height];
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            depth[y * width + x] = xyz[y, x, 2] / 1000f;  // Millimetres to metres; NaN stays NaN.
        }
    }
    // Next: GraspPlanner.FindGrasps(camera model, depth, bin, settings).
}
```

**What is the catch?** Four things must change, and they are not done:

1. **Use the measured X and Y.** `GraspEvaluator` now calculates X and Y from depth with `PinholeCamera`, a model
   without lens distortion. Zivid [advises against intrinsics](https://support.zivid.com/reference-articles/camera-intrinsics.html)
   where possible: its camera model is more complex, and intrinsics are an approximation. So the search should read X, Y
   and Z from the point cloud. A quick first test can use `Calibrator.EstimateIntrinsics`, which is an experimental API.
2. **A camera that does not look straight down.** `BinRegion` is a box in camera space, and tilt is measured against the
   camera axis. That only holds for a camera straight above the bin. A tilted camera needs the points transformed first.
3. **Hand-eye calibration.** `SensorMount.ToWorldPoint` is now a simple formula. A real pick needs the
   [transform from camera to robot base](https://support.zivid.com/academy/applications/hand-eye/how-to-use-the-result-of-hand-eye-calibration.html),
   from a calibration, and the tool centre point on the robot side. Zivid offers
   [C# samples](https://support.zivid.com/camera/api-reference/samples/csharp.html) for it.
4. **Capture blocks.** A capture should run outside the Godot main thread, or the camera should first be connected to a
   plain .NET console program. That second option proves the chain without Godot.

**In what order would you do it?**

1. Use a file camera: `CreateFileCamera` works [without hardware](https://support.zivid.com/academy/camera/capture-tutorial.html).
2. Make captures of real parts in Zivid Studio, save them as ZDF, and run the search on them in a console program.
3. Put a shared interface in front of the depth source, so the simulation and the camera are interchangeable. This
   interface does not exist yet.
4. Compare: does the search find the same kind of places as in simulation? Retune the thresholds, and use real captures
   to set the values of `DepthSensorSettings` so the simulation resembles the camera.
5. Only then add calibration and a robot.

**Which camera model?** This has not been worked out. The demo hangs its sensor 1.5 m above a 1.2 x 0.8 m bin that is 1 m
deep, with a made-up 15 cm baseline. Check on the datasheet whether a model covers that distance and depth, or change
the mount.

**Can the simulation test camera software?** Partly: the search and the logic can be tested on simulated depth. The
sensor model is illustrative, so a good result in simulation says little about a real camera until its values come from
real captures.

## Sources

The Zivid links were read in November 2026 and can change per SDK version. The picking literature is listed in
[theory.md](theory.md).
