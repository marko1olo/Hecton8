using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Measures the terrain inside the world that actually exists, and states how far outside it every
    /// other slope number in this suite was taken.
    ///
    /// THE FINDING, 2026-08-10. WorldExtentMeters is never set to anything but
    /// MinimumWorldExtentMeters = 30000. WorldMacroGeologyParams.CreateDefault assigns it
    /// (WorldMacroGeologyFields.cs:46), WorldProceduralFieldSampler's serialized field defaults to it
    /// and is overridden by no scene, prefab or asset in the project, and RenderDirectTextures.cs:23
    /// hardcodes 30000f. ResolveMinimumChunkRange (:441-448) then bounds the chunk grid to
    /// +/-15000 m. The playable world is a 30 km square centred on the origin.
    ///
    /// Four of the five atlas probe sites are outside it:
    ///
    ///     P1_origin    (5000, 5000)         inside
    ///     P2_near      (50000, 50000)       3.3x the half extent
    ///     P3_west      (-40000, 15000)      2.7x
    ///     P4_far       (300000, 90000)      20x
    ///     P5_deepfar   (777000, -333000)    52x
    ///
    /// The macro fields normalise position by the extent and simplex noise does not tile, so sampling
    /// at 26 world-extents is not "a far part of the world" - it is a region the authored province
    /// structure was never shaped to describe, reached by walking off the end of the noise domain.
    ///
    /// This matters to every conclusion drawn from those sites, including this session's headline
    /// figure that the world's median cell measures 29.1 degrees, which was swept over a 400 km square
    /// - 13x13 world extents, of which the real world is one part in 178.
    ///
    /// The arithmetic in those measurements was right. The framing was wrong, and the framing is what
    /// gets quoted. This fixture exists so the in-world number is the one on record.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyInWorldSlopeTests
    {
        private const uint Seed = (uint)WorldMacroGeologyFields.DefaultAuthoringSeed;

        /// <summary>
        /// The world, as ResolveMinimumChunkRange defines it: +/-15 km on both axes.
        /// </summary>
        private static readonly float HalfExtent = WorldMacroGeologyFields.MinimumWorldExtentMeters * 0.5f;

        private const int CellsPerAxis = 64;
        private const double ProbeOffsetMeters = 20.0;

        private static double[] Sweep(double halfSpan, in WorldMacroGeologyParams p)
        {
            var slopes = new double[CellsPerAxis * CellsPerAxis];
            double step = (halfSpan * 2.0) / (CellsPerAxis - 1);
            int k = 0;

            for (int iz = 0; iz < CellsPerAxis; iz++)
            {
                double z = -halfSpan + iz * step;
                for (int ix = 0; ix < CellsPerAxis; ix++)
                {
                    double x = -halfSpan + ix * step;
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
            return slopes;
        }

        private static string Describe(string label, double[] sorted)
        {
            double mean = 0.0;
            foreach (double s in sorted) mean += s;
            mean /= sorted.Length;

            int under20 = 0, under30 = 0, over40 = 0;
            foreach (double s in sorted)
            {
                if (s < 20.0) under20++;
                if (s < 30.0) under30++;
                if (s > 40.0) over40++;
            }

            return $"    {label,-28}{sorted[sorted.Length / 2],7:0.0}{mean,8:0.0}" +
                   $"{sorted[(int)(sorted.Length * 0.9)],8:0.0}" +
                   $"{100.0 * under20 / sorted.Length,9:0.0}%" +
                   $"{100.0 * under30 / sorted.Length,9:0.0}%" +
                   $"{100.0 * over40 / sorted.Length,9:0.0}%";
        }

        [Test]
        public void InWorldSlope_VersusOutOfWorld_IsReported()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"Slope inside the real world (+/-{HalfExtent:0} m) against the areas previously swept.");
            sb.AppendLine($"{CellsPerAxis}x{CellsPerAxis} cells each, {ProbeOffsetMeters * 2:0} m probe.");
            sb.AppendLine();
            sb.AppendLine($"    {"area",-28}{"median",7}{"mean",8}{"p90",8}{"<20deg",10}{"<30deg",10}{">40deg",10}");

            sb.AppendLine(Describe($"the world: 30 km square", Sweep(HalfExtent, in p)));
            sb.AppendLine(Describe("inner half: 15 km square", Sweep(HalfExtent * 0.5, in p)));
            sb.AppendLine(Describe("100 km square (3.3x world)", Sweep(50000.0, in p)));
            sb.AppendLine(Describe("400 km square (13x world)", Sweep(200000.0, in p)));

            sb.AppendLine();
            sb.AppendLine("  The 400 km row is the figure this suite has been quoting. The first row is the");
            sb.AppendLine("  one that describes terrain a player can reach.");

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>
        /// The traversability floor, asserted where it means something: inside the world.
        ///
        /// Median under 30 degrees, the same bar the 400 km sweep used, so the two are comparable. If
        /// the in-world median passes while the 400 km median fails, that is not the world improving -
        /// it is the earlier measurement having been taken somewhere the game cannot go.
        /// </summary>
        [Test]
        public void TheWorldPlayersCanReach_IsMostlyTraversable()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            double[] sorted = Sweep(HalfExtent, in p);
            double median = sorted[sorted.Length / 2];

            int under30 = 0;
            foreach (double s in sorted) if (s < 30.0) under30++;

            Assert.That(
                median,
                Is.LessThan(30.0),
                $"Median slope inside the 30 km world is {median:0.0} degrees and only " +
                $"{100.0 * under30 / sorted.Length:0.0}% of it is under 30. This is the number that " +
                "matters: it describes ground a player can actually swim over, unlike the 400 km sweep " +
                "which covers 13x13 world extents of terrain outside the chunk range that " +
                "ResolveMinimumChunkRange will ever emit.");
        }

        /// <summary>
        /// Guards the assumption the rest of this fixture rests on. If someone raises the world extent,
        /// every site coordinate and every swept area in this suite needs revisiting, and a silent
        /// change would leave the fixtures measuring the wrong square while still passing.
        /// </summary>
        [Test]
        public void WorldExtent_IsStillThirtyKilometres()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);

            Assert.That(
                p.WorldExtentMeters,
                Is.EqualTo(30000f),
                "CreateDefault no longer produces a 30 km world. Four of the five atlas probe sites " +
                "(P2 at 50 km, P3 at 40 km, P4 at 300 km, P5 at 777 km) were chosen when the extent " +
                "was 30 km and are outside it; if the extent has changed they may now be inside, " +
                "outside by a different factor, or meaningless. Re-derive them before trusting any " +
                "site-based slope number.");

            WorldMacroGeologyFields.ResolveMinimumChunkRange(
                1000f, out int minX, out int minZ, out int maxX, out int maxZ);

            Assert.That(minX, Is.EqualTo(-15), "chunk range no longer starts at -15 km");
            Assert.That(maxX, Is.EqualTo(14), "chunk range no longer ends at +15 km");
            Assert.That(minZ, Is.EqualTo(minX));
            Assert.That(maxZ, Is.EqualTo(maxX));
        }
    }
}
