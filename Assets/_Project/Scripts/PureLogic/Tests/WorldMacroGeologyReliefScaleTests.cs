using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Measures how the height field's relief grows with the size of the window it is measured in,
    /// which is the question that decides whether this world has macro structure at all.
    ///
    /// A world with real bathymetry spends its vertical range across its horizontal extent: a shelf,
    /// then a slope, then an abyssal plain, then a trench, each tens to hundreds of kilometres wide.
    /// Measured that way, relief keeps growing as the window grows - a 1 km window sees tens of metres,
    /// a 100 km window sees the whole range.
    ///
    /// A world that has spent its whole vertical range at kilometre scale looks completely different:
    /// relief SATURATES. A 3 km window already sees nearly the full range, and widening to 100 km adds
    /// almost nothing, because there is no larger structure left to find. Every site is then a few
    /// kilometres of wall, and the wall is the same wall everywhere.
    ///
    /// WHAT THIS ACTUALLY MEASURED, 2026-08-10. The fixture was written expecting saturation, because
    /// the clean room's 3 km grid drops 2036 m at the world origin, 3319 m at P4_far and 3482 m at
    /// P5_deepfar against an authored span of 4510 m. That expectation was WRONG and this test refuted
    /// it. Relief climbs properly:
    ///
    ///     window     origin   p2_near   p3_west   p4_far   p5_deepfar
    ///       1 km       0.26      0.07      0.07     0.19        0.19
    ///       3 km       0.49      0.18      0.45     0.70        0.51
    ///      10 km       0.82      0.75      0.83     0.80        0.83
    ///      30 km       0.92      1.00      0.95     1.00        0.99
    ///     100 km       1.00      1.00      0.99     1.02        1.01
    ///
    /// So the macro fields are not degenerate. What the table does say is that the full range arrives
    /// by 30 km, where Earth takes roughly 150-200 km to get from shelf to abyssal plain - the world is
    /// about five times horizontally compressed, which is a tuning judgement about scale and not a
    /// broken evaluator. P4_far and P5_deepfar reading 0.70 and 0.51 at 3 km is those two sites
    /// sitting on the steep part of that transition, which is where they were chosen from.
    ///
    /// The comment is left standing rather than deleted because the refuted hypothesis is the useful
    /// part: 'every site is a 3 km wall' is what the clean-room grid measurements look like if the
    /// distribution is never checked, and it is wrong.
    ///
    /// The ratio printed as 'frac' is the payload: relief at this window divided by relief at the
    /// widest window. A healthy world's fractions climb steadily. A saturated world's are already near
    /// 1.00 in the first few rows.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyReliefScaleTests
    {
        private const uint Seed = (uint)WorldMacroGeologyFields.DefaultAuthoringSeed;

        private static readonly (double X, double Z, string Label)[] Sites =
        {
            (0.0, 0.0, "origin"),
            (50000.0, 50000.0, "p2_near"),
            (-40000.0, 15000.0, "p3_west"),
            (300000.0, 90000.0, "p4_far"),
            (777000.0, -333000.0, "p5_deepfar")
        };

        private static readonly double[] WindowsMeters =
        {
            1000.0, 3000.0, 10000.0, 30000.0, 100000.0, 300000.0
        };

        /// <summary>
        /// Relief is measured with the SAME number of samples per axis at every window, so the sample
        /// count cannot explain the trend. That means the pitch coarsens as the window grows - a 41x41
        /// grid over 300 km samples every 7.5 km - which UNDER-reports relief at wide windows if
        /// anything, since it can step over a narrow trench. Under-reporting the wide windows biases
        /// the fractions UP, so a saturation verdict from this test is conservative.
        /// </summary>
        private const int SamplesPerAxis = 41;

        private static double ReliefMeters(double cx, double cz, double window, in WorldMacroGeologyParams p)
        {
            double half = window * 0.5;
            double step = window / (SamplesPerAxis - 1);
            float lo = float.MaxValue;
            float hi = float.MinValue;

            for (int iz = 0; iz < SamplesPerAxis; iz++)
            {
                double z = cz - half + iz * step;
                for (int ix = 0; ix < SamplesPerAxis; ix++)
                {
                    double x = cx - half + ix * step;
                    float h = WorldMacroGeologyFields.EvaluateHeightMeters(x, z, in p);
                    lo = math.min(lo, h);
                    hi = math.max(hi, h);
                }
            }

            return hi - lo;
        }

        [Test]
        public void ReliefVersusWindowSize_IsReported()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Relief (m) and its fraction of the 300 km relief, per window size.");
            sb.AppendLine("A world with macro structure climbs steadily. A saturated one starts near 1.00.");
            sb.AppendLine();
            sb.Append("  window".PadRight(12));
            foreach (var s in Sites) sb.Append(s.Label.PadLeft(20));
            sb.AppendLine();

            var relief = new double[Sites.Length, WindowsMeters.Length];
            for (int i = 0; i < Sites.Length; i++)
                for (int w = 0; w < WindowsMeters.Length; w++)
                    relief[i, w] = ReliefMeters(Sites[i].X, Sites[i].Z, WindowsMeters[w], in p);

            int widest = WindowsMeters.Length - 1;
            for (int w = 0; w < WindowsMeters.Length; w++)
            {
                sb.Append($"  {WindowsMeters[w] / 1000.0,5:0} km".PadRight(12));
                for (int i = 0; i < Sites.Length; i++)
                {
                    double frac = relief[i, w] / math.max(1e-3, relief[i, widest]);
                    sb.Append($"{relief[i, w],9:0}m {frac,6:0.00}".PadLeft(20));
                }
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("  Authored vertical constants for reference:");
            WorldMacroGeologyParams d = WorldMacroGeologyParams.CreateDefault(Seed);
            sb.AppendLine($"    ShelfDepth {d.ShelfDepthMeters:0}m  AbyssDepth {d.AbyssDepthMeters:0}m  " +
                          $"HadalDepth {d.HadalDepthMeters:0}m  RidgeHeight {d.RidgeHeightMeters:0}m  " +
                          $"TrenchDepth {d.TrenchDepthMeters:0}m  BasinDepth {d.BasinDepthMeters:0}m");
            sb.AppendLine($"    Full authored span, shelf to hadal: {d.HadalDepthMeters - d.ShelfDepthMeters:0}m");

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>
        /// Locks the property that distinguishes a world from a texture: widening the window by 100x
        /// must find substantially more relief than the narrow window did.
        ///
        /// The bar is that a 1 km window may not already contain half of the relief that a 100 km
        /// window contains. That is a deliberately weak bar - a real ocean basin would be nearer a
        /// tenth - and it is set weak on purpose so that failing it cannot be argued with.
        ///
        /// Asserted at every site rather than the worst one, because the failure this guards against is
        /// global by nature: it comes from the wavelengths chosen for the province and depth fields,
        /// which are the same wavelengths everywhere.
        /// </summary>
        [Test]
        public void MacroStructure_ExistsAboveKilometreScale()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            var failures = new System.Collections.Generic.List<string>();

            foreach (var site in Sites)
            {
                double near = ReliefMeters(site.X, site.Z, 1000.0, in p);
                double far = ReliefMeters(site.X, site.Z, 100000.0, in p);
                double frac = near / math.max(1e-3, far);

                if (frac > 0.5)
                {
                    failures.Add(
                        $"{site.Label}: a 1 km window already holds {near:0}m of the {far:0}m found in " +
                        $"100 km ({frac:0.00} of it)");
                }
            }

            Assert.That(
                failures,
                Is.Empty,
                "The height field has no structure above kilometre scale at these sites:\n  " +
                string.Join("\n  ", failures) +
                "\n\nWhen a 1 km window already contains most of the relief of a 100 km window, the " +
                "vertical range has been spent at kilometre wavelengths and there is no shelf, slope " +
                "or basin left to traverse - only the same few kilometres of wall repeated. This is " +
                "not fixable by tuning any single feature's width, because every feature is riding on " +
                "province and depth fields whose own wavelengths are too short.");
        }
    }
}
