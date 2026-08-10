using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Splits the base field's slope between the three terms that make it, so the world's steepness
    /// has an address rather than a description.
    ///
    /// The stage sweep established that stage 1 - the base shelf/abyss/basin field, before any
    /// landform - is the largest single contributor to a world whose median cell measures 29.1
    /// degrees: 16.1 degrees of it, against +3.7 from ridges, +2.6 from trench/fault/basin and +0.1
    /// from everything in stages 5 through 9 combined. Stage 1 is three lines of arithmetic
    /// (WorldMacroGeologyFields.cs:930-935):
    ///
    ///   depth  = lerp(AbyssDepthMeters 2950, ShelfDepthMeters 90, shelfMask)   2860 m of range
    ///   depth += abyssPlainMask * BasinDepthMeters 620 * 0.35                   217 m
    ///   depth += shelfRoughness * shelfMask                                     +/-28 m at 1667 m
    ///
    /// Two of the three are governed by authored parameters, so they can be nulled from outside
    /// without touching the evaluator. The third is a hardcoded amplitude and falls out by
    /// subtraction. That is enough to say which one to go and change.
    ///
    /// Sanitize (WorldMacroGeologyFields.cs:258) guarantees AbyssDepthMeters >= ShelfDepthMeters + 500,
    /// so the shelf lerp cannot be nulled completely; the narrowest it can be made is 500 m against
    /// its authored 2860. The row is labelled with what it actually is rather than pretending to be
    /// a null.
    ///
    /// MEASURED 2026-08-10, median over a 300 km square:
    ///
    ///     baseline (as authored)                     21.4
    ///     shelf lerp 2860 m -> 500 m                  5.1   -16.2
    ///     basin term 217 m -> 0                      20.5    -0.9
    ///     both reduced                                4.5   -16.9
    ///     shelf break width x4 (5200 -> 20800 m)     16.5    -4.9
    ///
    /// The shelf lerp IS the base field's slope. The basin term is negligible at 0.9 degrees, and the
    /// residual 4.5 is the hardcoded roughness plus the 500 m the Sanitize floor will not give up.
    ///
    /// The comparison that decides the fix: reducing the DROP buys 16.2 degrees, while quadrupling the
    /// WIDTH the same drop is spread over buys 4.9. Amplitude outweighs width by more than three to
    /// one, which is why the width work landed earlier this session moved the world so little. There
    /// is 2860 m of vertical range being delivered through one smoothstep, and no width setting makes
    /// that gentle.
    ///
    /// NOTE ON TWO DIFFERENT STAGE-1 NUMBERS. This fixture reports 21.4 as the MEDIAN CELL over a
    /// 300 km square; the structure function reports 16.1 as the MEAN GRADIENT at the origin. They are
    /// different statistics over different areas and do not contradict each other. Quoting either as
    /// "stage 1 is N degrees" without saying which is how a reader ends up believing the numbers moved
    /// when nothing did.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyBaseTermAttributionTests
    {
        private const uint Seed = (uint)WorldMacroGeologyFields.DefaultAuthoringSeed;

        /// <summary>
        /// Stage 1 only. The point is to attribute the BASE field's slope; running the full pipeline
        /// would fold in the ridge and trench terms that the stage sweep has already separated.
        /// </summary>
        private const int BaseStage = 1;

        private const int CellsPerAxis = 40;
        private const double SweepSpanMeters = 300000.0;
        private const double ProbeOffsetMeters = 20.0;

        private static double MedianSlopeDegrees(in WorldMacroGeologyParams p)
        {
            var slopes = new double[CellsPerAxis * CellsPerAxis];
            double step = SweepSpanMeters / (CellsPerAxis - 1);
            double half = SweepSpanMeters * 0.5;
            int k = 0;

            for (int iz = 0; iz < CellsPerAxis; iz++)
            {
                double z = -half + iz * step;
                for (int ix = 0; ix < CellsPerAxis; ix++)
                {
                    double x = -half + ix * step;
                    float w = Sample(x - ProbeOffsetMeters, z, in p);
                    float e = Sample(x + ProbeOffsetMeters, z, in p);
                    float s = Sample(x, z - ProbeOffsetMeters, in p);
                    float n = Sample(x, z + ProbeOffsetMeters, in p);
                    float dx = (e - w) / (float)(ProbeOffsetMeters * 2.0);
                    float dz = (n - s) / (float)(ProbeOffsetMeters * 2.0);
                    slopes[k++] = math.degrees(math.atan(math.sqrt(dx * dx + dz * dz)));
                }
            }

            System.Array.Sort(slopes);
            return slopes[slopes.Length / 2];
        }

        private static float Sample(double x, double z, in WorldMacroGeologyParams p)
        {
            return WorldMacroGeologyFields.EvaluateHeightMeters(
                x, z, in p, out WorldMacroGeologyFields.MacroMasks _, BaseStage);
        }

        [Test]
        public void BaseFieldSlope_IsAttributedToItsTerms()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"Median slope of the BASE field (stage 1) over a {SweepSpanMeters / 1000.0:0} km square, " +
                $"{CellsPerAxis}x{CellsPerAxis} cells, as each authored term is reduced.");
            sb.AppendLine();
            sb.AppendLine($"    {"variant",-46}{"median",9}{"delta",9}");

            WorldMacroGeologyParams baseline = WorldMacroGeologyParams.CreateDefault(Seed);
            double baselineMedian = MedianSlopeDegrees(in baseline);
            sb.AppendLine($"    {"baseline (as authored)",-46}{baselineMedian,8:0.0}{"",9}");

            // Shelf lerp: 2860 m of range. Sanitize floors Abyss at Shelf + 500, so this is the
            // narrowest the term can be made, not a null.
            WorldMacroGeologyParams narrowLerp = WorldMacroGeologyParams.CreateDefault(Seed);
            narrowLerp.AbyssDepthMeters = narrowLerp.ShelfDepthMeters + 500f;
            double narrowLerpMedian = MedianSlopeDegrees(in narrowLerp);
            sb.AppendLine($"    {"shelf lerp 2860m -> 500m (Sanitize floor)",-46}" +
                          $"{narrowLerpMedian,8:0.0}{narrowLerpMedian - baselineMedian,9:+0.0;-0.0}");

            // Basin term: 620 * 0.35 = 217 m, fully nullable.
            WorldMacroGeologyParams noBasin = WorldMacroGeologyParams.CreateDefault(Seed);
            noBasin.BasinDepthMeters = 0f;
            double noBasinMedian = MedianSlopeDegrees(in noBasin);
            sb.AppendLine($"    {"basin term 217m -> 0",-46}" +
                          $"{noBasinMedian,8:0.0}{noBasinMedian - baselineMedian,9:+0.0;-0.0}");

            // Both, so the residual is the hardcoded roughness plus whatever the masks contribute.
            WorldMacroGeologyParams both = WorldMacroGeologyParams.CreateDefault(Seed);
            both.AbyssDepthMeters = both.ShelfDepthMeters + 500f;
            both.BasinDepthMeters = 0f;
            double bothMedian = MedianSlopeDegrees(in both);
            sb.AppendLine($"    {"both reduced",-46}" +
                          $"{bothMedian,8:0.0}{bothMedian - baselineMedian,9:+0.0;-0.0}");

            // Widening the shelf break spreads the same 2860 m over more ground.
            WorldMacroGeologyParams wide = WorldMacroGeologyParams.CreateDefault(Seed);
            wide.ShelfBreakWidthMeters *= 4f;
            double wideMedian = MedianSlopeDegrees(in wide);
            sb.AppendLine($"    {"shelf break width x4 (5200 -> 20800 m)",-46}" +
                          $"{wideMedian,8:0.0}{wideMedian - baselineMedian,9:+0.0;-0.0}");

            sb.AppendLine();
            sb.AppendLine($"    Residual after both authored terms are reduced: {bothMedian:0.0} deg.");
            sb.AppendLine("    That residual is the hardcoded shelfRoughness (+/-28 m at a 1667 m");
            sb.AppendLine("    wavelength, WorldMacroGeologyFields.cs:934) plus the 500 m the Sanitize");
            sb.AppendLine("    floor will not let the lerp give up. Whatever it is, it is the part that");
            sb.AppendLine("    no authored parameter can currently reach.");

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }
    }
}
