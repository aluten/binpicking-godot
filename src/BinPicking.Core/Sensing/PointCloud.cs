using System.Globalization;

namespace BinPicking.Core.Sensing;

/// <summary>
/// The measured depth as a set of 3D points in camera space: one point per pixel that has a measurement.
/// A hole in the depth image is simply a missing point, so the cloud shows exactly what the sensor delivered.
/// </summary>
public sealed class PointCloud
{
    private readonly float[] _xs;
    private readonly float[] _ys;
    private readonly float[] _zs;

    private PointCloud(float[] xs, float[] ys, float[] zs)
    {
        _xs = xs;
        _ys = ys;
        _zs = zs;
    }

    /// <summary>The number of points.</summary>
    public int Count => _xs.Length;

    /// <summary>Camera-space X of point <paramref name="i"/>, in metres. To the right in the image.</summary>
    public float X(int i) => _xs[i];

    /// <summary>Camera-space Y of point <paramref name="i"/>, in metres. Down in the image.</summary>
    public float Y(int i) => _ys[i];

    /// <summary>Camera-space Z of point <paramref name="i"/>, in metres. Away from the camera.</summary>
    public float Z(int i) => _zs[i];

    /// <summary>
    /// Turns a depth image into points by unprojecting each measured pixel through the camera model.
    /// </summary>
    /// <param name="camera">The camera that belongs to the depth image.</param>
    /// <param name="depth">Depth in metres along the camera axis, NaN for a hole.</param>
    /// <param name="step">Uses every n-th pixel in both directions, so the cloud stays light enough to draw.</param>
    /// <param name="maxDepth">Pixels further away than this are left out, such as the background.</param>
    public static PointCloud FromDepth(PinholeCamera camera, float[] depth, int step = 1, float maxDepth = float.PositiveInfinity)
    {
        if (depth.Length != camera.Width * camera.Height)
        {
            throw new ArgumentException("The depth image does not match the camera size.", nameof(depth));
        }
        if (step < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "The step must be at least 1.");
        }

        var xs = new List<float>();
        var ys = new List<float>();
        var zs = new List<float>();
        for (int y = 0; y < camera.Height; y += step)
        {
            for (int x = 0; x < camera.Width; x += step)
            {
                float z = depth[y * camera.Width + x];
                if (!float.IsFinite(z) || z <= 0f || z > maxDepth)
                {
                    continue;
                }
                (float px, float py) = camera.Unproject(x, y, z);
                xs.Add(px);
                ys.Add(py);
                zs.Add(z);
            }
        }
        return new PointCloud(xs.ToArray(), ys.ToArray(), zs.ToArray());
    }

    /// <summary>
    /// Writes the cloud as an ASCII PLY file in camera space, in metres. Tools such as CloudCompare, MeshLab and
    /// Open3D open this format, the same way they would open the export of a real 3D camera.
    /// </summary>
    public void WritePly(TextWriter writer)
    {
        writer.NewLine = "\n";
        writer.WriteLine("ply");
        writer.WriteLine("format ascii 1.0");
        writer.WriteLine("comment Bin picking demo point cloud");
        writer.WriteLine("comment Camera space in metres: X right, Y down in the image, Z away from the camera");
        writer.WriteLine($"element vertex {Count}");
        writer.WriteLine("property float x");
        writer.WriteLine("property float y");
        writer.WriteLine("property float z");
        writer.WriteLine("end_header");
        for (int i = 0; i < Count; i++)
        {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{_xs[i]:G6} {_ys[i]:G6} {_zs[i]:G6}"));
        }
    }
}
