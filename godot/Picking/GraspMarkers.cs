using System.Collections.Generic;
using BinPicking.Core.Grasping;
using Godot;

/// <summary>Shows pick points in 3D as a disc with an approach line. The sensor does not see these.</summary>
public partial class GraspMarkers : Node3D
{
    private const float DiscThickness = 0.003f;
    private const float LineRadius = 0.003f;
    private const float BestLineLength = 0.25f;
    private const float CandidateLineLength = 0.06f;

    private static readonly Color BestColor = new(0.1f, 1f, 0.2f);
    private static readonly Color CandidateColor = new(0.1f, 0.8f, 1f);

    /// <summary>Replaces the markers with those of the current display mode.</summary>
    public void ShowPicks(PickSelection selection)
    {
        foreach (Node child in GetChildren())
        {
            child.QueueFree();
        }

        if (selection.Mode == PickDisplayMode.Hidden)
        {
            return;
        }

        IReadOnlyList<GraspCandidate> candidates = selection.Candidates;
        for (int i = 0; i < candidates.Count; i++)
        {
            AddChild(CreateMarker(candidates[i], selection.IsHighlighted(i), selection.Settings.CupDiameter / 2f));
        }
    }

    private static Node3D CreateMarker(GraspCandidate grasp, bool highlighted, float radius)
    {
        Vector3 normal = SensorMount.ToWorldDirection(grasp.NormalX, grasp.NormalY, grasp.NormalZ).Normalized();
        Vector3 helper = Mathf.Abs(normal.Dot(Vector3.Forward)) > 0.9f ? Vector3.Right : Vector3.Forward;
        Vector3 x = helper.Cross(normal).Normalized();

        var marker = new Node3D
        {
            Position = SensorMount.ToWorldPoint(grasp.X, grasp.Y, grasp.Z),
            Basis = new Basis(x, normal, x.Cross(normal)),
        };
        var material = new StandardMaterial3D
        {
            AlbedoColor = highlighted ? BestColor : CandidateColor,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        float line = highlighted ? BestLineLength : CandidateLineLength;
        marker.AddChild(CreateCylinder(radius, DiscThickness, Vector3.Up * DiscThickness, material));
        marker.AddChild(CreateCylinder(LineRadius, line, Vector3.Up * line / 2f, material));
        return marker;
    }

    private static MeshInstance3D CreateCylinder(float radius, float height, Vector3 position, Material material)
    {
        return new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height, Material = material },
            Position = position,
            Layers = 1u << (DepthSensorRig.ModelLayer - 1),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
    }
}
