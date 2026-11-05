using Godot;

/// <summary>Background, lights and ground of the demo. Only affects the overview camera image.</summary>
public partial class DemoEnvironment : Node3D
{
	public override void _Ready()
	{
		AddChild(new WorldEnvironment
		{
			Environment = new Godot.Environment
			{
				BackgroundMode = Godot.Environment.BGMode.Color,
				BackgroundColor = new Color(0.78f, 0.8f, 0.83f),
				AmbientLightSource = Godot.Environment.AmbientSource.Color,
				AmbientLightColor = Colors.White,
				AmbientLightEnergy = 0.45f,
			},
		});
		AddChild(new DirectionalLight3D
		{
			RotationDegrees = new Vector3(-65f, -30f, 0f),
			LightEnergy = 1.6f,
			ShadowEnabled = true,
		});
		AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-40f, 150f, 0f), LightEnergy = 0.6f });

		AddChild(new MeshInstance3D
		{
			Position = new Vector3(0f, -PalletDimensions.PalletHeight - 0.01f, 0f),
			Mesh = new BoxMesh
			{
				Size = new Vector3(8f, 0.02f, 8f),
				Material = new StandardMaterial3D { AlbedoColor = new Color(0.62f, 0.63f, 0.65f), Roughness = 0.85f },
			},
		});
	}
}
