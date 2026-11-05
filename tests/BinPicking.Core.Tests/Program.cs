using BinPicking.Core.Sensing;

// Eenvoudige tests zonder extra pakketten. Starten met:
//   dotnet run --project tests/BinPicking.Core.Tests

int failures = 0;
var camera = PinholeCamera.FromVerticalFov(200, 200, 40f);
var settings = new DepthSensorSettings();
const float Bright = 0.7f;
const float Black = 0.03f;

Check("Helder vlak heeft vrijwel geen gaten", MissingShare(Run(Plane(2f), Bright)) < 0.001);
Check("Zwart vlak heeft duidelijk meer gaten dan helder",
    MissingShare(Run(Plane(2f), Black)) > MissingShare(Run(Plane(2f), Bright)) + 0.2);
Check("Schuin zwart vlak heeft meer gaten dan recht zwart",
    MissingShare(Run(Tilted(2f, 1.5f), Black)) > MissingShare(Run(Plane(2f), Black)));
Check("Ruis neemt toe met de afstand", NoiseOf(Plane(3f), Bright) > NoiseOf(Plane(1.5f), Bright));
Check("Ruis is groter op zwart dan op helder", NoiseOf(Plane(2f), Black) > NoiseOf(Plane(2f), Bright));
Check("Helder vlak op de referentieafstand heeft de ingestelde ruis",
    Math.Abs(NoiseOf(Plane(2f), Bright) - settings.BaseNoise) < settings.BaseNoise * 0.1);

DepthSensorResult first = Run(Plane(2f), Black);
DepthSensorResult second = Run(Plane(2f), Black);
Check("Dezelfde seed geeft hetzelfde beeld", first.Depth.AsSpan().SequenceEqual(second.Depth));

// Een blok op een vloer: de projector staat rechts, dus de schaduw valt links van het blok.
float[] scene = Block(out int blockLeft, out int blockRight);
float[] fromProjector = ShiftedBlockView(blockLeft, blockRight);
DepthSensorResult shadowed = DepthSensorModel.Apply(camera, scene, fromProjector, Fill(Bright), settings);
int row = camera.Height / 2 * camera.Width;
int shadowLeft = CountShadow(shadowed, row, 0, blockLeft);
int shadowRight = CountShadow(shadowed, row, blockRight + 1, camera.Width);
Check($"Schaduw valt alleen aan de kant van het blok weg van de projector (links {shadowLeft}, rechts {shadowRight})",
    shadowLeft > 0 && shadowRight == 0);
Check("Randen van het blok geven gaten",
    Enumerable.Range(0, camera.Width * camera.Height).Any(i => shadowed.Missing[i] == MissingReason.Edge));

bool threw = false;
try
{
    DepthSensorModel.Apply(camera, new float[3], new float[3], new float[3], settings);
}
catch (ArgumentException)
{
    threw = true;
}
Check("Verkeerde beeldgrootte geeft een fout", threw);

Console.WriteLine(failures == 0 ? "RESULTAAT: GESLAAGD" : $"RESULTAAT: {failures} TESTS MISLUKT");
return failures == 0 ? 0 : 1;

void Check(string name, bool passed)
{
    Console.WriteLine($"{(passed ? "OK  " : "FOUT")} {name}");
    if (!passed)
    {
        failures++;
    }
}

DepthSensorResult Run(float[] depth, float reflectance) =>
    DepthSensorModel.Apply(camera, depth, depth, Fill(reflectance), settings);

double MissingShare(DepthSensorResult result) =>
    result.Depth.Count(float.IsNaN) / (double)result.Depth.Length;

double NoiseOf(float[] depth, float reflectance)
{
    float[] measured = Run(depth, reflectance).Depth;
    double sum = 0;
    int n = 0;
    for (int i = 0; i < measured.Length; i++)
    {
        if (!float.IsNaN(measured[i]))
        {
            double error = measured[i] - depth[i];
            sum += error * error;
            n++;
        }
    }
    return Math.Sqrt(sum / n);
}

float[] Fill(float value)
{
    var result = new float[camera.Width * camera.Height];
    Array.Fill(result, value);
    return result;
}

float[] Plane(float z) => Fill(z);

float[] Tilted(float z, float slopePerMeter)
{
    var result = new float[camera.Width * camera.Height];
    for (int y = 0; y < camera.Height; y++)
    {
        for (int x = 0; x < camera.Width; x++)
        {
            // Vlak z = z0 + s * X, dus z = z0 / (1 - s * (x - cx) / fx).
            result[y * camera.Width + x] = z / (1f - slopePerMeter * (x + 0.5f - camera.Cx) / camera.Fx);
        }
    }
    return result;
}

float[] Block(out int left, out int right)
{
    left = 80;
    right = 120;
    float[] result = Fill(2f);
    for (int y = 0; y < camera.Height; y++)
    {
        for (int x = left; x <= right; x++)
        {
            result[y * camera.Width + x] = 1.5f;
        }
    }
    return result;
}

float[] ShiftedBlockView(int left, int right)
{
    // Wat de projector ziet: het blok verschuift in beeld naar links, meer dan de vloer.
    float[] result = Fill(2f);
    int shift = (int)MathF.Round(camera.Fx * settings.ProjectorBaseline / 1.5f);
    for (int y = 0; y < camera.Height; y++)
    {
        for (int x = Math.Max(left - shift, 0); x <= Math.Max(right - shift, 0); x++)
        {
            result[y * camera.Width + x] = 1.5f;
        }
    }
    return result;
}

int CountShadow(DepthSensorResult result, int rowStart, int from, int to)
{
    int count = 0;
    for (int x = from; x < to; x++)
    {
        if (result.Missing[rowStart + x] == MissingReason.Shadow)
        {
            count++;
        }
    }
    return count;
}
