# Requirements

This is everything needed to build and run the project on a Windows computer.

## Software

| Component | Version | Purpose | When needed |
|---|---|---|---|
| Godot, .NET edition | 4.7.2 | Scene, rendering and physics | Always |
| .NET SDK | 8.0.x | Compiles the C# code | Always |
| Git | Recent | Version control | Always |
| Git LFS | Recent | Installed, but not used yet | Only for large files in Git |
| Python | 3.12 | Runs the CAD scripts | When creating the link files |
| Code editor | Visual Studio, Rider or VS Code | Editing and debugging C# | Recommended |

Not needed: SolidWorks, Blender, a depth camera, a robot or CUDA. Forward+ does require a graphics card that
supports Vulkan or Direct3D 12.

Godot 4.7.2 creates a project with `net8.0`. That is why `global.json` in the root pins SDK 8.

### Install and verify

```powershell
winget install Microsoft.DotNet.SDK.8
winget install Python.Python.3.12
dotnet --list-sdks
git --version
py -0p
```

Download Godot from [godotengine.org](https://godotengine.org/) and choose the **.NET** version. Keep the whole
folder, because `GodotSharp` must stay next to the `.exe`.

## Python packages

`tools/cad_pipeline/requirements.txt` pins the tested versions.

| Package | What it does in this project |
|---|---|
| `trimesh` | Loads, measures, edits and exports meshes |
| `cascadio` | Converts STEP to triangles with the OpenCASCADE CAD kernel |
| `numpy` | Calculates with coordinates |
| `scipy` | Measures the largest circle on a face and the deviation between models |
| `pillow` | Draws the face as a raster for that circle measurement |
| `fast_simplification` | Creates the light version of the mesh |
| `coacd` | Splits the link into convex parts for the collision shape |

## Known pitfalls

| Message or problem | Cause | Solution |
|---|---|---|
| `Could not load file or assembly 'System.Runtime, Version=11.0.0.0'` | Godot uses a newer .NET SDK that it cannot handle | Keep `global.json` and restart Godot |
| No hammer button in Godot | The C# project was only just created | Restart Godot |
| `Python is niet gevonden` | The Microsoft Store shortcut gets in the way | Use `py -3.12`, or disable the aliases in the Windows settings |
| No lines appear in the Output | The script is not attached to the node | Drag the script onto the node |
| Segmentation fault when loading the STEP file | `coacd` was imported before reading it | Import `coacd` after loading, as `convert_link.py` does |
| Links sink centimetres into the floor | The Jolt defaults are too loose | See [testing.md](testing.md#physics-settings) |
