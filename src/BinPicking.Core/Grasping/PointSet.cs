namespace BinPicking.Core.Grasping;

/// <summary>Reusable list of camera-space points, so a search does not allocate per location.</summary>
internal sealed class PointSet
{
    /// <summary>X coordinates in metres; Ys and Zs have the same order.</summary>
    public List<float> Xs { get; } = new();

    public List<float> Ys { get; } = new();

    public List<float> Zs { get; } = new();

    /// <summary>The number of points.</summary>
    public int Count => Xs.Count;

    /// <summary>Empties the list and keeps the memory.</summary>
    public void Clear()
    {
        Xs.Clear();
        Ys.Clear();
        Zs.Clear();
    }

    /// <summary>Adds one point.</summary>
    public void Add(float x, float y, float z)
    {
        Xs.Add(x);
        Ys.Add(y);
        Zs.Add(z);
    }
}
