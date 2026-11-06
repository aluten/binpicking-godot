using System.Collections.Generic;
using Godot;

/// <summary>Saves and loads the link transforms of a filled bin. The file stays out of Git.</summary>
public static class BinFillStore
{
    private const string FillPath = "res://generated/pallet_vulling.json";

    /// <summary>Tells whether a fill has been saved.</summary>
    public static bool Exists() => FileAccess.FileExists(FillPath);

    /// <summary>Deletes the saved fill, which makes the demo fill the bin again.</summary>
    public static void Delete() => DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(FillPath));

    /// <summary>Saves each link as position and quaternion.</summary>
    /// <param name="transforms">The link transforms.</param>
    /// <param name="seed">The random seed of the fill, for reference.</param>
    public static void Save(IReadOnlyList<Transform3D> transforms, int seed)
    {
        var rows = new Godot.Collections.Array();
        foreach (Transform3D t in transforms)
        {
            Quaternion q = t.Basis.GetRotationQuaternion();
            rows.Add(new Godot.Collections.Array
            {
                t.Origin.X, t.Origin.Y, t.Origin.Z, q.X, q.Y, q.Z, q.W,
            });
        }

        var data = new Godot.Collections.Dictionary { { "seed", seed }, { "links", rows } };
        using FileAccess file = FileAccess.Open(FillPath, FileAccess.ModeFlags.Write);
        file.StoreString(Json.Stringify(data));
    }

    /// <summary>Loads the saved link transforms.</summary>
    public static List<Transform3D> Load()
    {
        using FileAccess file = FileAccess.Open(FillPath, FileAccess.ModeFlags.Read);
        var data = Json.ParseString(file.GetAsText()).AsGodotDictionary();
        var result = new List<Transform3D>();
        foreach (Variant row in data["links"].AsGodotArray())
        {
            var v = row.AsGodotArray();
            var rotation = new Quaternion(v[3].AsSingle(), v[4].AsSingle(), v[5].AsSingle(), v[6].AsSingle());
            var origin = new Vector3(v[0].AsSingle(), v[1].AsSingle(), v[2].AsSingle());
            result.Add(new Transform3D(new Basis(rotation.Normalized()), origin));
        }
        return result;
    }
}
