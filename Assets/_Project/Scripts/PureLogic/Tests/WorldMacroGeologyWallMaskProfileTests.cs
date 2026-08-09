using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Prints the component masks along a transect through the steepest point in the world, so the
    /// wall is attributed to a named mask instead of to a stage.
    ///
    /// What is already established. WorldMacroGeologyWallHunterTests scanned 20 km x 20 km around
    /// three regions at a 250 m pitch and found the steepest points at 77.8-79.5 deg. The per-stage
    /// height profile through those points puts the wall in stage 3 (+ridges) every time: at
    /// P5_deepfar the profile span goes 28 m -> 721 m across that one stage, a 692 m rise over 250 m
    /// of ground, and stages 5 through 8 together move it by 13 m.
    ///
    /// What is NOT yet established, and is the point of this fixture: stage 3 is two lines, and both
    /// are scaled by `ridgeMask`.
    ///     :868  depth -= billowMountains * RidgeHeightMeters * 0.65 * ridgeMask * oceanicRidgeGate
    ///     :869  depth -= ridgeMask * RidgeHeightMeters * (0.58 + plateEdgeMask * 0.42) * oceanicRidgeGate
    /// Together they place up to 2558 m of amplitude wherever ridgeMask goes from 0 to 1, so the
    /// width of the ridgeMask transition IS the slope of the ridge flank, and nothing else in stage 3
    /// can be blamed until that width is measured at the wall rather than on average.
    ///
    /// The averages are not the problem: WorldMacroGeologyFeatureWidthTests measured Ridge mask
    /// transitions of 2875-5625 m median along four transects, which for 2558 m of amplitude is
    /// 24-42 deg. The minimum on those same transects was 1425 m. This fixture asks what the width is
    /// at the extreme the scanner found, because a mask whose transition width varies by 4x produces
    /// terrain whose slope varies by 4x, and it is the narrow tail that builds walls.
    ///
    /// ridgeMask is composed at :865 as
    ///     saturate(smoothstep(0.38, 0.86, ridgeBelt) * (1 - shelfMask * 0.42) + plateRidgeMask * 0.95)
    /// so there are two independent paths to 1.0 and the sum saturates. MacroMasks exposes Ridge and
    /// PlateEdge, which is enough to tell those two paths apart.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyWallMaskProfileTests
    {
        private const uint Seed = 880031u;

        /// <summary>The steepest points found by WorldMacroGeologyWallHunterTests, 2026-08-09.</summary>
        private static readonly (double X, double Z, string Label)[] WallPoints =
        {
            (7500.0, 250.0, "P1 wall (79.1 deg)"),
            (-43000.0, 11500.0, "P3 wall (77.8 deg)"),
            (773000.0, -325750.0, "P5 wall (79.5 deg)")
        };

        [Test]
        public void MaskProfilesThroughTheWall_AreReported()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Mask values along a 500 m transect through each wall, sampled every 25 m.");
            sb.AppendLine("ridgeMask (:865) = saturate(smoothstep(0.38,0.86,ridgeBelt)*(1-shelf*0.42) + plateRidge*0.95)");
            sb.AppendLine();

            foreach (var wp in WallPoints)
            {
                sb.AppendLine($"{wp.Label} at ({wp.X:0}, {wp.Z:0})");
                sb.AppendLine("   offset   height     Ridge  PlateEdge     Shelf    Trench     Basin");

                float previousRidge = float.NaN;
                float maxRidgeStep = 0f;
                float minRidge = 1f, maxRidge = 0f;
                float minPlate = 1f, maxPlate = 0f;

                for (int i = -10; i <= 10; i++)
                {
                    double x = wp.X + i * 25.0;
                    float h = WorldMacroGeologyFields.EvaluateHeightMeters(
                        x, wp.Z, in p, out WorldMacroGeologyFields.MacroMasks m);

                    sb.AppendLine(
                        $"  {i * 25,6}m {h,8:0}m  {m.Ridge,8:0.000}  {m.PlateEdge,8:0.000}  " +
                        $"{m.Shelf,8:0.000}  {m.Trench,8:0.000}  {m.Basin,8:0.000}");

                    if (!float.IsNaN(previousRidge))
                        maxRidgeStep = math.max(maxRidgeStep, math.abs(m.Ridge - previousRidge));
                    previousRidge = m.Ridge;
                    minRidge = math.min(minRidge, m.Ridge);
                    maxRidge = math.max(maxRidge, m.Ridge);
                    minPlate = math.min(minPlate, m.PlateEdge);
                    maxPlate = math.max(maxPlate, m.PlateEdge);
                }

                float ridgeSwing = maxRidge - minRidge;
                float plateSwing = maxPlate - minPlate;

                // If ridgeMask swings by S over 500 m, the implied ridge-flank width for a full
                // 0->1 transition is 500/S metres, and the flank gradient is 2558 m / that width.
                double impliedWidth = ridgeSwing > 0.001f ? 500.0 / ridgeSwing : double.PositiveInfinity;
                double impliedDegrees = math.degrees(math.atan(2558.0 / impliedWidth));

                sb.AppendLine(
                    $"  ridgeMask swing over 500 m = {ridgeSwing:0.000} " +
                    $"(biggest 25 m step {maxRidgeStep:0.000}); plateEdge swing = {plateSwing:0.000}");
                sb.AppendLine(
                    $"  implied full 0->1 ridge width = {impliedWidth:0}m, which for the 2558 m of " +
                    $"stage-3 amplitude is a {impliedDegrees:0.0} deg flank");
                sb.AppendLine(
                    $"  authored RidgeWidthMeters = {p.RidgeWidthMeters:0}m " +
                    $"(a {math.degrees(math.atan(2558.0 / p.RidgeWidthMeters)):0.0} deg flank at that width)");
                sb.AppendLine();
            }

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }
    }
}
