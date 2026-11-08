using BinPicking.Core.Grasping;
using BinPicking.Core.Picking;
using BinPicking.Core.Sensing;
using BinPicking.Core.StructuredLight;

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

var artefactStats = new PickStatistics();
artefactStats.Add(new PickResult(PickOutcome.LostDuringLift, 1f, 0, 8, 0f, MovedBeforeLift: 6, LostBeforeLift: true));
artefactStats.Add(new PickResult(PickOutcome.Placed, 1f, 0, 1, 0f, MovedBeforeLift: 0));
Check("Statistics separate artefacts from real losses", artefactStats.LostBeforeLift == 1 && Math.Abs(artefactStats.AverageMovedBeforeLift - 3.0) < 1e-9);

var splitStats = DepthSensorStatistics.Compute(shadowed, scene, i => i % 2 == 0);
Check("Statistics split all pixels into parts and others",
    splitStats.PartPixels + splitStats.OtherPixels == scene.Length && splitStats.PartMissing <= splitStats.PartPixels);

// Structured light: a flat plate lit by the real patterns, decoded from the images alone.
var lightSettings = new StructuredLightSettings();
var lightCamera = PinholeCamera.FromVerticalFov(320, 240, 35f);
var projector = new ProjectorModel(lightSettings.Columns, MathF.Tan(35f * MathF.PI / 180f), 0.15f);
var patterns = new PatternGenerator(lightSettings);
var sensor = new CameraNoiseSettings();

Check("The default patterns take 11 images", lightSettings.ImageCount == 11);
Check("The stripes of neighbouring columns differ in at most one bit", Enumerable.Range(1, 127)
    .All(s => System.Numerics.BitOperations.PopCount((uint)(GrayOf(s) ^ GrayOf(s - 1))) == 1));

const float CleanLight = 1.5f;
DepthSensorResult clean = DecodePlate(2f, CleanLight, null);
Check("Without noise the plate is measured everywhere", MeasuredShare(clean) > 0.99);
Check($"Without noise the depth is correct within 1 mm (worst {WorstError(clean, 2f) * 1000f:F2} mm)", WorstError(clean, 2f) < 0.001f);
Check("Triangulation finds another distance too", WorstError(DecodePlate(1.2f, CleanLight, null), 1.2f) < 0.001f);

DepthSensorResult bright = DecodePlate(2f, 1.5f, sensor);
DepthSensorResult dark = DecodePlate(2f, 0.05f, sensor);
Check("A bright plate under noise is still measured everywhere", MeasuredShare(bright) > 0.99);
Check($"Black is noisier than white ({DepthError(dark, 2f) * 1000f:F1} mm against {DepthError(bright, 2f) * 1000f:F1} mm)",
    DepthError(dark, 2f) > 2.0 * DepthError(bright, 2f));
Check("Almost no light gives holes for low signal", DecodePlate(2f, 0.0005f, sensor).Missing.Count(m => m == MissingReason.LowSignal) > lightCamera.Width * lightCamera.Height / 2);
Check("Too much light gives holes for overexposure", DecodePlate(2f, 10f, sensor).Missing.Count(m => m == MissingReason.Overexposed) > lightCamera.Width * lightCamera.Height / 2);
Check("The same seed gives the same noise", CameraNoiseModel.Expose(new[] { 0.3f, 0.1f }, sensor, 5).SequenceEqual(CameraNoiseModel.Expose(new[] { 0.3f, 0.1f }, sensor, 5)));

float[] step = Enumerable.Range(0, lightCamera.Width * lightCamera.Height)
    .Select(i => i % lightCamera.Width < lightCamera.Width / 2 ? 1.8f : 2f).ToArray();
DepthSensorResult stepped = DecodeDepths(step, CleanLight, null);
Check("A depth step is measured on both sides", WorstErrorAgainst(stepped, step, 3) < 0.001f);

bool wrongCount = false;
try
{
    PatternDecoder.Decode(lightCamera, projector, lightSettings, new[] { new float[1] });
}
catch (ArgumentException)
{
    wrongCount = true;
}
Check("A wrong number of images throws", wrongCount);

// HDR: one short and one long exposure of the same plate.
var shortExposure = new CameraNoiseSettings { ElectronsPerLightUnit = 400f };
var longExposure = new CameraNoiseSettings { ElectronsPerLightUnit = 4000f };
DepthSensorResult brightShort = DecodePlate(2f, 10f, shortExposure);
DepthSensorResult brightLong = DecodePlate(2f, 10f, longExposure);
DepthSensorResult brightHdr = HdrCombiner.Combine(new[] { brightShort, brightLong });
Check("HDR measures a bright plate that the long exposure overexposes",
    MeasuredShare(brightLong) < 0.01 && MeasuredShare(brightHdr) > 0.99);
DepthSensorResult darkShort = DecodePlate(2f, 0.1f, shortExposure);
DepthSensorResult darkLong = DecodePlate(2f, 0.1f, longExposure);
DepthSensorResult darkHdr = HdrCombiner.Combine(new[] { darkShort, darkLong });
Check($"HDR takes the long exposure on a dark plate ({DepthError(darkHdr, 2f) * 1000f:F1} mm, short alone {DepthError(darkShort, 2f) * 1000f:F1} mm)",
    MeasuredShare(darkHdr) >= MeasuredShare(darkShort) && DepthError(darkHdr, 2f) <= DepthError(darkLong, 2f) + 1e-6);
DepthSensorResult nothing = HdrCombiner.Combine(new[] { DecodePlate(2f, 0.0005f, shortExposure), DecodePlate(2f, 0.0005f, longExposure) });
// A weak signal: plate light 0.1 with the short exposure. Count pixels that are more than 50 mm off.
float[] plate = Enumerable.Repeat(2f, lightCamera.Width * lightCamera.Height).ToArray();
int Wrong(DepthSensorResult r) => r.Depth.Count(d => !float.IsNaN(d) && MathF.Abs(d - 2f) > 0.05f);
DepthSensorResult darkChecked = DecodeDepths(plate, 0.1f, shortExposure, checkNoise: true);
Check($"With the noise check, a weak signal gives holes instead of wrong depth " +
      $"({Wrong(darkShort)} wrong pixels without the check, {Wrong(darkChecked)} with it)",
    Wrong(darkShort) > 100 && Wrong(darkChecked) == 0);
DepthSensorResult mediumChecked = DecodeDepths(plate, 0.4f, shortExposure, checkNoise: true);
Check($"At a moderate signal the check removes the wrong pixels and keeps most ({MeasuredShare(mediumChecked) * 100:F0}% measured, {Wrong(mediumChecked)} wrong)",
    Wrong(mediumChecked) == 0 && MeasuredShare(mediumChecked) > 0.9);
Check("The noise check keeps a well-lit plate", MeasuredShare(DecodeDepths(Enumerable.Repeat(2f, lightCamera.Width * lightCamera.Height).ToArray(), 1.5f, sensor, checkNoise: true)) > 0.99);
Check("Without light HDR reports low signal", nothing.Missing.Count(m => m == MissingReason.LowSignal) > lightCamera.Width * lightCamera.Height / 2);

Console.WriteLine(failures == 0 ? "RESULT: PASSED" : $"RESULT: {failures} TESTS FAILED");
return failures == 0 ? 0 : 1;

int GrayOf(int stripe) => stripe ^ (stripe >> 1);

DepthSensorResult DecodePlate(float z, float light, CameraNoiseSettings? noise) =>
    DecodeDepths(Enumerable.Repeat(z, lightCamera.Width * lightCamera.Height).ToArray(), light, noise);

// Renders the scene by hand: each pixel receives the pattern value of the column that lights its point.
DepthSensorResult DecodeDepths(float[] depths, float light, CameraNoiseSettings? noise, bool checkNoise = false)
{
    var images = new List<float[]>();
    for (int image = 0; image < lightSettings.ImageCount; image++)
    {
        var lit = new float[depths.Length];
        for (int i = 0; i < depths.Length; i++)
        {
            (float x, _) = lightCamera.Unproject(i % lightCamera.Width, i / lightCamera.Width, depths[i]);
            lit[i] = light * patterns.Value(image, projector.PositionOf(x, depths[i]));
        }
        images.Add(noise == null
            ? lit.Select(v => MathF.Min(v / 2f, 1f)).ToArray()
            : CameraNoiseModel.Expose(lit, noise, 100 + image));
    }
    return PatternDecoder.Decode(lightCamera, projector, lightSettings, images, checkNoise ? noise : null);
}

double MeasuredShare(DepthSensorResult result) => result.Depth.Count(d => !float.IsNaN(d)) / (double)result.Depth.Length;

float WorstError(DepthSensorResult result, float z) => result.Depth.Where(d => !float.IsNaN(d)).Max(d => MathF.Abs(d - z));

// Ignores pixels right next to the depth step.
float WorstErrorAgainst(DepthSensorResult result, float[] truth, int margin)
{
    float worst = 0f;
    int width = lightCamera.Width;
    for (int i = 0; i < truth.Length; i++)
    {
        if (Math.Abs(i % width - width / 2) > margin && !float.IsNaN(result.Depth[i]))
        {
            worst = MathF.Max(worst, MathF.Abs(result.Depth[i] - truth[i]));
        }
    }
    return worst;
}

double DepthError(DepthSensorResult result, float z)
{
    float[] measured = result.Depth.Where(d => !float.IsNaN(d)).ToArray();
    return Math.Sqrt(measured.Average(d => (double)((d - z) * (d - z))));
}

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
