using BinPicking.Core.Grasping;
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

// Pickpunt zoeken. Beeld van 1,5 m hoogte op een vloer op 2 m, met een bak van +/- 0,4 m.
var pickCamera = PinholeCamera.FromVerticalFov(400, 400, 30f);
var bin = new BinRegion(-0.4f, 0.4f, -0.4f, 0.4f, 1.5f, 2.1f);
var grasp = new GraspSettings { SearchStep = 3 };

float[] Scene(float floor) { var d = new float[400 * 400]; Array.Fill(d, floor); return d; }
void Box(float[] d, int x0, int y0, int x1, int y1, float z)
{
    for (int y = y0; y <= y1; y++) { for (int x = x0; x <= x1; x++) { d[y * 400 + x] = z; } }
}
var (noneFound, noneReport) = GraspPlanner.FindGrasps(pickCamera, Enumerable.Repeat(float.NaN, 160000).ToArray(), bin, grasp);
Check("Zonder metingen is er geen pickpunt", noneFound.Count == 0 && noneReport.Evaluated == 0);

float[] patch = Scene(2.05f);
Box(patch, 150, 150, 250, 250, 1.8f);
var (patchFound, _) = GraspPlanner.FindGrasps(pickCamera, patch, bin, grasp);
GraspCandidate? top = patchFound.FirstOrDefault();
Check("Hoog vlak wordt gekozen boven de vloer", top != null && MathF.Abs(top.Z - 1.8f) < 0.002f);
Check("Het pickpunt ligt op het vlak", top != null && top.PixelX is >= 150 and <= 250 && top.PixelY is >= 150 and <= 250);
Check("Perfecte diepte op dat vlak dicht volledig af", top != null && GraspPlanner.TrueSealShare(pickCamera, patch, top, grasp) > 0.99f);
Check("Kandidaten liggen minstens een napbreedte uit elkaar", patchFound.Count > 1 && DistanceBetween(patchFound[0], patchFound[1]) >= 0.03f - 1e-4f);

float[] tinyPatch = Scene(2.05f);
Box(tinyPatch, 195, 195, 205, 205, 1.8f);
var (tinyFound, _) = GraspPlanner.FindGrasps(pickCamera, tinyPatch, bin, grasp);
Check("Een vlak kleiner dan de nap wordt niet gekozen", tinyFound.All(c => c.Z > 2f));

float[] holey = Scene(1.9f);
var holeRandom = new Random(3);
for (int i = 0; i < holey.Length; i++) { if (holeRandom.NextDouble() < 0.97) { holey[i] = float.NaN; } }
var (holeyFound, holeyReport) = GraspPlanner.FindGrasps(pickCamera, holey, bin, grasp);
Check("Te weinig metingen geeft geen pickpunt", holeyFound.Count == 0 && holeyReport.TooFewPoints > 0);

var noisy = Scene(1.9f);
var noiseRandom = new Random(4);
for (int i = 0; i < noisy.Length; i++) { noisy[i] += (float)(noiseRandom.NextDouble() - 0.5) * 0.04f; }
var (noisyFound, noisyReport) = GraspPlanner.FindGrasps(pickCamera, noisy, bin, grasp);
Check("Een ruw oppervlak geeft geen pickpunt", noisyFound.Count == 0 && noisyReport.NotFlat > 0);

float[] steep = new float[400 * 400];
for (int y = 0; y < 400; y++) { for (int x = 0; x < 400; x++) { steep[y * 400 + x] = 1.9f / (1f - 1.5f * (x + 0.5f - pickCamera.Cx) / pickCamera.Fx); } }
var (steepFound, steepReport) = GraspPlanner.FindGrasps(pickCamera, steep, bin, grasp);
Check("Een te schuin vlak wordt geweigerd", steepFound.Count == 0 && steepReport.TooTilted > 0);

float[] walled = Scene(2.05f);
Box(walled, 150, 150, 250, 250, 1.9f);
Box(walled, 196, 150, 204, 250, 1.8f);
var (walledFound, walledReport) = GraspPlanner.FindGrasps(pickCamera, walled, bin, new GraspSettings { SearchStep = 3, ToolDiameter = 0.09f });
Check("De houder mag een hoger onderdeel niet raken", walledReport.ToolCollision > 0 && walledFound.All(c => c.Z < 1.85f || MathF.Abs(c.X) > 0.04f));

float[] edge = Scene(1.9f);
var smallBin = bin with { MinX = -0.02f, MaxX = 0.02f, MinY = -0.02f, MaxY = 0.02f };
var (edgeFound, edgeReport) = GraspPlanner.FindGrasps(pickCamera, edge, smallBin, grasp);
Check("Te dicht bij de wand wordt geweigerd", edgeFound.Count == 0 && edgeReport.OutsideOpening > 0);

Console.WriteLine(failures == 0 ? "RESULTAAT: GESLAAGD" : $"RESULTAAT: {failures} TESTS MISLUKT");
return failures == 0 ? 0 : 1;

float DistanceBetween(GraspCandidate a, GraspCandidate b) =>
    MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));

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
