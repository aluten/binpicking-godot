# Choosing a pick point

The origin of each requirement, and what is missing compared to literature, is in [theory.md](theory.md).

The goal is not to recognise all links, but to find one place where the suction cup can seal. The search uses
only measured depth and has no Godot dependency.

| Class | Responsibility |
|---|---|
| `GraspEvaluator` | Judges one location and names the first requirement it fails |
| `GraspPlanner` | Scans every 6th pixel, counts the outcomes and ranks the candidates |
| `SealVerifier` | Compares a candidate with perfect depth, in simulation only |

## Requirements

| Step | Question | Default requirement |
|---|---|---|
| 1. Bin filter | Is the point inside the bin? | 1 cm from the wall, at least 3 cm above the floor |
| 2. Measurements | Are there enough points under the cup? | At least 15% of the cup area |
| 3. Flatness | Does a plane fit the points? | 90% within 3 mm |
| 4. Angle | Can the cup approach it? | At most 35 degrees tilted |
| 5. Middle | Is the cup near the middle of the flat surface? | Within 20 mm of the centroid of the points in the same plane, within 70 mm |
| 6. Holder | Does the holder hit something higher? | Within Ø 50 mm, nothing more than 5 mm above the plane |
| 7. Opening | Does the holder fit through the bin opening? | Along the normal, inside the walls |

The cup is Ø 30 mm. The requirements are in `GraspSettings.cs` and are a first choice, not tuned to a real cup.

Score = measured share x share in the plane x cos(angle) x (0.5 + 0.5 x height in the bin) x (0.5 + 0.5 x centredness). The best candidates
remain, at least one cup width apart.

## Viewing

Open `Scenes/pallet_bin.tscn` and press F6. At first no pick points are visible, so you can explain the bin and
the measurement first.

| Key P | On screen |
|---|---|
| First press | The best candidates in **blue** |
| Second press | The choice in **green** with its approach direction |
| Third press | Hidden again |

In the sensor view (key 4) they appear as circles. The Output shows how many locations drop out at each step.
With key R you can make a lower fill and compare.

## Result

On the Linux cloud computer, with fixed fills and one seed:

| | Full, black | Full, light | 96 links, black |
|---|---:|---:|---:|
| Evaluated | 5,940 | 15,385 | 262 |
| Too few measurements | 334 | 0 | 238 |
| Not flat | 2,434 | 10,348 | 17 |
| Too tilted | 1,123 | 2,963 | 1 |
| Not near the middle | 993 | 852 | 3 |
| Holder hits | 479 | 491 | 0 |
| Outside the opening | 66 | 135 | 0 |
| Accepted | 511 | 596 | 3 |
| Best candidates with at least 90% seal | 18 of 20 | 18 of 20 | 3 of 3 |

The middle requirement halves the accepted places in a full bin and costs some verified seals (it was 19 of 20 and
20 of 20). That verification only measures flatness, so it cannot show whether the requirement prevents tipping. Only
the real pick can test that.

**On black it works well in a full bin, but few places remain low in the bin.** Further away less light returns,
so most places have too few measurements. The margin is small there.

The requirements were adjusted for this. With 25% measurements and 95% within 2 mm, the planner found nothing on
this low fill, while 463 places passed on perfect depth. Measurements are noisy, so the search now allows 3 mm;
the verification stays at 2 mm.

## What the verification does and does not say

The true seal compares the chosen plane with perfect depth: what share of the cup lies within 2 mm? This is only
possible in simulation, and it has limits:

- It covers few fills and one seed, so it is not a reliable success rate.
- The fill uses the simplified mesh, not the full link.
- Two links at the same height can count as one surface.
- Holes in the measurement are treated as free space in the holder check.
- The middle of a surface is an approximation: there are no part labels, and it is not the centre of mass.
- Nothing has been picked yet: vacuum, weight and hooked links only come into play with the pick.
