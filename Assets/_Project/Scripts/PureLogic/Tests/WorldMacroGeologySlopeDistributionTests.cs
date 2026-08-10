using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Measures the DISTRIBUTION of terrain steepness across a large area, instead of the mean at a
    /// handful of chosen sites.
    ///
    /// Why the site-based numbers were not enough. Every slope figure quoted in this suite so far -
    /// P1 25.9, P2 19.1, P3 12.7, P4 33.7, P5 46.9 degrees - is a mean over one 1 km window at one
    /// hand-picked coordinate. On 2026-08-10 two 1 km windows barely a kilometre apart at the world
    /// origin were measured at 1313 m and 304 m of relief. A four-fold difference between neighbours
    /// means a single window is not a sample of the world, it is a sample of one landform, and five of
    /// them cannot say what fraction of the seafloor a diver can cross.
    ///
    /// That is the number this fixture produces. It sweeps a wide area on a coarse lattice, measures
    /// each cell's mean slope, and reports the histogram plus the percentiles. The gameplay question -
    /// how much of this world is traversable - is a property of the distribution, and only the
    /// distribution can answer it.
    ///
    /// It also states the sampling honestly. Cells are measured at a 40 m probe pitch, which resolves
    /// the macro fields and the large mesoscale but not scree or boulders; a cell reported at 20
    /// degrees can still be locally impassable at metre scale. This measures the shape of the seafloor,
    /// not the roughness a diver's collider meets, and those are different questions.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologySlopeDistributionTests
    {
        private const uint Seed = (uint)WorldMacroGeologyFields.DefaultAuthoringSeed;

        /// <summary>
        /// A 400 km square centred on the origin, sampled on a 60x60 lattice of cells - so cells sit
        /// about 6.8 km apart and each is measured independently. The sweep is wide rather than dense
        /// on purpose: the previous work already knows what one square kilometre looks like in fine
        /// detail, and what it does not know is how that kilometre compares to the rest of the world.
        /// </summary>
        private const double SweepSpanMeters = 400000.0;
        private const int CellsPerAxis = 60;

        /// <summary>
        /// Each cell's slope is the mean over a 9x9 probe grid spanning 320 m, i.e. a 40 m pitch.
        /// Coarse enough to stay affordable across 3600 cells, fine enough that the macro relief and
        /// the large mesoscale both register.
        /// </summary>
        private const int ProbesPerAxis = 9;
        private const double CellProbeSpanMeters = 320.0;
        private const double ProbeOffsetMeters = 20.0;

        private static double CellMeanSlopeDegrees(double cx, double cz, in WorldMacroGeologyParams p)
        {
            double half = CellProbeSpanMeters * 0.5;
            double step = CellProbeSpanMeters / (ProbesPerAxis - 1);
            double sum = 0.0;
            int count = 0;

            for (int iz = 0; iz < ProbesPerAxis; iz++)
            {
                double z = cz - half + iz * step;
                for (int ix = 0; ix < ProbesPerAxis; ix++)
                {
                    double x = cx - half + ix * step;
                    float w = WorldMacroGeologyFields.EvaluateHeightMeters(x - ProbeOffsetMeters, z, in p);
                    float e = WorldMacroGeologyFields.EvaluateHeightMeters(x + ProbeOffsetMeters, z, in p);
                    float s = WorldMacroGeologyFields.EvaluateHeightMeters(x, z - ProbeOffsetMeters, in p);
                    float n = WorldMacroGeologyFields.EvaluateHeightMeters(x, z + ProbeOffsetMeters, in p);
                    float dx = (e - w) / (float)(ProbeOffsetMeters * 2.0);
                    float dz = (n - s) / (float)(ProbeOffsetMeters * 2.0);
                    sum += math.degrees(math.atan(math.sqrt(dx * dx + dz * dz)));
                    count++;
                }
            }

            return sum / count;
        }

        private static double[] SweepSlopes(in WorldMacroGeologyParams p)
        {
            var slopes = new double[CellsPerAxis * CellsPerAxis];
            double step = SweepSpanMeters / (CellsPerAxis - 1);
            double half = SweepSpanMeters * 0.5;
            int k = 0;

            for (int iz = 0; iz < CellsPerAxis; iz++)
            {
                double cz = -half + iz * step;
                for (int ix = 0; ix < CellsPerAxis; ix++)
                {
                    double cx = -half + ix * step;
                    slopes[k++] = CellMeanSlopeDegrees(cx, cz, in p);
                }
            }

            return slopes;
        }

        [Test]
        public void SlopeDistribution_AcrossTheWorld_IsReported()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            double[] slopes = SweepSlopes(in p);
            var sorted = (double[])slopes.Clone();
            System.Array.Sort(sorted);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"Mean slope per cell over a {SweepSpanMeters / 1000.0:0} km square, " +
                $"{CellsPerAxis}x{CellsPerAxis} = {slopes.Length} cells, each a {CellProbeSpanMeters:0} m " +
                $"window at a {ProbeOffsetMeters * 2:0} m probe pitch.");
            sb.AppendLine();

            double[] edges = { 0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 60, 90 };
            var counts = new int[edges.Length - 1];
            foreach (double s in slopes)
                for (int b = 0; b < counts.Length; b++)
                    if (s >= edges[b] && s < edges[b + 1]) { counts[b]++; break; }

            sb.AppendLine("  histogram:");
            for (int b = 0; b < counts.Length; b++)
            {
                double pct = 100.0 * counts[b] / slopes.Length;
                int bars = (int)math.round(pct * 0.8);
                sb.AppendLine(
                    $"    {edges[b],3:0}-{edges[b + 1],3:0} deg {pct,6:0.0}%  {new string('#', bars)}");
            }

            sb.AppendLine();
            double[] percentiles = { 5, 25, 50, 75, 90, 95, 99 };
            sb.Append("  percentiles: ");
            foreach (double q in percentiles)
            {
                int idx = (int)math.clamp(math.round(q / 100.0 * (sorted.Length - 1)), 0, sorted.Length - 1);
                sb.Append($"p{q:0}={sorted[idx]:0.0}  ");
            }
            sb.AppendLine();

            double mean = 0.0;
            foreach (double s in slopes) mean += s;
            mean /= slopes.Length;
            sb.AppendLine($"  mean {mean:0.0} deg, min {sorted[0]:0.0}, max {sorted[sorted.Length - 1]:0.0}");

            // The traversability read, stated as fractions rather than a verdict.
            int under20 = 0, under25 = 0, under30 = 0, over40 = 0;
            foreach (double s in slopes)
            {
                if (s < 20.0) under20++;
                if (s < 25.0) under25++;
                if (s < 30.0) under30++;
                if (s > 40.0) over40++;
            }
            sb.AppendLine();
            sb.AppendLine(
                $"  under 20 deg: {100.0 * under20 / slopes.Length:0.0}%   " +
                $"under 25: {100.0 * under25 / slopes.Length:0.0}%   " +
                $"under 30: {100.0 * under30 / slopes.Length:0.0}%   " +
                $"over 40: {100.0 * over40 / slopes.Length:0.0}%");
            sb.AppendLine();
            sb.AppendLine(
                "  Measured at a 40 m probe pitch, so this is the shape of the seafloor and not the " +
                "roughness a diver collider meets. A cell reported at 20 deg can still be locally " +
                "impassable at metre scale.");

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>
        /// The floor that matters for a game about swimming across a seafloor: most of the world has
        /// to be ground a diver can cross.
        ///
        /// The bar is that the MEDIAN cell is under 30 degrees. Median rather than mean, because a
        /// small number of vertical walls should not be able to condemn an otherwise gentle world, and
        /// 30 rather than 25 because a submarine diver is not a walker and the number should be
        /// defensible rather than flattering.
        ///
        /// This is the assertion the five-site slope budget was reaching for and could not support. It
        /// replaces it: a site mean says what one landform does, a median over 3600 cells says what the
        /// world does.
        /// </summary>
        [Test]
        public void MostOfTheWorld_IsTraversable()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            double[] slopes = SweepSlopes(in p);
            System.Array.Sort(slopes);
            double median = slopes[slopes.Length / 2];

            int under30 = 0;
            foreach (double s in slopes) if (s < 30.0) under30++;
            double pctUnder30 = 100.0 * under30 / slopes.Length;

            Assert.That(
                median,
                Is.LessThan(30.0),
                $"The median cell across a {SweepSpanMeters / 1000.0:0} km square measures {median:0.0} " +
                $"degrees and only {pctUnder30:0.0}% of cells are under 30. More than half this " +
                "seafloor is steeper than a diver can comfortably cross, which is a property of the " +
                "authored depth constants and the widths they are spread over, not of any one feature.");
        }
    }
}
