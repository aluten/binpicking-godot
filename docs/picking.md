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

Ten picks in a row from the full reference fill, on the Linux cloud computer with a software renderer:

| Measurement | Result |
|---|---|
| Placed | 7 of 10 |
| No seal | 0 |
| Lost during lift | 3 |
| Picks with links pulled along (rise above 20 mm) | 3 |
| Links disturbed per pick (moved above 5 mm) | 10.5 on average, from 0 to 32 |

**This is no reliable success rate.** It is one fill and ten picks, and other versions of the same model gave 6 and 8 of
10 before they were corrected.

## Known limitations

- **Physics artefacts.** Restored links overlap slightly and jump when released. This is why they start damped, but it
  did not fully solve it: losses often come with many disturbed links. Part of the losses and disturbance can therefore
  be caused by the simulation and not by real hooking.
- **The tool does not push during descent**, because the links are still frozen then. Contact is therefore not simulated
  until the part is lifted.
- **The tool moves in straight lines**, without a robot, joint limits or acceleration.
- **The simulation knows which link is under the cup.** A real system only finds out from whether the vacuum holds.
- **Collision shapes are simplified**, up to 3 mm from the full mesh, which affects hooking.
- **Whether the middle requirement prevents tipping is still unproven.** All tested picks were within 5 mm of the middle,
  so this run has no comparison.
