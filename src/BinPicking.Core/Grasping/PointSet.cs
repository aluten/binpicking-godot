namespace BinPicking.Core.Grasping;

/// <summary>Reusable list of camera-space points, so a search does not allocate per location.</summary>
internal sealed class PointSet
{
    public List<float> Xs { get; } = new();

    public List<float> Ys { get; } = new();

    public List<float> Zs { get; } = new();

    public int Count => Xs.Count;

    public void Clear()
    {
        Xs.Clear();
        Ys.Clear();
        Zs.Clear();
    }

    public void Add(float x, float y, float z)
    {
        Xs.Add(x);
        Ys.Add(y);
        Zs.Add(z);
    }
}
