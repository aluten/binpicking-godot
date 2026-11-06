# Handover

## Goal

Learn, explain and demonstrate bin picking in a public repository, without the confidential model from the real
project. Every intermediate step must be visible and explainable.

The core of the problem is that a black part yields few good depth measurements and that all parts look alike. The
pipeline does not need to recognise all parts, only to find one easily reachable part per cycle:

```text
depth -> point cloud -> bin filter -> flat candidates -> sealing score
      -> collision check -> pick -> check after lifting -> place
```

## Status

| Phase | Item | Status |
|---|---|---|
| 0 | Part chosen, repository and CAD reference | Done |
| 1 | CAD to Godot, links that fall stably | Done |
| 2 | Pallet with 720 links | Works and is stored; no physics after loading yet |
| 3 | Depth camera and sensor model | Works; values are illustrative |
| 4 | First pick with suction cup | Works in simulation; see [picking.md](picking.md) |
| 5 | Failure cases and statistics | Outcomes and series work; artefacts are reduced but not gone |
| 6 | Pose estimation with the CAD model | Not started |
| 7 | Robot and publication | Not started |

## Decisions

| Decision | Reason |
|---|---|
| Simulation only for now | No hardware needed; a real camera can later sit behind an interface |
| Godot with C# | Good C# integration and a plugin structure |
| igus R4.32.10.125.0 | Publicly downloadable, black, standard closed, and links can hook together |
| Model not in Git | Permission to redistribute is not confirmed, so everyone downloads it themselves |
| A pipeline without AI first | Every step stays visible and explainable |
| 5 collars and 720 links, not 3000 | 3000 stands more than 4 m high on one pallet; see [bin.md](bin.md) |
| Scripts instead of manual export | Anyone can reproduce the same result |
| Pick strategy is an own heuristic | Explainable starting point; see [theory.md](theory.md) |
| Algorithms separate from Godot | Testable without the engine; see [architecture.md](architecture.md) |

## Open points

- **Publication:** the repository is private. Before making it public, agree with the employer who holds the
  rights, including to work by the intern.
- **Licence:** no licence has been chosen for the code yet.
- **Images:** check with igus whether videos and screenshots of the model are allowed.
- **Confidentiality:** do not take cycle times, gripper choices or camera settings from the real project.
- **Windows check:** `requirements.txt` has not been installed on Windows yet.
- **Git LFS:** not in use, because there are no large files in Git.
- **Forces:** the pick strategy does not model forces on the part yet; see [theory.md](theory.md).
- **Dutch text:** text on screen and in the Output panel is still in Dutch.

## Next step

The principle is good enough for a demonstration; see [demo-questions.md](demo-questions.md). The cause of links coming
off during lifting has been found: contact with neighbours, see [picking.md](picking.md#why-links-come-off-during-lifting).
Not needed for the demonstration, but useful afterwards: take that into account in the pick choice and the lift
direction, and run many picks over several fills. Only then does a success rate mean something. To connect a real camera,
start with the steps in [demo-questions.md](demo-questions.md#connecting-a-real-camera).
