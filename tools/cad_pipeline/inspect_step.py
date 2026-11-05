"""Toont de opbouw van een STEP-bestand, zonder iets te wijzigen of te exporteren.

Gebruik:
    python tools/cad_pipeline/inspect_step.py cad/r4.32.stp
"""

import argparse
from collections import defaultdict
from pathlib import Path

import numpy as np
import trimesh


def collect_parts(scene):
    """Geeft alle onderdelen terug in wereldcoordinaten, in de eenheid van de scene."""
    parts = []
    for node in scene.graph.nodes_geometry:
        transform, geometry_name = scene.graph[node]
        mesh = scene.geometry[geometry_name].copy()
        mesh.apply_transform(transform)
        parts.append((str(node), mesh))
    return parts


def oriented_extents(mesh):
    """Afmetingen van de kleinste gedraaide doos, gesorteerd van groot naar klein."""
    try:
        return np.sort(mesh.bounding_box_oriented.primitive.extents)[::-1]
    except Exception:
        return np.sort(mesh.extents)[::-1]


def fmt(values):
    return " x ".join(f"{value:.4g}" for value in values)


def report(scene, max_rows=40):
    parts = collect_parts(scene)
    if not parts:
        print("Geen meshes gevonden in dit bestand.")
        return

    print(f"Aantal onderdelen : {len(parts)}")
    print(f"Unieke geometrieen: {len(scene.geometry)}")
    print(f"Driehoeken totaal : {sum(len(mesh.faces) for _, mesh in parts)}")
    print(f"Totale omvang     : {fmt(scene.extents)} (eenheid van de scene)")
    print(f"Eenheid volgens bestand: {scene.units}")

    # Gelijke onderdelen herken je aan driehoeken en afmetingen, ook als ze gedraaid staan.
    groups = defaultdict(list)
    for name, mesh in parts:
        size = oriented_extents(mesh)
        key = (len(mesh.faces), tuple(np.round(size / max(size[0], 1e-9), 2)))
        groups[key].append((name, mesh, size))

    print(f"\nGroepen van vergelijkbare onderdelen: {len(groups)}")
    ordered = sorted(groups.items(), key=lambda item: -len(item[1]))
    for index, (key, members) in enumerate(ordered, start=1):
        name, mesh, size = members[0]
        print(f"  Groep {index}: {len(members)} stuks, {key[0]} driehoeken, "
              f"afmeting {fmt(size)}, watertight={mesh.is_watertight}, "
              f"losse delen={mesh.body_count}, voorbeeld '{name}'")

    print(f"\nEerste {min(max_rows, len(parts))} onderdelen (midden en omvang in de scene):")
    for name, mesh in parts[:max_rows]:
        print(f"  {name}: midden {fmt(mesh.bounds.mean(axis=0))}; "
              f"omvang {fmt(mesh.extents)}; driehoeken {len(mesh.faces)}")
    if len(parts) > max_rows:
        print(f"  ... en nog {len(parts) - max_rows} onderdelen")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("step_file", type=Path)
    args = parser.parse_args()

    if not args.step_file.is_file():
        raise SystemExit(f"Bestand niet gevonden: {args.step_file}")

    loaded = trimesh.load(args.step_file)
    scene = loaded if isinstance(loaded, trimesh.Scene) else trimesh.Scene(loaded)
    report(scene)


if __name__ == "__main__":
    main()
