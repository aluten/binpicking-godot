# Bin picking in Godot

A working, explainable simulation of bin picking with a suction cup: pick one black part from a pallet full
of identical black parts. The project uses an igus cable chain link as a public example and currently runs
entirely in simulation.

> Status: the CAD step, a pallet with 720 links, a depth camera with a sensor model, choosing a pick point and
> a simulated pick all work. The vacuum and mass values are assumptions.

## Quick start

1. Install the software from [docs/requirements.md](docs/requirements.md).
2. Download the igus model. These files are deliberately not in this repository:
   1. Open the [igus CAD page for R4.32.10.125.0](https://www.igus-cad.com/default.aspx?cul=en-GB&ArtNr=R4.32.10.125.0&mandant=INT).
   2. Under **Chain length**, choose **Quantity chain links** and enter **9**.
   3. Choose **STEP** as the format and click the download button.
   4. Download the file through the **Download center**.
   5. Save it as `cad/r4.32.stp`.
3. Create the Godot files, from the repository root:
   ```powershell
   py -3.12 -m venv .venv
   .venv\Scripts\activate
   pip install -r tools\cad_pipeline\requirements.txt
   python tools\cad_pipeline\convert_link.py cad\r4.32.stp godot\generated
   ```
4. Open `godot/project.godot` in Godot 4.7.2 (.NET edition) and click the hammer to build.
5. Open `Scenes/link_physics_test.tscn` and press F6. The Output must end with `RESULTAAT: GESLAAGD`.
6. Open `Scenes/pallet_bin.tscn` and press F6 to see the pallet. Without a stored fill, you first watch the bin
   fill up. The controls are shown at the top left.

## Documentation

| Document | Purpose |
|---|---|
| [docs/architecture.md](docs/architecture.md) | How the code is organised and where things belong |
| [docs/requirements.md](docs/requirements.md) | What to install, why, and known pitfalls |
| [docs/cad-pipeline.md](docs/cad-pipeline.md) | How a STEP file becomes a usable link |
| [docs/bin.md](docs/bin.md) | How the pallet is built and filled, and the controls |
| [docs/sensor-model.md](docs/sensor-model.md) | How the virtual camera measures black parts |
| [docs/pick-point.md](docs/pick-point.md) | How a pick point is chosen and verified |
| [docs/picking.md](docs/picking.md) | How a pick is performed, its models and its limits |
| [docs/theory.md](docs/theory.md) | The theory behind the overlay and pick strategy, with sources |
| [docs/testing.md](docs/testing.md) | Which tests exist, how to read them and what they showed |
| [docs/handover.md](docs/handover.md) | Status, decisions, open points and next steps |

## About the model

igus is not involved in this project. Download the CAD model from igus yourself; the igus terms apply. Do not
add the model or the files generated from it to this repository.
