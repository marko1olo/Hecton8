using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Tests the one combination that the arithmetic says can work: a bigger world AND a wider shelf
    /// break, moved together.
    ///
    /// THE ARITHMETIC. The shelf lerp drops AbyssDepthMeters - ShelfDepthMeters = 2860 m across a band
    /// of ShelfBreakWidthMeters. A flank at angle A therefore needs a band of 2860 / tan(A):
    ///
    ///     30 deg ->  5.0 km      15 deg -> 10.7 km      8 deg -> 20.4 km      5 deg -> 32.7 km
    ///
    /// The world is 30 km across. A 15 degree flank needs a third of it and an 8 degree flank needs
    /// two thirds, so a traversable seafloor and a 30 km world containing several margins cannot both
    /// exist at these depths. Measured directly: at an authored 41600 m width there are ZERO complete
    /// shelf crossings inside the world, because the band no longer fits.
    ///
    /// WHY NEITHER LEVER WORKS ALONE, both measured 2026-08-10:
    ///   - widening the band alone: the delivered band tracks the authored one at about 0.65x, so the
    ///     dial turns, but past 20 km the crossings stop fitting in the world and the median only
    ///     falls 4.9 deg
    ///   - stretching the province structure alone (ShelfProvinceCyclesPerWorld 1.35 -> 0.40): the
    ///     transition's share of the world collapses from 58.6% to 22.8% and the median falls only
    ///     4.0 deg, because WidthNormalisedGate pins the band to a fixed width IN METRES regardless of
    ///     the field's wavelength. Fewer walls, each exactly as steep.
    ///
    /// The band width in metres is the only thing that sets how steep a wall is, and a wide band needs
    /// a big world to fit inside. So this fixture scales the two together and measures whether the
    /// combination delivers what neither does alone.
    ///
    /// The measurement window stays at +/-15 km throughout, because ResolveMinimumChunkRange
    /// (WorldMacroGeologyFields.cs:441-448) derives the chunk grid from the CONST
    /// MinimumWorldExtentMeters and not from parameters.WorldExtentMeters - so raising the parameter
    /// stretches the field without enlarging the terrain the game emits. That asymmetry is itself
    /// worth knowing and is why the window is held fixed rather than scaled with the extent.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyExtentWidthPairSweepTests
    {
        private const uint Seed = (uint)WorldMacroGeologyFields.DefaultAuthoringSeed;

        /// <summary>The terrain the game actually emits, whatever the extent parameter says.</summary>
        private static readonly float EmittedHalfExtent =
            WorldMacroGeologyFields.MinimumWorldExtentMeters * 0.5f;

        private const int CellsPerAxis = 48;
        private const double ProbeOffsetMeters = 20.0;

        private static (double Median, double Mean, double PctUnder20, double PctUnder30,
                        double PctOver40, double Relief) Measure(float extent, float width)
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            p.WorldExtentMeters = extent;
            p.ShelfBreakWidthMeters = width;

            var slopes = new double[CellsPerAxis * CellsPerAxis];
            double step = (EmittedHalfExtent * 2.0) / (CellsPerAxis - 1);
            int k = 0;
            float lo = float.MaxValue, hi = float.MinValue;

            for (int iz = 0; iz < CellsPerAxis; iz++)
            {
                double z = -EmittedHalfExtent + iz * step;
                for (int ix = 0; ix < CellsPerAxis; ix++)
                {
                    double x = -EmittedHalfExtent + ix * step;

                    float c = WorldMacroGeologyFields.EvaluateHeightMeters(x, z, in p);
                    lo = math.min(lo, c);
                    hi = math.max(hi, c);

                    float w = WorldMacroGeologyFields.EvaluateHeightMeters(x - ProbeOffsetMeters, z, in p);
                    float e = WorldMacroGeologyFields.EvaluateHeightMeters(x + ProbeOffsetMeters, z, in p);
                    float s = WorldMacroGeologyFields.EvaluateHeightMeters(x, z - ProbeOffsetMeters, in p);
                    float n = WorldMacroGeologyFields.EvaluateHeightMeters(x, z + ProbeOffsetMeters, in p);
                    float dx = (e - w) / (float)(ProbeOffsetMeters * 2.0);
                    float dz = (n - s) / (float)(ProbeOffsetMeters * 2.0);
                    slopes[k++] = math.degrees(math.atan(math.sqrt(dx * dx + dz * dz)));
                }
            }

            System.Array.Sort(slopes);
            double mean = 0.0;
            int u20 = 0, u30 = 0, o40 = 0;
            foreach (double sl in slopes)
            {
                mean += sl;
                if (sl < 20.0) u20++;
                if (sl < 30.0) u30++;
                if (sl > 40.0) o40++;
            }

            return (slopes[slopes.Length / 2], mean / slopes.Length,
                    100.0 * u20 / slopes.Length, 100.0 * u30 / slopes.Length,
                    100.0 * o40 / slopes.Length, hi - lo);
        }

        [Test]
        public void ExtentAndWidthTogether_AreReported()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                "Scaling world extent and shelf break width together. The measured window stays at the");
            sb.AppendLine($"+/-{EmittedHalfExtent:0} m the chunk grid actually emits.");
            sb.AppendLine();
            sb.AppendLine($"    {"extent",9}{"width",9}{"median",9}{"mean",8}{"<20deg",9}{"<30deg",9}" +
                          $"{">40deg",9}{"relief",10}");

            (float Extent, float Width, string Note)[] variants =
            {
                (30000f, 5200f, "authored"),
                (30000f, 10400f, "width alone x2"),
                (60000f, 10400f, "both x2"),
                (120000f, 20800f, "both x4"),
                (240000f, 41600f, "both x8"),
                (480000f, 83200f, "both x16")
            };

            foreach (var v in variants)
            {
                var r = Measure(v.Extent, v.Width);
                sb.AppendLine(
                    $"    {v.Extent / 1000f,8:0}k{v.Width,8:0}m{r.Median,9:0.0}{r.Mean,8:0.0}" +
                    $"{r.PctUnder20,8:0.0}%{r.PctUnder30,8:0.0}%{r.PctOver40,8:0.0}%{r.Relief,9:0}m   {v.Note}");
            }

            sb.AppendLine();
            sb.AppendLine("  The relief column is the guard. A variant that flattens the world into a plane");
            sb.AppendLine("  would post a low median and a collapsed relief, and would be a regression.");
            sb.AppendLine("  The wanted shape is a falling median with the depth range intact.");
            sb.AppendLine();
            sb.AppendLine("  Note that raising WorldExtentMeters does NOT enlarge the terrain the game");
            sb.AppendLine("  emits: ResolveMinimumChunkRange reads the const, not the parameter. It");
            sb.AppendLine("  stretches the macro fields under a chunk grid that stays 30 km wide.");

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }
    }
}
