# Theory and sources

This page explains the overlay and the pick strategy, and separates what comes from literature from what was chosen
for this project. **The pick strategy is a geometric heuristic built for this project. It is not one published
method, and its thresholds were not tested on a real suction cup or camera.**

## Depth measurement and the overlay

A structured-light camera projects a pattern and derives depth from how the pattern deforms. A point is measured only
if both camera and projector can see it and enough of the pattern returns
([Pickit](https://docs.pickit3d.com/en/3.5/faq/hardware/how-does-the-camera-work.html)).

| Colour | Meaning | Idea |
|---|---|---|
| Magenta | Low light | Black and oblique surfaces return less, and distance weakens the signal |
| Blue | Projector shadow | The camera sees the spot, the projector does not |
| Yellow | Depth jump | The pattern breaks at an edge |

The causes are real. The formula (reflectance x cos(angle) / distance²), the noise model and all percentages are a
simplification made for this project. They say nothing about a specific camera. The Pickit page names reflective
and transparent parts as limits; it does not discuss black parts.

On top of that image, a blue circle is a candidate and a green circle the choice. Each circle is the Ø 30 mm cup.

## Pick strategy

A suction cup only holds if its rim makes contact all the way round. `GraspEvaluator` therefore asks these questions:

| Requirement | Why | Origin |
|---|---|---|
| Enough measurements | Black parts leave holes | Own choice |
| Flat under the cup | Stand-in for a seal | Plane-fit residual also used by Jiang et al.; Dex-Net 3.0 checks the contact ring instead |
| Not too tilted | The cup must be able to approach | Own choice |
| Near the middle of the surface | Limits tipping | Jiang et al. prefer central points; the threshold and the calculation are own choices |
| Room for the holder | The holder must not hit anything | Simple stand-in for collision checks in Jiang et al. |
| Through the bin opening | The holder must get in | Own choice, without robot kinematics |
| Score includes height | Top parts are less buried | Own choice; not found in these sources |

## What is missing compared to the sources

- **Forces.** [Dex-Net 3.0](https://ar5iv.labs.arxiv.org/html/1709.06670) also evaluates whether the seal can resist the
  forces on the part. This project does not. The distance to the middle of a surface is only a rough stand-in.
- **Contact model.** Dex-Net 3.0 models the cup as a spring system and checks whether the contact ring lies on the
  surface. This project requires the whole circle to be flat, which is simpler and stricter.
- **Robot reachability.** [Jiang et al.](https://ar5iv.labs.arxiv.org/html/2111.02571) evaluate inverse kinematics and
  collisions of the robot.
- **Learning.** Both sources train a neural network. This project deliberately uses no network yet, so every step
  stays explainable.

Dex-Net 3.0 uses a comparable idea, `Planarity-Centroid`, as a baseline and reports that it performs poorly on objects
with non-planar surfaces near the centroid. This project is therefore an explainable starting point, not the best
known method.

## Reading order

| Source | Why |
|---|---|
| [Pickit: How does the camera work?](https://docs.pickit3d.com/en/3.5/faq/hardware/how-does-the-camera-work.html) | Short and practical; start here |
| [Jiang et al.: Learning suction graspability considering grasp quality and robot reachability for bin-picking](https://ar5iv.labs.arxiv.org/html/2111.02571) | Closest to this code |
| [Mahler et al.: Dex-Net 3.0](https://ar5iv.labs.arxiv.org/html/1709.06670) | Theory of seal and forces; more demanding |

The articles are linked and not copied, to avoid copyright problems in a public repository.
