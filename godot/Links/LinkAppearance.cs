#nullable enable

using Godot;

/// <summary>
/// The colour of the links. All links share one mesh and material, so one change applies everywhere.
/// The sensor sees this colour too, so changing it really changes how much light returns.
/// </summary>
public sealed class LinkAppearance
{
    private static readonly Color WhiteColor = new(0.9f, 0.9f, 0.9f);

    // Remembered once, because the material outlives a scene reload.
    private static Color? _originalColor;

    private readonly BaseMaterial3D? _material;

    public LinkAppearance(Mesh mesh)
    {
        _material = mesh.SurfaceGetMaterial(0) as BaseMaterial3D;
        if (_material != null)
        {
            _originalColor ??= _material.AlbedoColor;
        }
        SetWhite(false);
    }

    public bool IsWhite { get; private set; }

    public void SetWhite(bool white)
    {
        IsWhite = white;
        if (_material != null)
        {
            _material.AlbedoColor = white ? WhiteColor : _originalColor!.Value;
        }
    }
}
