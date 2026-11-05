using System.Collections.Generic;
using Godot;

/// <summary>Draws a stored fill as one mesh repeated many times, without physics.</summary>
public partial class BinFillView : MultiMeshInstance3D
{
    public BinFillView(Mesh linkMesh, IReadOnlyList<Transform3D> transforms)
    {
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = linkMesh,
            InstanceCount = transforms.Count,
        };
        for (int i = 0; i < transforms.Count; i++)
        {
            multiMesh.SetInstanceTransform(i, transforms[i]);
        }
        Multimesh = multiMesh;
    }
}
