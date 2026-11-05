using BinPicking.Core.Grasping;
using BinPicking.Core.Sensing;
using Godot;

/// <summary>Draws pick points as circles on a sensor image.</summary>
public static class GraspImageOverlay
{
	private const int CircleSteps = 128;

	public static void Draw(byte[] pixels, PinholeCamera camera, PickSelection selection)
	{
		if (selection.Mode == PickDisplayMode.Hidden)
		{
			return;
		}

		// Back to front, so the best candidate ends up on top.
		for (int i = selection.Candidates.Count - 1; i >= 0; i--)
		{
			DrawCircle(pixels, camera, selection.Candidates[i], selection.IsHighlighted(i), selection.Settings.CupDiameter / 2f);
		}
	}

	private static void DrawCircle(byte[] pixels, PinholeCamera camera, GraspCandidate grasp, bool highlighted, float cupRadius)
	{
		(byte r, byte g, byte b) = highlighted ? ((byte)20, (byte)255, (byte)40) : ((byte)30, (byte)210, (byte)255);
		float radius = camera.Fx * cupRadius / grasp.Z;
		float lineWidth = highlighted ? 3f : 2f;
		for (int s = 0; s < CircleSteps; s++)
		{
			float angle = s * Mathf.Tau / CircleSteps;
			for (float extra = 0f; extra < lineWidth; extra += 1f)
			{
				int x = grasp.PixelX + Mathf.RoundToInt((radius + extra) * Mathf.Cos(angle));
				int y = grasp.PixelY + Mathf.RoundToInt((radius + extra) * Mathf.Sin(angle));
				if (x >= 0 && y >= 0 && x < camera.Width && y < camera.Height)
				{
					int at = (y * camera.Width + x) * 3;
					pixels[at] = r;
					pixels[at + 1] = g;
					pixels[at + 2] = b;
				}
			}
		}
	}
}
