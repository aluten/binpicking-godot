#nullable enable

using Godot;

/// <summary>On-screen text with the controls, and the full-window sensor image.</summary>
public partial class DemoHud : CanvasLayer
{
	private TextureRect _image = null!;
	private Label _status = null!;

	public override void _Ready()
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

		var panel = new PanelContainer { Position = new Vector2(12f, 12f) };
		AddChild(panel);
		var box = new VBoxContainer();
		panel.AddChild(box);
		_status = new Label();
		box.AddChild(_status);
		box.AddChild(new Label
		{
			Text = "Muis: slepen = draaien, middelste knop = verschuiven, wiel = zoomen\n" +
				   "1 = overzicht, 2 = vanaf de sensor, 3 = dichtbij\n" +
				   "R = vullen starten, nog een keer R = stoppen op deze hoogte\n" +
				   "4 = 3D / perfecte diepte / sensor, B = zwarte of lichte schakels\n" +
				   "P = kandidaten (blauw), nog een keer = keuze (groen), nog een keer = verbergen",
		});
	}

	public void SetStatus(string text) => _status.Text = text;

	/// <summary>Shows an image over the 3D view, or returns to the 3D view when null.</summary>
	public void ShowImage(Image? image)
	{
		_image.Visible = image != null;
		if (image != null)
		{
			_image.Texture = ImageTexture.CreateFromImage(image);
		}
	}
}
