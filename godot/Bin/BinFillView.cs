using System.Collections.Generic;
using Godot;

/// <summary>Draws a stored fill as one mesh repeated many times, without physics.</summary>
public partial class BinFillView : MultiMeshInstance3D
{
    private readonly Mesh _linkMesh;

    public BinFillView(Mesh linkMesh, IReadOnlyList<Transform3D> transforms)
    {
        _linkMesh = linkMesh;
        Layers = 1u | (1u << (DepthSensorRig.LinkLayer - 1));
        Show(transforms);
    }

    /// <summary>Draws these transforms, replacing what was drawn.</summary>
    public void Show(IReadOnlyList<Transform3D> transforms)
    {
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = _linkMesh,
            InstanceCount = transforms.Count,
        };
        for (int i = 0; i < transforms.Count; i++)
        {
            multiMesh.SetInstanceTransform(i, transforms[i]);
        }
        Multimesh = multiMesh;
    }

    /// <summary>Stops drawing these links, because a pick region draws them now.</summary>
    public void Hide(ISet<int> indices, IReadOnlyList<Transform3D> transforms)
    {
        var hidden = new Transform3D(Basis.Identity.Scaled(Vector3.One * 1e-6f), new Vector3(0f, -10f, 0f));
        foreach (int index in indices)
        {
            Multimesh.SetInstanceTransform(index, hidden);
        }
    }
}
