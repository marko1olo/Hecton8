using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Proves that the authored width parameters now GOVERN terrain slope, by varying them and
    /// measuring the response, and quantifies how much lever each one gives.
    ///
    /// Why this is the test that matters. Three width parameters were declared, defaulted, clamped in
    /// Sanitize and never read by EvaluateHeightMeters, so the authored aspect ratio of every feature
    /// was decorative. They are wired now - but "wired" is a claim about code, and a call site can be
    /// present and still be inert. Measured on 2026-08-10: TrenchWidthMeters was wired through the
    /// same helper as the others and changed P5_deepfar's 1 km mean slope by EXACTLY zero, because
    /// `saturate(beltGate + plateTrenchMask * 1.15)` was already pinned by the plate term and the gate
    /// could not be seen at all. A parameter that is read but cannot affect the output is not fixed.
    ///
    /// So this fixture does not inspect code. It changes the number and watches the terrain.
    ///
    /// It also answers the question the slope work runs into at the end. After the shelf, ridge and
    /// trench widths were wired, P5_deepfar still measures 46.9 deg mean over a 1 km window. Its base
    /// field alone - the shelf lerp, before any feature - measures 36.1 deg, and the arithmetic says
    /// that is correct rather than broken: the lerp spans AbyssDepthMeters - ShelfDepthMeters = 2860 m,
    /// smoothstep's peak derivative is 1.5x its average, and 1.5 * 2860 / 5200 = 0.825, which is
    /// 39.5 deg. The seafloor is steep because 2860 m of drop across 5200 m of ground IS steep. That is
    /// an authored decision in the depth constants, not a defect in the evaluator, and this test makes
    /// the trade visible so it can be decided with numbers.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyWidthParameterAuthorityTests
    {
        private const uint Seed = 880031u;
        private const int SamplesPerAxis = 40;

        private static readonly (double X, double Z, string Label)[] Sites =
        {
            (5000.0, 5000.0, "P1_origin"),
            (50000.0, 50000.0, "P2_near"),
            (300000.0, 90000.0, "P4_far"),
            (777000.0, -333000.0, "P5_deepfar")
        };

        private static double MeanSlopeDegrees(
            double centerX, double centerZ, double windowMeters, in WorldMacroGeologyParams p)
        {
            const double probe = 12.0;
            double half = windowMeters * 0.5;
            double step = windowMeters / (SamplesPerAxis - 1);
            double sum = 0.0;
            int count = 0;

            for (int iz = 0; iz < SamplesPerAxis; iz++)
            {
                double z = centerZ - half + iz * step;
                for (int ix = 0; ix < SamplesPerAxis; ix++)
                {
                    double x = centerX - half + ix * step;
                    float w = WorldMacroGeologyFields.EvaluateHeightMeters(x - probe, z, in p);
                    float e = WorldMacroGeologyFields.EvaluateHeightMeters(x + probe, z, in p);
                    float s = WorldMacroGeologyFields.EvaluateHeightMeters(x, z - probe, in p);
                    float n = WorldMacroGeologyFields.EvaluateHeightMeters(x, z + probe, in p);
                    float dx = (e - w) / (float)(probe * 2.0);
                    float dz = (n - s) / (float)(probe * 2.0);
                    sum += math.degrees(math.atan(math.sqrt(dx * dx + dz * dz)));
                    count++;
                }
            }

            return sum / count;
        }

        [Test]
        public void WidthParameters_MoveTheSlopeBudget()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Mean slope (deg) over a 1 km window as each authored width is scaled.");
            sb.AppendLine("Baseline is CreateDefault: shelf 5200 m, ridge 2350 m, trench 2200 m.");
            sb.AppendLine();
            sb.Append("  variant".PadRight(34));
            foreach (var s in Sites) sb.Append(s.Label.PadLeft(13));
            sb.AppendLine();

            (string Label, float ShelfScale, float RidgeScale, float TrenchScale)[] variants =
            {
                ("baseline", 1f, 1f, 1f),
                ("shelf x2 (10400 m)", 2f, 1f, 1f),
                ("shelf x4 (20800 m)", 4f, 1f, 1f),
                ("ridge x2 (4700 m)", 1f, 2f, 1f),
                ("trench x2 (4400 m)", 1f, 1f, 2f),
                ("all x2", 2f, 2f, 2f),
                ("shelf x0.5 (2600 m)", 0.5f, 1f, 1f)
            };

            var baseline = new double[Sites.Length];
            for (int v = 0; v < variants.Length; v++)
            {
                WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
                p.ShelfBreakWidthMeters *= variants[v].ShelfScale;
                p.RidgeWidthMeters *= variants[v].RidgeScale;
                p.TrenchWidthMeters *= variants[v].TrenchScale;

                sb.Append($"  {variants[v].Label}".PadRight(34));
                for (int i = 0; i < Sites.Length; i++)
                {
                    double mean = MeanSlopeDegrees(Sites[i].X, Sites[i].Z, 1000.0, in p);
                    if (v == 0) baseline[i] = mean;
                    string cell = v == 0
                        ? $"{mean,6:0.0}"
                        : $"{mean,6:0.0} ({mean - baseline[i],+5:0.0})";
                    sb.Append(cell.PadLeft(13));
                }
                sb.AppendLine();
            }

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>
        /// The shelf width must have real authority over the slope budget, because it drives the
        /// largest vertical move in the generator and because a parameter that is read but cannot
        /// change the output is indistinguishable from one that is ignored.
        ///
        /// Asserted at P5_deepfar, the steepest site, and only against a 4x widening - a change large
        /// enough that no plausible amount of noise could mask it. 3 degrees is a deliberately small
        /// bar for a 4x change; it is a liveness check on the wiring, not a quality target.
        /// </summary>
        [Test]
        public void ShelfBreakWidth_HasRealAuthorityOverSlope()
        {
            WorldMacroGeologyParams narrow = WorldMacroGeologyParams.CreateDefault(Seed);
            WorldMacroGeologyParams wide = WorldMacroGeologyParams.CreateDefault(Seed);
            wide.ShelfBreakWidthMeters *= 4f;

            double narrowMean = MeanSlopeDegrees(777000.0, -333000.0, 1000.0, in narrow);
            double wideMean = MeanSlopeDegrees(777000.0, -333000.0, 1000.0, in wide);

            Assert.That(
                narrowMean - wideMean,
                Is.GreaterThan(3.0),
                $"Widening ShelfBreakWidthMeters 4x (5200 -> 20800 m) moved P5_deepfar's mean slope " +
                $"only from {narrowMean:0.0} to {wideMean:0.0} deg. The parameter is read by " +
                "EvaluateHeightMeters but is not governing the terrain, which is the state " +
                "TrenchWidthMeters was found in on 2026-08-10: wired through the same helper, and " +
                "inert because saturate() was already pinned by another term.");
        }
    }
}
