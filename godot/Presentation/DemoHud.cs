#nullable enable

using Godot;

/// <summary>
/// On-screen information: the current status at the top centre, a collapsible legend at the bottom left,
/// and the full-window sensor image.
/// </summary>
public partial class DemoHud : CanvasLayer
{
    private const int FontSize = 13;
    private const float Margin = 10f;

    private const string LegendText =
        "Muis: slepen = draaien, middelste knop = verschuiven, wiel = zoomen\n" +
        "1 = overzicht, 2 = vanaf de sensor, 3 = dichtbij\n" +
        "4 = 3D / perfecte diepte / sensor, B = zwarte of witte schakels\n" +
        "L = sensor: formule of geprojecteerd licht\n" +
        "P = kandidaten (blauw), nog een keer = keuze (groen), nog een keer = verbergen\n" +
        "G = één pick uitvoeren, A = reeks van 10 picks\n" +
        "R = vullen starten, nog een keer R = stoppen op deze hoogte\n" +
        "Sensorbeeld: magenta = weinig licht, blauw = schaduw, geel = rand of oneens, oranje = overbelicht";

    private TextureRect _image = null!;
    private Label _status = null!;
    private PanelContainer _statusPanel = null!;
    private Label _legend = null!;
    private Button _legendToggle = null!;

    public override void _Ready()
    {
        BuildImage();
        BuildStatus();
        BuildLegend();
        SetLegendVisible(false);
    }

    /// <summary>Shows this text at the top centre; an empty text hides the panel.</summary>
    public void SetStatus(string text)
    {
        _status.Text = text;
        _statusPanel.Visible = text.Length > 0;
    }

    /// <summary>Opens or closes the legend, with key H or the button.</summary>
    public void ToggleLegend() => SetLegendVisible(!_legend.Visible);

    /// <summary>Shows an image over the 3D view, or returns to the 3D view when null.</summary>
    public void ShowImage(Image? image)
    {
        _image.Visible = image != null;
        if (image != null)
        {
            _image.Texture = ImageTexture.CreateFromImage(image);
        }
    }

    private void BuildImage()
    {
        _image = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _image.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_image);
    }

    private void BuildStatus()
    {
        _statusPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _statusPanel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _statusPanel.GrowHorizontal = Control.GrowDirection.Both;
        _statusPanel.OffsetTop = Margin;
        AddChild(_statusPanel);

        _status = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _status.AddThemeFontSizeOverride("font_size", FontSize);
        _statusPanel.AddChild(_status);
    }

    private void BuildLegend()
    {
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        panel.GrowVertical = Control.GrowDirection.Begin;
        panel.OffsetLeft = Margin;
        panel.OffsetBottom = -Margin;
        AddChild(panel);

        var box = new VBoxContainer();
        panel.AddChild(box);

        _legendToggle = new Button
        {
            Flat = true,
            FocusMode = Control.FocusModeEnum.None,
            Alignment = HorizontalAlignment.Left,
        };
        _legendToggle.AddThemeFontSizeOverride("font_size", FontSize);
        _legendToggle.Pressed += ToggleLegend;
        box.AddChild(_legendToggle);

        _legend = new Label();
        _legend.AddThemeFontSizeOverride("font_size", FontSize);
        box.AddChild(_legend);
        _legend.Text = LegendText;
    }

    private void SetLegendVisible(bool visible)
    {
        _legend.Visible = visible;
        _legendToggle.Text = visible ? "[-] Legenda (H)" : "[+] Legenda (H)";

        // The panel shrinks to its content again when the legend closes.
        Control panel = (Control)_legendToggle.GetParent().GetParent();
        panel.ResetSize();
        panel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        panel.OffsetLeft = Margin;
        panel.OffsetRight = Margin;
        panel.OffsetBottom = -Margin;
        panel.OffsetTop = -Margin;
    }
}
