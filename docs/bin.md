# The bin: a pallet with collars

The bin is a euro pallet with 5 wooden pallet collars, such as
[Fabory 8W372370](https://www.fabory.com/en_FR/pallet-stacking-frame-l-1200-x-w-800-mm-4-hinges-useful-height-200-mm-pine/p/8W372370).
All measurements are in `PalletDimensions`.

| Measurement | Value | Source |
|---|---|---|
| Outer size per collar | 1200 x 800 mm | Fabory |
| Height per collar | 200 mm | Fabory |
| Number of collars | 5, so 1000 mm high | Choice |
| Plank thickness | 20 mm | Assumption |
| Inner size | 1160 x 760 mm | Calculated from the assumption |
| Links in a full bin | 720 | Choice |

## Why not 3000 links

On one pallet, 3000 links would stand more than 4 m high. The camera only sees the top layers anyway, so little
changes for the vision problem. The number is `FullBinLinks` in `BinFiller`.

## Controls

| Input | Action |
|---|---|
| Drag with the left or right mouse button | Rotate |
| Drag with the middle mouse button | Pan |
| Mouse wheel | Zoom |
| 1 / 2 / 3 | Overview / from the sensor / close-up |
| 4 | 3D / perfect depth / sensor |
| B | Black or light links |
| R | Empty and fill the bin; press R again to stop at that height |
| P | Show candidates; again: show the choice; again: hide |

R deletes the stored fill immediately.

## How the bin is filled

Physics on hundreds of links at once is too heavy, so `BinFiller` works in steps. `BinFillStore` then saves the
result in `godot/generated/pallet_vulling.json`. This file is not in Git; without it, the scene fills the bin.

1. Drop 24 links, just above the highest link.
2. Wait until they lie still, at most 4 s.
3. Freeze links that are still and more than 12 cm below the highest link.
4. If such a link is stuck in a wall or the floor, drop it again.
5. Repeat until all 720 links are placed or R is pressed. The last links land first, and the transforms are then
   saved, also for a partial fill.

A stored fill is drawn by `BinFillView` with a `MultiMesh`: one mesh drawn 720 times, without physics.

## Lessons learned

| Problem | Cause | Solution |
|---|---|---|
| Links stuck up to 24 cm in the wall | Thin collision walls; past the middle a link is pushed outward | Collision walls and floor 2 m thick; only the visible planks are thin |
| Some links stayed 5 cm in the wall | They were trapped between frozen links | Drop them again |
| More `position_steps` | No demonstrable help; the physics test got worse | Keep the default |

## Limitations

- The planks and corners are simple boxes; the exact shape was not measured.
- The mass of 0.15 kg per link is an assumption.
- A stored fill has no physics yet. That comes with the pick.
- The fill height varied between 774 and 1004 mm across all trial runs with different settings.
