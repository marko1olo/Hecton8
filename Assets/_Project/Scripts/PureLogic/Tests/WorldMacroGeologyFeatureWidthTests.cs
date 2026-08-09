using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Measures the HORIZONTAL width of the shelf break and the ridge flank against the widths the
    /// parameter block authors, because slope is amplitude divided by width and only the amplitude
    /// half is currently wired.
    ///
    /// WorldMacroGeologyParams declares four width parameters and EvaluateHeightMeters reads none of
    /// them:
    ///   ShelfBreakWidthMeters = 5200  declared :32, defaulted :52, clamped :260, NEVER READ
    ///   RidgeWidthMeters      = 2350  declared :34, defaulted :54, clamped :262, NEVER READ
    ///   TrenchWidthMeters     = 2200  declared :36, defaulted :56, clamped :264, NEVER READ
    /// against every height parameter, all of which ARE read:
    ///   AbyssDepthMeters / ShelfDepthMeters :764, RidgeHeightMeters :856,
    ///   TrenchDepthMeters :865, BasinDepthMeters :909, HadalDepthMeters :1342.
    ///
    /// So each feature's vertical size is authored and its horizontal size is an accident of whatever
    /// frequency multiplier was typed into the noise call. That is the same "parameter accepted then
    /// ignored" class this file documents at :753-757 for ShelfDepthMeters, which was found the same
    /// way and fixed by reading the parameter.
    ///
    /// Consequence, measured 2026-08-09 by WorldMacroGeologyStageSlopeSweepTests: stage 1 (the base
    /// shelf/abyss lerp, before any feature) already reads 27.6 deg mean slope at P1_origin, 30.0 at
    /// P4_far and 27.1 at P5_deepfar over their 200 m windows, against 1.4 deg at P3_west which sits
    /// deep inside the abyssal plain away from any transition. Stage 3 (+ridges) then adds a further
    /// 8.1 deg at P1 and 12.3 deg at P5. Stages 5 through 8 together move the mean by less than half
    /// a degree, so the steepness is entirely in the base field and the ridge, not in the detail
    /// terms - which is the opposite of where amplitude constants suggest looking.
    ///
    /// The transect method: walk a straight line, find where the mask crosses 0.1 and 0.9, and report
    /// the distance between those crossings. Measured on masks rather than on height so the number is
    /// the feature's own width and not the sum of everything overlapping it.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyFeatureWidthTests
    {
        private const uint Seed = 880031u;

        /// <summary>25 m sampling pitch over 40 km: fine enough to resolve a 500 m transition, coarse
        /// enough that a transect is 1600 height evaluations rather than 40000.</summary>
        private const double TransectStepMeters = 25.0;
        private const double TransectLengthMeters = 40000.0;

        private struct WidthStat
        {
            public int Crossings;
            public double MedianWidth;
            public double MinWidth;
            public double SteepestGradient;
            public double SteepestDegrees;
        }

        /// <summary>
        /// Walks a transect and measures every 0.1 -> 0.9 (or 0.9 -> 0.1) run of the selected mask,
        /// returning the median width of those runs. Median rather than mean because a transect
        /// clipping the corner of a feature produces one artificially wide run that a mean would let
        /// dominate.
        /// </summary>
        private static WidthStat MeasureMaskWidth(
            double startX, double startZ, double dirX, double dirZ, int maskIndex)
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            int steps = (int)(TransectLengthMeters / TransectStepMeters);

            var values = new float[steps];
            var heights = new float[steps];
            double len = math.sqrt(dirX * dirX + dirZ * dirZ);
            dirX /= len;
            dirZ /= len;

            for (int i = 0; i < steps; i++)
            {
                double d = i * TransectStepMeters;
                double x = startX + dirX * d;
                double z = startZ + dirZ * d;
                heights[i] = WorldMacroGeologyFields.EvaluateHeightMeters(
                    x, z, in p, out WorldMacroGeologyFields.MacroMasks m);
                values[i] = maskIndex == 0 ? m.Shelf : (maskIndex == 1 ? m.Ridge : m.Trench);
            }

            var widths = new System.Collections.Generic.List<double>();
            int runStart = -1;
            int direction = 0;

            for (int i = 1; i < steps; i++)
            {
                if (runStart < 0)
                {
                    if (values[i - 1] < 0.1f && values[i] >= 0.1f) { runStart = i; direction = 1; }
                    else if (values[i - 1] > 0.9f && values[i] <= 0.9f) { runStart = i; direction = -1; }
                    continue;
                }

                bool done = direction > 0 ? values[i] >= 0.9f : values[i] <= 0.1f;
                bool aborted = direction > 0 ? values[i] < 0.1f : values[i] > 0.9f;

                if (done)
                {
                    widths.Add((i - runStart) * TransectStepMeters);
                    runStart = -1;
                }
                else if (aborted)
                {
                    runStart = -1;
                }
            }

            double steepest = 0.0;
            for (int i = 1; i < steps; i++)
            {
                double g = math.abs(heights[i] - heights[i - 1]) / TransectStepMeters;
                if (g > steepest) steepest = g;
            }

            widths.Sort();
            return new WidthStat
            {
                Crossings = widths.Count,
                MedianWidth = widths.Count == 0 ? 0.0 : widths[widths.Count / 2],
                MinWidth = widths.Count == 0 ? 0.0 : widths[0],
                SteepestGradient = steepest,
                SteepestDegrees = math.degrees(math.atan(steepest))
            };
        }

        /// <summary>
        /// Reports measured widths against the authored ones. No assertion - the table is the point,
        /// and the assertion that matters is in the test below it.
        /// </summary>
        [Test]
        public void FeatureWidths_AreReportedAgainstAuthoredValues()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Measured 0.1->0.9 mask transition width vs authored parameter:");
            sb.AppendLine($"  authored ShelfBreakWidthMeters = {p.ShelfBreakWidthMeters:0}");
            sb.AppendLine($"  authored RidgeWidthMeters      = {p.RidgeWidthMeters:0}");
            sb.AppendLine($"  authored TrenchWidthMeters     = {p.TrenchWidthMeters:0}");
            sb.AppendLine();

            (double X, double Z, double DX, double DZ, string Label)[] transects =
            {
                (0.0, 5000.0, 1.0, 0.0, "east from origin"),
                (0.0, 5000.0, 0.0, 1.0, "north from origin"),
                (280000.0, 90000.0, 1.0, 0.3, "P4_far diagonal"),
                (760000.0, -333000.0, 1.0, 0.0, "P5_deepfar east")
            };

            string[] maskNames = { "Shelf", "Ridge", "Trench" };
            for (int t = 0; t < transects.Length; t++)
            {
                for (int m = 0; m < 3; m++)
                {
                    WidthStat s = MeasureMaskWidth(
                        transects[t].X, transects[t].Z, transects[t].DX, transects[t].DZ, m);
                    sb.AppendLine(
                        $"  {transects[t].Label,-20} {maskNames[m],-8} " +
                        $"runs={s.Crossings,3} median={s.MedianWidth,7:0}m min={s.MinWidth,7:0}m  " +
                        $"steepest height gradient on transect={s.SteepestDegrees,5:0.0}deg");
                }
            }

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>
        /// The shelf break must not be narrower than a fraction of its authored width. The lerp it
        /// drives spans AbyssDepthMeters - ShelfDepthMeters = 2860 m by default, so a break compressed
        /// to a quarter of 5200 m puts a 2.86 km drop into 1.3 km of ground - a 65 degree wall where
        /// the parameter block asks for 29 degrees.
        ///
        /// The bar is a QUARTER of the authored width, not the authored width itself, because the
        /// transition is noise-driven and legitimately varies along its length. A measurement below a
        /// quarter means the authored figure is not participating at all.
        /// </summary>
        [Test]
        public void ShelfBreak_IsNotNarrowerThanAQuarterOfItsAuthoredWidth()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            double floor = p.ShelfBreakWidthMeters * 0.25;

            WidthStat east = MeasureMaskWidth(0.0, 5000.0, 1.0, 0.0, 0);
            WidthStat north = MeasureMaskWidth(0.0, 5000.0, 0.0, 1.0, 0);

            Assert.That(
                east.Crossings + north.Crossings,
                Is.GreaterThan(0),
                "No shelf-mask transition found on either transect, so this test measured nothing. " +
                "Either the shelf mask is constant across 40 km (a defect in itself) or the transect " +
                "start point is badly chosen.");

            double widest = math.max(east.MedianWidth, north.MedianWidth);

            Assert.That(
                widest,
                Is.GreaterThan(floor),
                $"Shelf break transitions over {widest:0} m at its widest measured median, against an " +
                $"authored ShelfBreakWidthMeters of {p.ShelfBreakWidthMeters:0} m " +
                $"(floor for this test: {floor:0} m). The parameter is declared at " +
                "WorldMacroGeologyFields.cs:32, defaulted at :52 and clamped at :260, but " +
                "EvaluateHeightMeters never reads it - so the width of the break is set by the " +
                "hardcoded frequency in the continentField noise call at :736 instead. The depth lerp " +
                $"it drives at :764 spans {p.AbyssDepthMeters - p.ShelfDepthMeters:0} m.");
        }
    }
}
