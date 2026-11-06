# The pick

After a pick point is chosen, the demo performs the pick in the physics engine. This shows what the geometry alone
cannot: whether the cup really seals, whether the part stays on the cup, and what happens to the parts around it.

## Controls

Open `Scenes/pallet_bin.tscn` and press F6. **G** performs one pick; **A** runs 10 in a row. Results appear on screen and
in the Output panel. Reload the scene to get the stored fill back: picks do not change the saved file.

## The cycle

| Step | What happens |
|---|---|
| 1. Region | Links near the pick point become physics bodies, all frozen; links further away are static support |
| 2. Descend | The tool moves along the surface normal to the pick point |
| 3. Check the seal | Rays test the contact ring against the full mesh of each nearby link |
| 4. Engage | The nearby links are released, heavily damped, while the vacuum holds the part for 0.5 s |
| 5. Lift | The part goes straight up to 30 cm above the rim |
| 6. Carry | The part goes to the table next to the pallet and is released |
| 7. Settle | The remaining links come to rest and the new positions are stored in memory |
| 8. Measure | The sensor captures the changed bin, ready for the next pick |

A failed pick point is not tried again in the same run, within 3 cm.

## Models

| Model | How it works | Status |
|---|---|---|
| Seal | At least 90% of 32 ring points touch one link, within 3 mm of the cup plane | Follows the contact-ring idea of Dex-Net 3.0, without its spring model |
| Holding force | Vacuum x area x efficiency = 40 kPa x 7.07 cm² x 0.5 = 14 N | Assumption, not a real cup |
| Holding torque | Force x cup diameter / 4 = 0.11 Nm, reduced by off-centre force | Rough own estimate |
| Lost part | More than 12 mm or 12 degrees from the cup, for 0.1 s | Own choice |
| Link mass | 0.15 kg | Assumption |

The vacuum is not a rigid attachment. `VacuumHold` applies limited force and torque, so a hooked part can come off.

## Outcomes

| Outcome | Meaning |
|---|---|
| `Placed` | Lifted and put on the table |
| `NoSeal` | The ring does not touch one link all the way round |
| `LostDuringLift` | The part came off the cup |
| `NoPartAtPoint` | No link touches the ring at all |

## Result

Series from the full reference fill, on the Linux cloud computer with a software renderer:

| Measurement | Before artefact work (10 picks) | Now (20 picks) |
|---|---:|---:|
| Placed | 7 of 10 | 12 of 20 |
| No seal | 0 | 1 |
| Lost during lift or carry | 3 | 7 |
| Lost before lifting began | Not measured | 0 |
| Links disturbed per pick, on average | 10.5 | 6.2 |
| Links moved before lifting began, on average | Not measured | 2.8 |
| Picks with links pulled along | 3 | 0 |

**This is no reliable success rate:** it is one fill and one seed. Both series are too small and use different fills, so
the difference between them means little.

`MovedBeforeLift` separates artefacts from effects of the pick: nothing has pulled on a link before lifting starts, so
any movement then comes from the simulation. It is smaller now, but not zero.

## Why links come off during lifting

Investigated with temporary instrumentation that is not in the code. Each run is 15 picks from one saved fill, so a
difference of two or three picks means little. Only the run without neighbour contact has a clear result.

| Run | Placed | Lost while lifting |
| --- | --- | --- |
| Baseline, wider loss limits (30 mm, 25 degrees) | 10 | 5 |
| Twice the torque limit | 10 | 5 |
| Four times the torque limit | 12 | 3 |
| First 80 mm along the surface normal, then up | 12 | 3 |
| No collision between the lifted link and its neighbours | 15 | 0 |

Findings:

- **Neighbour contact is necessary for the loss.** With that contact removed, none of the 15 picks was lost. This
  removes a physical effect, so it is a diagnostic, not a fix.
- **Pull force is not the limit.** The force stayed at 6.4 N or less against the 14 N limit and never saturated.
- **The neighbours are not carried along.** In the five baseline losses they had moved 2 to 13 mm, and all touching
  bodies were active links, never frozen links or walls. The lifted link scrapes past or rotates against them.
- **The link rotates before it is lost.** Losses come at 42 to 65 mm of rise, with rotation up to 24 degrees and the
  torque limit saturated for 7 to 14 steps.
- **A higher torque limit only partly helps.** At four times the limit the torque no longer saturates, yet three links
  still reach the 25 degree limit, because the rotation servo is soft. Neighbours were also pulled along in 3 picks,
  against 0 in the baseline.

Not established: whether a real cup would let go in these situations. The torque limit, the servo stiffness and the
loss limits are own estimates, and simplified collision shapes affect how links catch on each other. The wider moving
zone was not tested.

## Known limitations

- **Physics artefacts.** Restored links overlap slightly and can jump when released. A lower position correction, a new
  fill and damping reduce this, but 2.8 links per pick still move before lifting. See
  [testing.md](testing.md#physics-artefacts).
- **Losses are caused by contact with neighbouring links, not by vacuum force.** See
  [Why links come off during lifting](#why-links-come-off-during-lifting). The hold limits are rough estimates, so the
  share of losses is not a prediction for a real gripper.
- **The tool does not push during descent**, because the links are still frozen then. Contact is therefore not simulated
  until the part is lifted.
- **The tool moves in straight lines**, without a robot, joint limits or acceleration.
- **The simulation knows which link is under the cup.** A real system only finds out from whether the vacuum holds.
- **Collision shapes are simplified**, up to 3 mm from the full mesh, which affects hooking.
- **Whether the middle requirement prevents tipping is still unproven.** All tested picks were within 5 mm of the middle,
  so this run has no comparison.
