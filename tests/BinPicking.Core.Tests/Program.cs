using BinPicking.Core.Grasping;
using BinPicking.Core.Picking;
using BinPicking.Core.Sensing;

// Simple tests without extra packages. Run with:
//   dotnet run --project tests/BinPicking.Core.Tests

int failures = 0;
var camera = PinholeCamera.FromVerticalFov(200, 200, 40f);
var settings = new DepthSensorSettings();
const float Bright = 0.7f;
const float Black = 0.03f;

Check("A bright surface has almost no holes", MissingShare(Run(Plane(2f), Bright)) < 0.001);
Check("A black surface has clearly more holes than a bright one",
    MissingShare(Run(Plane(2f), Black)) > MissingShare(Run(Plane(2f), Bright)) + 0.2);
Check("An oblique black surface has more holes than a facing one",
    MissingShare(Run(Tilted(2f, 1.5f), Black)) > MissingShare(Run(Plane(2f), Black)));
Check("Noise grows with distance", NoiseOf(Plane(3f), Bright) > NoiseOf(Plane(1.5f), Bright));
Check("Noise is larger on black than on bright", NoiseOf(Plane(2f), Black) > NoiseOf(Plane(2f), Bright));
Check("A bright surface at the reference distance has the configured noise",
    Math.Abs(NoiseOf(Plane(2f), Bright) - settings.BaseNoise) < settings.BaseNoise * 0.1);

DepthSensorResult first = Run(Plane(2f), Black);
DepthSensorResult second = Run(Plane(2f), Black);
Check("The same seed gives the same image", first.Depth.AsSpan().SequenceEqual(second.Depth));

// A block on a floor: the projector is on the right, so the shadow falls left of the block.
float[] scene = Block(out int blockLeft, out int blockRight);
float[] fromProjector = ShiftedBlockView(blockLeft, blockRight);
DepthSensorResult shadowed = DepthSensorModel.Apply(camera, scene, fromProjector, Fill(Bright), settings);
int row = camera.Height / 2 * camera.Width;
int shadowLeft = CountShadow(shadowed, row, 0, blockLeft);
int shadowRight = CountShadow(shadowed, row, blockRight + 1, camera.Width);
Check($"Shadow falls only on the side away from the projector (left {shadowLeft}, right {shadowRight})",
    shadowLeft > 0 && shadowRight == 0);
Check("Block edges produce holes",
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
Check("A wrong image size throws", threw);

// Pick point search. A view from 1.5 m onto a floor at 2 m, with a bin of +/- 0.4 m.
var pickCamera = PinholeCamera.FromVerticalFov(400, 400, 30f);
var bin = new BinRegion(-0.4f, 0.4f, -0.4f, 0.4f, 1.5f, 2.1f);
var grasp = new GraspSettings { SearchStep = 3 };

float[] Scene(float floor) { var d = new float[400 * 400]; Array.Fill(d, floor); return d; }
void Box(float[] d, int x0, int y0, int x1, int y1, float z)
{
    for (int y = y0; y <= y1; y++) { for (int x = x0; x <= x1; x++) { d[y * 400 + x] = z; } }
}
var (noneFound, noneReport) = GraspPlanner.FindGrasps(pickCamera, Enumerable.Repeat(float.NaN, 160000).ToArray(), bin, grasp);
Check("Without measurements there is no pick point", noneFound.Count == 0 && noneReport.Evaluated == 0);

float[] patch = Scene(2.05f);
Box(patch, 150, 150, 250, 250, 1.8f);
var (patchFound, _) = GraspPlanner.FindGrasps(pickCamera, patch, bin, grasp);
GraspCandidate? top = patchFound.FirstOrDefault();
Check("A high surface is preferred over the floor", top != null && MathF.Abs(top.Z - 1.8f) < 0.002f);
Check("The pick point lies on the surface", top != null && top.PixelX is >= 150 and <= 250 && top.PixelY is >= 150 and <= 250);
Check("Perfect depth on that surface seals completely", top != null && SealVerifier.TrueSealShare(pickCamera, patch, top, grasp) > 0.99f);
Check("Candidates are at least one cup width apart", patchFound.Count > 1 && DistanceBetween(patchFound[0], patchFound[1]) >= 0.03f - 1e-4f);

float[] tinyPatch = Scene(2.05f);
Box(tinyPatch, 195, 195, 205, 205, 1.8f);
var (tinyFound, _) = GraspPlanner.FindGrasps(pickCamera, tinyPatch, bin, grasp);
Check("A surface smaller than the cup is not chosen", tinyFound.All(c => c.Z > 2f));

float[] holey = Scene(1.9f);
var holeRandom = new Random(3);
for (int i = 0; i < holey.Length; i++) { if (holeRandom.NextDouble() < 0.97) { holey[i] = float.NaN; } }
var (holeyFound, holeyReport) = GraspPlanner.FindGrasps(pickCamera, holey, bin, grasp);
Check("Too few measurements give no pick point", holeyFound.Count == 0 && holeyReport.TooFewPoints > 0);

var noisy = Scene(1.9f);
var noiseRandom = new Random(4);
for (int i = 0; i < noisy.Length; i++) { noisy[i] += (float)(noiseRandom.NextDouble() - 0.5) * 0.04f; }
var (noisyFound, noisyReport) = GraspPlanner.FindGrasps(pickCamera, noisy, bin, grasp);
Check("A rough surface gives no pick point", noisyFound.Count == 0 && noisyReport.NotFlat > 0);

float[] steep = new float[400 * 400];
for (int y = 0; y < 400; y++) { for (int x = 0; x < 400; x++) { steep[y * 400 + x] = 1.9f / (1f - 1.5f * (x + 0.5f - pickCamera.Cx) / pickCamera.Fx); } }
var (steepFound, steepReport) = GraspPlanner.FindGrasps(pickCamera, steep, bin, grasp);
Check("A surface that is too tilted is rejected", steepFound.Count == 0 && steepReport.TooTilted > 0);

float[] walled = Scene(2.05f);
Box(walled, 150, 150, 250, 250, 1.9f);
Box(walled, 196, 150, 204, 250, 1.8f);
var (walledFound, walledReport) = GraspPlanner.FindGrasps(pickCamera, walled, bin, new GraspSettings { SearchStep = 3, ToolDiameter = 0.09f, MaxCenterOffset = 1f });
Check("The holder must not hit a higher part", walledReport.ToolCollision > 0 && walledFound.All(c => c.Z < 1.85f || MathF.Abs(c.X) > 0.04f));

float[] edge = Scene(1.9f);
var smallBin = bin with { MinX = -0.02f, MaxX = 0.02f, MinY = -0.02f, MaxY = 0.02f };
var (edgeFound, edgeReport) = GraspPlanner.FindGrasps(pickCamera, edge, smallBin, grasp);
Check("Too close to the wall is rejected", edgeFound.Count == 0 && edgeReport.OutsideOpening > 0);

// The evaluator judges one location and names the first requirement that fails.
GraspEvaluator Evaluator(float[] d, BinRegion region) => new(pickCamera, d, region, grasp);
float[] flat = Scene(1.9f);
GraspEvaluation acceptedHere = Evaluator(flat, bin).Evaluate(200, 200);
Check("The evaluator accepts a flat surface", acceptedHere.IsAccepted && acceptedHere.Rejection == GraspRejection.None);
Check("The evaluator names too few points", Evaluator(holey, bin).Evaluate(FirstMeasured(holey), 200).Rejection == GraspRejection.TooFewPoints);
Check("The evaluator names a rough surface", Evaluator(noisy, bin).Evaluate(200, 200).Rejection == GraspRejection.NotFlat);
Check("The evaluator names a tilted surface", Evaluator(steep, bin).Evaluate(200, 200).Rejection == GraspRejection.TooTilted);
Check("The evaluator names the bin opening", Evaluator(flat, smallBin).Evaluate(200, 200).Rejection == GraspRejection.OutsideOpening);
Check("A floor below the bin floor is outside the bin", !Evaluator(Scene(2.2f), bin).IsInsideBin(200, 200));

// A surface that is large for the cup: the middle is accepted and the edge is off-centre.
float[] offCentre = Scene(2.05f);
Box(offCentre, 170, 170, 230, 230, 1.8f);
var centreSettings = new GraspSettings { SearchStep = 3, SurfaceSearchRadius = 0.15f };
GraspEvaluation atMiddle = new GraspEvaluator(pickCamera, offCentre, bin, centreSettings).Evaluate(200, 200);
GraspEvaluation atEdge = new GraspEvaluator(pickCamera, offCentre, bin, centreSettings).Evaluate(222, 200);
Check("The middle of a surface is accepted", atMiddle.IsAccepted && atMiddle.Candidate!.CenterOffset < 0.003f);
Check("The evaluator names a location away from the middle", atEdge.Rejection == GraspRejection.OffCenter);

// Suction cup and contact ring.
var cup = new SuctionCupSpecification { Diameter = 0.03f, VacuumPascal = 40_000f, Efficiency = 0.5f };
Check("Holding force is pressure x area x efficiency", MathF.Abs(cup.HoldingForce - 14.137f) < 0.01f);
float?[] fullRing = Enumerable.Repeat<float?>(0.001f, 32).ToArray();
float?[] brokenRing = Enumerable.Range(0, 32).Select(i => i < 8 ? (float?)null : 0.001f).ToArray();
float?[] liftedRing = Enumerable.Repeat<float?>(0.01f, 32).ToArray();
Check("A full ring seals", CupContact.Seals(fullRing, cup));
Check("A ring with a gap does not seal", !CupContact.Seals(brokenRing, cup));
Check("A ring above the surface does not seal", !CupContact.Seals(liftedRing, cup));
Check("An empty ring does not seal", !CupContact.Seals(Array.Empty<float?>(), cup));

var pickStats = new PickStatistics();
pickStats.Add(new PickResult(PickOutcome.Placed, 1f, 0, 2, 0.001f));
pickStats.Add(new PickResult(PickOutcome.NoSeal, 0.5f, 3, 4, 0.01f));
Check("Statistics count outcomes", pickStats.Attempts == 2 && pickStats.Placed == 1 && pickStats.Count(PickOutcome.NoSeal) == 1);
Check("Statistics give the success rate", Math.Abs(pickStats.SuccessRate - 0.5) < 1e-9 && pickStats.WithPulledAlong == 1 && Math.Abs(pickStats.AverageDisturbed - 3.0) < 1e-9);

var splitStats = DepthSensorStatistics.Compute(shadowed, scene, i => i % 2 == 0);
Check("Statistics split all pixels into parts and others",
    splitStats.PartPixels + splitStats.OtherPixels == scene.Length && splitStats.PartMissing <= splitStats.PartPixels);

Console.WriteLine(failures == 0 ? "RESULT: PASSED" : $"RESULT: {failures} TESTS FAILED");
return failures == 0 ? 0 : 1;

int FirstMeasured(float[] d)
{
    for (int x = 100; x < 300; x++)
    {
        if (!float.IsNaN(d[200 * 400 + x]))
        {
            return x;
        }
    }
    throw new InvalidOperationException("No measurement in this row.");
}

float DistanceBetween(GraspCandidate a, GraspCandidate b) =>
    MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));

void Check(string name, bool passed)
{
    Console.WriteLine($"{(passed ? "OK  " : "FAIL")} {name}");
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
            // Plane z = z0 + s * X, so z = z0 / (1 - s * (x - cx) / fx).
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
    // What the projector sees: the block shifts left in the image, more than the floor.
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
