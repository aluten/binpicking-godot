"""Creates the Godot files for a single link from the igus STEP file.

Axes of the result, in metres:
    X = chain length, Y = top (suction surface), Z = width.

Usage:
    python tools/cad_pipeline/convert_link.py cad/r4.32.stp godot/generated
"""

import argparse
import json
from pathlib import Path

import numpy as np
import trimesh
from PIL import Image, ImageDraw
from scipy.ndimage import distance_transform_edt

LOD_FACES = 5000
MM_PER_PIXEL = 0.25
BLACK = trimesh.visual.material.PBRMaterial(
    baseColorFactor=[0.03, 0.03, 0.03, 1.0], metallicFactor=0.0, roughnessFactor=0.55
)


def pick_link(scene):
    """Picks the watertight link with the most triangles."""
    candidates = []
    for node in scene.graph.nodes_geometry:
        transform, name = scene.graph[node]
        mesh = scene.geometry[name].copy()
        mesh.apply_transform(transform)
        mesh.merge_vertices(merge_tex=True, merge_norm=True, digits_vertex=5)
        if mesh.is_watertight and mesh.is_winding_consistent:
            candidates.append((len(mesh.faces), str(node), mesh))
    if not candidates:
        raise SystemExit("Geen gesloten schakel gevonden in dit bestand.")
    faces, node, mesh = max(candidates, key=lambda item: item[0])
    print(f"Gekozen schakel: {node}, {faces} driehoeken, "
          f"{len(candidates)} gesloten kandidaten")
    return mesh


def straighten(mesh):
    """Straightens the link: largest side face = Z, largest face perpendicular to it = Y."""
    order = np.argsort(-mesh.facets_area)
    width = mesh.facets_normal[order[0]]
    top = next(mesh.facets_normal[i] for i in order
               if abs(np.dot(mesh.facets_normal[i], width)) < 0.01)
    length = np.cross(top, width)
    rotation = np.eye(4)
    rotation[:3, :3] = np.vstack([length, top, np.cross(length, top)])
    mesh.apply_transform(rotation)
    mesh.apply_translation(-mesh.bounds.mean(axis=0))
    return mesh


def measure_flat(mesh, sign):
    """Measures the largest circle on the largest face on top (+1) or bottom (-1)."""
    normals = mesh.facets_normal[:, 1] * sign
    indices = np.flatnonzero(normals > 0.999)
    best = indices[np.argmax(mesh.facets_area[indices])]
    triangles = mesh.triangles[mesh.facets[best]]
    plane_y = float(triangles[:, :, 1].mean())

    minimum = triangles[:, :, [0, 2]].reshape(-1, 2).min(axis=0)
    scale = 1000.0 / MM_PER_PIXEL
    pixels = (triangles[:, :, [0, 2]] - minimum) * scale + 2
    size = np.ceil(pixels.reshape(-1, 2).max(axis=0)).astype(int) + 3
    image = Image.new("1", tuple(int(v) for v in size), 0)
    draw = ImageDraw.Draw(image)
    for triangle in pixels:
        draw.polygon([tuple(point) for point in triangle], fill=1)

    distance = distance_transform_edt(np.array(image))
    row, column = np.unravel_index(np.argmax(distance), distance.shape)
    radius = float(distance[row, column]) / scale
    center_x = (column - 2) / scale + minimum[0]
    center_z = (row - 2) / scale + minimum[1]

    # Does anything protrude above the face, inside the circle and outside it?
    points, _ = trimesh.sample.sample_surface(mesh, 300000, seed=1)
    height = (points[:, 1] - plane_y) * sign
    inside = np.hypot(points[:, 0] - center_x, points[:, 2] - center_z) < radius

    return {
        "vlak_y_mm": round(plane_y * 1000, 2),
        "oppervlak_cm2": round(float(mesh.facets_area[best]) * 1e4, 1),
        "omvang_x_z_mm": [round(float(v) * 1000, 1) for v in
                          np.ptp(triangles[:, :, [0, 2]].reshape(-1, 2), axis=0)],
        "grootste_cirkel_diameter_mm": round(radius * 2000, 1),
        "cirkel_midden_x_z_mm": [round(center_x * 1000, 1), round(center_z * 1000, 1)],
        "uitsteek_binnen_cirkel_mm": round(float(max(height[inside].max(), 0)) * 1000, 2),
        "uitsteek_hele_schakel_mm": round(float(max(height.max(), 0)) * 1000, 2),
    }


def export_visual(mesh, path):
    shaded = trimesh.graph.smooth_shade(mesh, angle=np.radians(30))
    shaded.visual = trimesh.visual.TextureVisuals(material=BLACK)
    shaded.export(path, include_normals=True)
    return shaded


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("step_file", type=Path)
    parser.add_argument("output_dir", type=Path)
    parser.add_argument("--max-hulls", type=int, default=24)
    parser.add_argument("--threshold", type=float, default=0.03)
    args = parser.parse_args()
    args.output_dir.mkdir(parents=True, exist_ok=True)

    scene = trimesh.load(args.step_file)
    link = straighten(pick_link(scene))

    export_visual(link, args.output_dir / "schakel_visual.glb")
    lod = link.simplify_quadric_decimation(face_count=LOD_FACES)
    export_visual(lod, args.output_dir / "schakel_lod.glb")

    # Import here: coacd makes reading the STEP file crash if it is already loaded.
    import coacd

    print("Botsingsvorm berekenen...", flush=True)
    parts = coacd.run_coacd(
        coacd.Mesh(link.vertices, link.faces),
        threshold=args.threshold, max_convex_hull=args.max_hulls, seed=1,
    )
    hulls = trimesh.Scene()
    hull_volume = 0.0
    for index, (vertices, faces) in enumerate(parts):
        hull = trimesh.Trimesh(vertices, faces, process=True)
        hull_volume += hull.volume
        hulls.add_geometry(hull, node_name=f"hull_{index:02d}", geom_name=f"hull_{index:02d}")
    hulls.export(args.output_dir / "schakel_collision.glb")

    report = {
        "bron": args.step_file.name,
        "afmeting_x_y_z_mm": [round(float(v) * 1000, 1) for v in link.extents],
        "volume_cm3": round(float(link.volume) * 1e6, 1),
        "driehoeken_visual": len(link.faces),
        "driehoeken_lod": len(lod.faces),
        "botsing_hulls": len(parts),
        "botsing_volume_cm3": round(hull_volume * 1e6, 1),
        "boven": measure_flat(link, 1),
        "onder": measure_flat(link, -1),
    }
    (args.output_dir / "schakel_rapport.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
