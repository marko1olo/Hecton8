using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Resolves a contradiction between two measurements that cannot both be right, by finding out
    /// where the shelf mask actually spends its time.
    ///
    /// The contradiction, measured 2026-08-10 on the base field (stage 1) over a 300 km square:
    ///   reducing the shelf lerp from 2860 m to 500 m   ->  median 21.4 -> 5.1 deg  (-16.2)
    ///   widening the shelf break from 5200 m to 20800 m ->  median 21.4 -> 16.5 deg (-4.9)
    ///
    /// Both act on the same term. depth = lerp(AbyssDepth, ShelfDepth, shelfMask), so the gradient it
    /// contributes is (ShelfDepth - AbyssDepth) * grad(shelfMask). Shrinking the range by 5.7x and
    /// widening the transition by 4x should both cut that gradient by roughly their factor, and the
    /// effect on the median should scale together. It did not.
    ///
    /// Only two shapes of world explain the gap:
    ///
    ///   A. The mask is saturated at 0 or 1 across most of the world with thin transition bands. Then
    ///      widening the band cannot move the median, because the median cell is not in a band - but
    ///      neither could shrinking the range, so this fails to explain -16.2.
    ///
    ///   B. The mask is INTERMEDIATE across much of the world. Then the lerp contributes gradient
    ///      nearly everywhere and shrinking its range flattens everything, while widening the authored
    ///      band does much less because the mask's spatial variation is not actually being set by
    ///      ShelfBreakWidthMeters at all - the width only controls the gate near the isoline.
    ///
    /// B would mean the authored width parameter governs less of the world than the width work
    /// assumed, which matters because that work was landed on the strength of a 4x-widening
    /// measurement at five hand-picked sites.
    ///
    /// This fixture does not choose between them by argument. It histograms the mask and reports the
    /// slope conditioned on it.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyShelfMaskOccupancyTests
    {
        private const uint Seed = (uint)WorldMacroGeologyFields.DefaultAuthoringSeed;
        private const int BaseStage = 1;
        private const int CellsPerAxis = 56;
        private const double SweepSpanMeters = 300000.0;
        private const double ProbeOffsetMeters = 20.0;

        private static float BaseHeight(double x, double z, in WorldMacroGeologyParams p)
        {
            return WorldMacroGeologyFields.EvaluateHeightMeters(
                x, z, in p, out WorldMacroGeologyFields.MacroMasks _, BaseStage);
        }

        [Test]
        public void ShelfMaskOccupancy_AndConditionalSlope_AreReported()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);

            // Ten buckets on the mask, plus the slope accumulated in each.
            var bucketCount = new int[10];
            var bucketSlope = new double[10];
            int total = 0;
            double slopeInBand = 0.0;
            int inBand = 0;
            double slopeOutOfBand = 0.0;
            int outOfBand = 0;

            double step = SweepSpanMeters / (CellsPerAxis - 1);
            double half = SweepSpanMeters * 0.5;

            for (int iz = 0; iz < CellsPerAxis; iz++)
            {
                double z = -half + iz * step;
                for (int ix = 0; ix < CellsPerAxis; ix++)
                {
                    double x = -half + ix * step;

                    WorldMacroGeologyFields.EvaluateHeightMeters(
                        x, z, in p, out WorldMacroGeologyFields.MacroMasks m);

                    float w = BaseHeight(x - ProbeOffsetMeters, z, in p);
                    float e = BaseHeight(x + ProbeOffsetMeters, z, in p);
                    float s = BaseHeight(x, z - ProbeOffsetMeters, in p);
                    float n = BaseHeight(x, z + ProbeOffsetMeters, in p);
                    float dx = (e - w) / (float)(ProbeOffsetMeters * 2.0);
                    float dz = (n - s) / (float)(ProbeOffsetMeters * 2.0);
                    double slope = math.degrees(math.atan(math.sqrt(dx * dx + dz * dz)));

                    int b = (int)math.clamp(math.floor(m.Shelf * 10f), 0, 9);
                    bucketCount[b]++;
                    bucketSlope[b] += slope;
                    total++;

                    if (m.Shelf > 0.02f && m.Shelf < 0.98f) { slopeInBand += slope; inBand++; }
                    else { slopeOutOfBand += slope; outOfBand++; }
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"Shelf mask occupancy over a {SweepSpanMeters / 1000.0:0} km square " +
                $"({CellsPerAxis}x{CellsPerAxis} = {total} cells), with base-field slope per bucket.");
            sb.AppendLine();
            sb.AppendLine($"    {"mask range",-14}{"cells",9}{"share",9}{"mean slope",13}");

            for (int b = 0; b < 10; b++)
            {
                double share = 100.0 * bucketCount[b] / total;
                double mean = bucketCount[b] > 0 ? bucketSlope[b] / bucketCount[b] : 0.0;
                sb.AppendLine(
                    $"    {b / 10.0,4:0.0}-{(b + 1) / 10.0,-9:0.0}{bucketCount[b],9}{share,8:0.0}%" +
                    $"{mean,12:0.0}");
            }

            sb.AppendLine();
            sb.AppendLine(
                $"    in transition (0.02..0.98): {100.0 * inBand / total,5:0.0}% of cells, " +
                $"mean slope {(inBand > 0 ? slopeInBand / inBand : 0.0),5:0.0} deg");
            sb.AppendLine(
                $"    saturated at 0 or 1:        {100.0 * outOfBand / total,5:0.0}% of cells, " +
                $"mean slope {(outOfBand > 0 ? slopeOutOfBand / outOfBand : 0.0),5:0.0} deg");
            sb.AppendLine();
            sb.AppendLine("    If the saturated cells are ALSO steep, the lerp is not what makes them steep");
            sb.AppendLine("    and the -16.2 deg from shrinking the range came from somewhere else - which");
            sb.AppendLine("    would mean AbyssDepthMeters is reaching the slope by a path other than this");
            sb.AppendLine("    lerp's gradient.");

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }
    }
}
