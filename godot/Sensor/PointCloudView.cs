#nullable enable

using System.Linq;
using BinPicking.Core.Sensing;
using Godot;
using static PalletDimensions;

/// <summary>
/// Draws a depth measurement as a point cloud in the 3D view: one dot per measured pixel, placed in world space
/// with the sensor mount. Points inside the bin are coloured by height, from blue (low) to red (high); the walls,
/// rim, pallet and ground are grey. A hole in the measurement is simply a missing dot. The sensor does not see this.
/// </summary>
public partial class PointCloudView : MeshInstance3D
{
    /// <summary>Uses every second pixel in both directions: a quarter of the points, still dense on a link.</summary>
    public const int PixelStep = 2;

    /// <summary>Leaves out the far plane of the render, which is not a measurement but empty background.</summary>
    public static float MaxDepth => SensorMount.Height + 0.5f;

    private const float PointSizePixels = 2f;

    /// <summary>Share of the lowest and highest points left out of the colour scale, so a few outliers do not flatten it.</summary>
    private const float ScaleClip = 0.02f;

    /// <summary>
    /// The camera also sees the inside of the walls. Those points stay grey, otherwise the walls from floor to rim
    /// would stretch the colour scale and the pile would get only one or two colours.
    /// </summary>
    private const float WallMargin = 0.01f;

    /// <summary>The colour scale spans at least this many metres.</summary>
    private const float MinimumScale = 0.05f;

    private static readonly Color OutsideBinColor = new(0.55f, 0.55f, 0.55f);

    // From low to high, like a height map: blue, cyan, green, yellow, red.
    private static readonly Color[] HeightColors =
    {
        new(0.15f, 0.2f, 0.85f),
        new(0.1f, 0.8f, 0.9f),
        new(0.2f, 0.85f, 0.25f),
        new(0.95f, 0.85f, 0.1f),
        new(0.9f, 0.15f, 0.1f),
    };

    public PointCloudView()
    {
        Layers = 1u << (DepthSensorRig.ModelLayer - 1);
        CastShadow = ShadowCastingSetting.Off;
        Visible = false;
        MaterialOverride = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            UsePointSize = true,
            PointSize = PointSizePixels,
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
        };
    }

    /// <summary>The lowest and highest height of the colour scale, in metres above the bin floor.</summary>
    public (float Low, float High) HeightRange { get; private set; }

    /// <summary>Replaces the drawn points with this cloud and makes them visible.</summary>
    public void ShowCloud(PointCloud cloud)
    {
        var points = new Vector3[cloud.Count];
        var inBin = new bool[cloud.Count];
        for (int i = 0; i < cloud.Count; i++)
        {
            points[i] = SensorMount.ToWorldPoint(cloud.X(i), cloud.Y(i), cloud.Z(i));
            inBin[i] = Mathf.Abs(points[i].X) < InnerLength / 2f - WallMargin
                       && Mathf.Abs(points[i].Z) < InnerWidth / 2f - WallMargin;
        }

        HeightRange = ScaleOf(points.Where((_, i) => inBin[i]).Select(p => p.Y).ToArray());
        (float low, float high) = HeightRange;
        var colors = new Color[cloud.Count];
        for (int i = 0; i < cloud.Count; i++)
        {
            colors[i] = inBin[i] ? HeightColor((points[i].Y - low) / (high - low)) : OutsideBinColor;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = points;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        var mesh = new ArrayMesh();
        if (points.Length > 0)
        {
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Points, arrays);
        }
        Mesh = mesh;
        Visible = true;
    }

    /// <summary>Hides the points; they are drawn again with <see cref="ShowCloud"/>.</summary>
    public void HideCloud() => Visible = false;

    /// <summary>The colour scale runs over the heights in the bin, without the outer few percent.</summary>
    private static (float Low, float High) ScaleOf(float[] heights)
    {
        if (heights.Length == 0)
        {
            return (0f, RimHeight);
        }
        System.Array.Sort(heights);
        int clip = (int)(heights.Length * ScaleClip);
        float low = heights[clip];
        // An empty floor would otherwise spread its noise of a millimetre or two over all colours.
        return (low, Mathf.Max(heights[heights.Length - 1 - clip], low + MinimumScale));
    }

    /// <summary>Interpolates the height colours; 0 is the bottom of the scale and 1 the top.</summary>
    private static Color HeightColor(float t)
    {
        float scaled = Mathf.Clamp(t, 0f, 1f) * (HeightColors.Length - 1);
        int index = Mathf.Min((int)scaled, HeightColors.Length - 2);
        return HeightColors[index].Lerp(HeightColors[index + 1], scaled - index);
    }
}
