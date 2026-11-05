"""Meet hoe goed de vereenvoudigde mesh en de botsingsvorm op de volledige schakel passen.

Gebruik:
    python tools/cad_pipeline/check_link.py godot/generated
"""

import argparse
from pathlib import Path

import numpy as np
import trimesh
from scipy.spatial import cKDTree


def load(path):
    return trimesh.load(path, force="scene")


def deviation_mm(reference, other):
    """Afstand van punten op de schakel tot het dichtstbijzijnde punt op het andere model."""
    points, _ = trimesh.sample.sample_surface(reference, 40000, seed=2)
    targets, _ = trimesh.sample.sample_surface(other, 200000, seed=3)
    distances = cKDTree(targets).query(points)[0] * 1000
    return np.round(np.percentile(distances, [50, 95, 100]), 2)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("generated_dir", type=Path)
    args = parser.parse_args()

    visual = load(args.generated_dir / "schakel_visual.glb").to_geometry()
    lod = load(args.generated_dir / "schakel_lod.glb").to_geometry()
    collision = load(args.generated_dir / "schakel_collision.glb")

    print(f"Visual: {np.round(visual.extents * 1000, 1)} mm, {len(visual.faces)} driehoeken")
    print(f"LOD   : {np.round(lod.extents * 1000, 1)} mm, {len(lod.faces)} driehoeken")
    print(f"Hulls : {len(collision.geometry)}, allemaal convex: "
          f"{all(mesh.is_convex for mesh in collision.geometry.values())}")
    print(f"Afwijking LOD mediaan/95%/max: {deviation_mm(visual, lod)} mm")
    print(f"Afwijking botsing mediaan/95%/max: {deviation_mm(visual, collision.to_geometry())} mm")


if __name__ == "__main__":
    main()
