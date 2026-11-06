using System.Collections.Generic;
using System.Linq;
using Godot;
using static PalletDimensions;

/// <summary>Checks a fill geometrically and prints the result to the Output panel.</summary>
public static class BinFillInspector
{
    // Limits in metres. A little overlap is normal, because collision shapes differ from the mesh.
    private const float MaxFloorPenetration = 0.005f;
    private const float WallReportDistance = 0.005f;

    /// <summary>Reports how far links reach into the walls and floor, and how high the pile is.</summary>
    public static void Report(IReadOnlyList<Transform3D> transforms, Mesh linkMesh)
    {
        Vector3[] points = linkMesh.GetFaces().Distinct().ToArray();
        int outside = 0;
        int aboveRim = 0;
        float top = 0f;
        float deepest = 0f;
        float deepestWall = 0f;
        foreach (Transform3D t in transforms)
        {
            float low = float.MaxValue;
            float high = float.MinValue;
            float reachX = 0f;
            float reachZ = 0f;
            foreach (Vector3 point in points)
            {
                Vector3 world = t * point;
                low = Mathf.Min(low, world.Y);
                high = Mathf.Max(high, world.Y);
                reachX = Mathf.Max(reachX, Mathf.Abs(world.X));
                reachZ = Mathf.Max(reachZ, Mathf.Abs(world.Z));
            }

            top = Mathf.Max(top, high);
            deepest = Mathf.Max(deepest, -low);
            deepestWall = Mathf.Max(deepestWall, Mathf.Max(reachX - InnerLength / 2f, reachZ - InnerWidth / 2f));
            if (reachX > InnerLength / 2f + WallReportDistance || reachZ > InnerWidth / 2f + WallReportDistance)
            {
                outside++;
            }
            if (high > RimHeight)
            {
                aboveRim++;
            }
        }

        GD.Print($"INFO Schakels: {transforms.Count} (een volle bak is {BinFiller.FullBinLinks})");
        GD.Print($"{(deepestWall < PlankThickness ? "OK  " : "FOUT")} Diepst in de wand: {deepestWall * 1000f:F1} mm " +
                 $"(plank {PlankThickness * 1000f:F0} mm); {outside} schakels meer dan 5 mm");
        GD.Print($"{(deepest <= MaxFloorPenetration ? "OK  " : "FOUT")} Diepst in de bodem: {deepest * 1000f:F1} mm (maximaal 5 mm)");
        GD.Print($"INFO Hoogste punt: {top * 1000f:F0} mm, rand op {RimHeight * 1000f:F0} mm; boven de rand: {aboveRim} schakels");
    }
}
