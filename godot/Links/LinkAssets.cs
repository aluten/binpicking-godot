#nullable enable

using System.Collections.Generic;
using Godot;

/// <summary>Loads the files created by tools/cad_pipeline/convert_link.py.</summary>
public static class LinkAssets
{
    public const string VisualPath = "res://generated/schakel_visual.glb";
    public const string LodPath = "res://generated/schakel_lod.glb";
    public const string CollisionPath = "res://generated/schakel_collision.glb";

    private const float ShapeMargin = 0.001f;

    public static Mesh? LoadSingleMesh(string path)
    {
        var scene = ResourceLoader.Load<PackedScene>(path);
        if (scene == null)
        {
            GD.PrintErr($"Kan {path} niet laden.");
            return null;
        }

        Node root = scene.Instantiate();
        var found = new List<(Mesh Mesh, Transform3D Offset)>();
        CollectMeshes(root, Transform3D.Identity, found);
        root.Free();
        return found.Count > 0 ? found[0].Mesh : null;
    }

    public static List<(Shape3D Shape, Transform3D Offset)> LoadCollisionShapes()
    {
        var shapes = new List<(Shape3D, Transform3D)>();
        var scene = ResourceLoader.Load<PackedScene>(CollisionPath);
        if (scene == null)
        {
            GD.PrintErr($"Kan {CollisionPath} niet laden.");
            return shapes;
        }

        Node root = scene.Instantiate();
        var meshes = new List<(Mesh Mesh, Transform3D Offset)>();
        CollectMeshes(root, Transform3D.Identity, meshes);
        root.Free();

        foreach ((Mesh mesh, Transform3D offset) in meshes)
        {
            ConvexPolygonShape3D shape = mesh.CreateConvexShape(true, false);
            shape.Margin = ShapeMargin;
            shapes.Add((shape, offset));
        }
        return shapes;
    }

    private static void CollectMeshes(Node node, Transform3D parent, List<(Mesh, Transform3D)> result)
    {
        Transform3D current = parent;
        if (node is Node3D node3D)
        {
            current = parent * node3D.Transform;
        }
        if (node is MeshInstance3D { Mesh: not null } instance)
        {
            result.Add((instance.Mesh, current));
        }
        foreach (Node child in node.GetChildren())
        {
            CollectMeshes(child, current, result);
        }
    }
}
