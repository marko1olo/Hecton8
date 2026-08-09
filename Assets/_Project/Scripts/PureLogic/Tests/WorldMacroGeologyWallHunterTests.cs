using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Finds the steepest place in a region and reports the per-stage slope sweep AT THAT PLACE,
    /// instead of at a fixed probe coordinate.
    ///
    /// Why this fixture exists. Every earlier slope measurement in this suite sampled fixed sites,
    /// and fixed sites answer the wrong question when the defect is localised. Measured 2026-08-09
    /// with nine 200 m windows spread 1.5 km around P3_west - the site that reads a healthy 13.3 deg
    /// at its centre - the median relief:window ratio is 1.33, the best window is 0.13 and the WORST
    /// is 2.89, which is 578 m of relief inside a 200 m window. A 22x spread across 3 km means the
    /// world is not uniformly steep or uniformly gentle: it is flat floors separated by walls, and a
    /// probe at a fixed coordinate reports whichever one it happens to land on.
    ///
    /// Corroborating reading from WorldMacroGeologyFeatureWidthTests: every 40 km transect recorded a
    /// steepest height gradient of 74-77 deg at a 25 m sampling pitch, which is a ~108 m step between
    /// adjacent samples. Walls exist on every transect tried.
    ///
    /// So: hunt the wall, then ask which pipeline stage builds it. That is a different question from
    /// "what is the average slope", and only the first one can be answered by a fix.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyWallHunterTests
    {
        private const uint Seed = 880031u;

        /// <summary>Coarse scan pitch. 250 m over a 20 km region = 80x80 = 6400 probe points.</summary>
        private const double ScanPitchMeters = 250.0;
        private const double ScanExtentMeters = 20000.0;

        /// <summary>Probe distance for the scan gradient. 25 m matches the transect pitch that first
        /// recorded the 74-77 deg steps, so the scan is looking for the same thing that was seen.</summary>
        private const double ScanProbeMeters = 25.0;

        private static readonly (double X, double Z, string Label)[] Regions =
        {
            (5000.0, 5000.0, "around P1_origin"),
            (-40000.0, 15000.0, "around P3_west"),
            (777000.0, -333000.0, "around P5_deepfar")
        };

        private static (double X, double Z, double Degrees) FindSteepestPoint(
            double centerX, double centerZ)
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            int steps = (int)(ScanExtentMeters / ScanPitchMeters);
            double half = ScanExtentMeters * 0.5;

            double bestDeg = -1.0;
            double bestX = centerX;
            double bestZ = centerZ;

            for (int iz = 0; iz <= steps; iz++)
            {
                double z = centerZ - half + iz * ScanPitchMeters;
                for (int ix = 0; ix <= steps; ix++)
                {
                    double x = centerX - half + ix * ScanPitchMeters;

                    float w = WorldMacroGeologyFields.EvaluateHeightMeters(x - ScanProbeMeters, z, in p);
                    float e = WorldMacroGeologyFields.EvaluateHeightMeters(x + ScanProbeMeters, z, in p);
                    float s = WorldMacroGeologyFields.EvaluateHeightMeters(x, z - ScanProbeMeters, in p);
                    float n = WorldMacroGeologyFields.EvaluateHeightMeters(x, z + ScanProbeMeters, in p);

                    float dx = (e - w) / (float)(ScanProbeMeters * 2.0);
                    float dz = (n - s) / (float)(ScanProbeMeters * 2.0);
                    double deg = math.degrees(math.atan(math.sqrt(dx * dx + dz * dz)));

                    if (deg > bestDeg)
                    {
                        bestDeg = deg;
                        bestX = x;
                        bestZ = z;
                    }
                }
            }

            return (bestX, bestZ, bestDeg);
        }

        /// <summary>
        /// Height along a short transect through a point, at each pipeline stage. A wall shows as a
        /// step in this profile, and the stage at which the step first appears is the stage that
        /// builds it. Reported as height rather than slope because a step is easier to read as a
        /// number sequence than as a derivative.
        /// </summary>
        private static string ProfileThroughPoint(double x, double z, int stage)
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            var sb = new System.Text.StringBuilder();
            for (int i = -5; i <= 5; i++)
            {
                float h = WorldMacroGeologyFields.EvaluateHeightMeters(
                    x + i * 25.0, z, in p, out _, stage);
                sb.Append($"{h,8:0}");
            }
            return sb.ToString();
        }

        [Test]
        public void SteepestPlaces_AndTheStageThatBuildsThem_AreReported()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"Scanned {ScanExtentMeters / 1000.0:0} km x {ScanExtentMeters / 1000.0:0} km at " +
                $"{ScanPitchMeters:0} m pitch around each region, {ScanProbeMeters:0} m gradient probe.");
            sb.AppendLine();

            (int Stage, string Name)[] stages =
            {
                (1, "base shelf/abyss"),
                (2, "+continentRelief"),
                (3, "+ridges"),
                (4, "+trench/fault/basin"),
                (6, "+volcano/crater/river/lake/mesa/dune"),
                (7, "+strata"),
                (8, "+mesoFracture/gravel/talus"),
                (0, "FULL")
            };

            for (int r = 0; r < Regions.Length; r++)
            {
                var hit = FindSteepestPoint(Regions[r].X, Regions[r].Z);
                sb.AppendLine(
                    $"{Regions[r].Label}: steepest at ({hit.X:0}, {hit.Z:0}) = {hit.Degrees:0.0} deg");
                sb.AppendLine("  height profile, 25 m apart, centred on that point:");

                double previousSpan = 0.0;
                for (int k = 0; k < stages.Length; k++)
                {
                    string profile = ProfileThroughPoint(hit.X, hit.Z, stages[k].Stage);

                    WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
                    float lo = float.MaxValue, hi = float.MinValue;
                    float maxStep = 0f;
                    float prev = float.NaN;
                    for (int i = -5; i <= 5; i++)
                    {
                        float h = WorldMacroGeologyFields.EvaluateHeightMeters(
                            hit.X + i * 25.0, hit.Z, in p, out _, stages[k].Stage);
                        if (h < lo) lo = h;
                        if (h > hi) hi = h;
                        if (!float.IsNaN(prev)) maxStep = math.max(maxStep, math.abs(h - prev));
                        prev = h;
                    }

                    double span = hi - lo;
                    double delta = k == 0 ? 0.0 : span - previousSpan;
                    previousSpan = span;

                    sb.AppendLine(
                        $"  {stages[k].Stage}. {stages[k].Name,-38} span={span,7:0}m " +
                        $"(delta {delta,+7:0}m) biggest 25m step={maxStep,6:0}m");
                    sb.AppendLine($"      {profile}");
                }
                sb.AppendLine();
            }

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }
    }
}
