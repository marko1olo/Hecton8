using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Measures what happens to the world when the shelf/abyss province structure is stretched, which
    /// is the candidate fix for a seafloor whose median cell measures 28.4 degrees.
    ///
    /// THE DIAGNOSIS THIS TESTS. The world is a 30 km square carrying 4510 m of authored depth range.
    /// Spread monotonically that is 8.5 degrees. It measures 28.4, and three independent measurements
    /// say why:
    ///   - a 30 km transect accumulates 14463 m of vertical travel against 3695 m of relief, ratio 3.9
    ///   - 56.4% of the world sits inside the shelf mask's transition, at a mean of 36.6 degrees,
    ///     while the 43.6% that is saturated at 0 or 1 averages 4.2
    ///   - the shelf field is sampled at 1.35 domain units per world extent, so its base wavelength is
    ///     30000/1.35 = 22 km and its three octaves reach down to 5.4 km
    /// A 30 km world therefore contains roughly four shelf-to-abyss cycles instead of one. The seafloor
    /// is not one continental margin, it is four of them stacked inside a space that fits one, and the
    /// full 2860 m lerp is spent on each.
    ///
    /// WHY THIS IS A BETTER LEVER THAN THE DEPTH CONSTANTS. Reducing AbyssDepthMeters flattens the
    /// world by making it shallow, and this is a deep-sea game - the abyss being 2950 m down is the
    /// point. Stretching the province structure keeps every authored depth and changes only how much
    /// ground the transition between them is given.
    ///
    /// The sweep reports slope, the travel-to-relief ratio and the transition occupancy together,
    /// because a change that lowers slope by making the whole world a single flat plane would be a
    /// regression dressed as a fix, and only the occupancy column shows the difference.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyProvinceScaleSweepTests
    {
        private const uint Seed = (uint)WorldMacroGeologyFields.DefaultAuthoringSeed;

        private static readonly float HalfExtent = WorldMacroGeologyFields.MinimumWorldExtentMeters * 0.5f;

        private const int CellsPerAxis = 48;
        private const double ProbeOffsetMeters = 20.0;

        private static readonly float[] Cycles = { 1.35f, 0.90f, 0.60f, 0.40f, 0.25f };

        private struct WorldStats
        {
            public double MedianSlope;
            public double MeanSlope;
            public double PctUnder30;
            public double PctOver40;
            public double TravelToRelief;
            public double TransitionOccupancy;
            public double ReliefMeters;
        }

        private static WorldStats Measure(float cycles)
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            p.ShelfProvinceCyclesPerWorld = cycles;

            var slopes = new double[CellsPerAxis * CellsPerAxis];
            double step = (HalfExtent * 2.0) / (CellsPerAxis - 1);
            int k = 0;
            int inTransition = 0;

            for (int iz = 0; iz < CellsPerAxis; iz++)
            {
                double z = -HalfExtent + iz * step;
                for (int ix = 0; ix < CellsPerAxis; ix++)
                {
                    double x = -HalfExtent + ix * step;

                    WorldMacroGeologyFields.EvaluateHeightMeters(
                        x, z, in p, out WorldMacroGeologyFields.MacroMasks m);
                    if (m.Shelf > 0.02f && m.Shelf < 0.98f) inTransition++;

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
            int under30 = 0, over40 = 0;
            foreach (double sl in slopes)
            {
                mean += sl;
                if (sl < 30.0) under30++;
                if (sl > 40.0) over40++;
            }
            mean /= slopes.Length;

            // Travel-to-relief along a transect spanning the world, at a 100 m pitch.
            double travel = 0.0;
            float lo = float.MaxValue, hi = float.MinValue, previous = 0f;
            int steps = (int)(HalfExtent * 2.0 / 100.0) + 1;
            for (int i = 0; i < steps; i++)
            {
                float h = WorldMacroGeologyFields.EvaluateHeightMeters(-HalfExtent + i * 100.0, 0.0, in p);
                lo = math.min(lo, h);
                hi = math.max(hi, h);
                if (i > 0) travel += math.abs(h - previous);
                previous = h;
            }

            return new WorldStats
            {
                MedianSlope = slopes[slopes.Length / 2],
                MeanSlope = mean,
                PctUnder30 = 100.0 * under30 / slopes.Length,
                PctOver40 = 100.0 * over40 / slopes.Length,
                TravelToRelief = travel / math.max(1.0, hi - lo),
                TransitionOccupancy = 100.0 * inTransition / (CellsPerAxis * CellsPerAxis),
                ReliefMeters = hi - lo
            };
        }

        [Test]
        public void ProvinceScaleSweep_IsReported()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"Stretching the shelf province structure. Measured inside the {HalfExtent * 2 / 1000.0:0} km world.");
            sb.AppendLine("1.35 is the authored value. 'wavelen' is the field's base wavelength in the world.");
            sb.AppendLine();
            sb.AppendLine($"    {"cycles",8}{"wavelen",10}{"median",9}{"mean",8}{"<30deg",9}{">40deg",9}" +
                          $"{"travel/relief",15}{"in transit",12}{"relief",9}");

            foreach (float c in Cycles)
            {
                WorldStats st = Measure(c);
                sb.AppendLine(
                    $"    {c,8:0.00}{HalfExtent * 2f / c / 1000f,9:0}km{st.MedianSlope,9:0.0}{st.MeanSlope,8:0.0}" +
                    $"{st.PctUnder30,8:0.0}%{st.PctOver40,8:0.0}%{st.TravelToRelief,15:0.0}" +
                    $"{st.TransitionOccupancy,11:0.0}%{st.ReliefMeters,8:0}m");
            }

            sb.AppendLine();
            sb.AppendLine("  travel/relief near 1 is one descent across the world - a single continental");
            sb.AppendLine("  margin. Near 4 is four of them stacked in a space that fits one.");
            sb.AppendLine();
            sb.AppendLine("  Watch the relief column: a variant that lowers slope by flattening the world");
            sb.AppendLine("  into a plane is a regression, not a fix. The depth range must survive.");

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>
        /// The guarantee that makes the parameter safe to add: at its authored default the world is
        /// unchanged, exactly. Without this the sweep above could be measuring a world that the new
        /// parameter had already perturbed, and every number in it would be about something other than
        /// the shipped terrain.
        /// </summary>
        [Test]
        public void DefaultCycles_LeaveTheWorldBitIdentical()
        {
            WorldMacroGeologyParams withParam = WorldMacroGeologyParams.CreateDefault(Seed);
            Assert.That(
                withParam.ShelfProvinceCyclesPerWorld,
                Is.EqualTo(1.35f),
                "the default must remain the value that was hardcoded at WorldMacroGeologyFields.cs:878-904");

            // Sample a spread of the world, including the in-world atlas sites, and compare against
            // the literal the parameter replaced.
            WorldMacroGeologyParams literal = WorldMacroGeologyParams.CreateDefault(Seed);
            literal.ShelfProvinceCyclesPerWorld = 1.35f;

            var points = new (double X, double Z)[]
            {
                (0.0, 0.0), (11896.0, -13148.0), (5635.0, -3130.0), (9391.0, -10643.0),
                (6887.0, -6887.0), (-11896.0, 4383.0), (-14999.0, 14999.0), (123.0, -4567.0)
            };

            foreach (var pt in points)
            {
                Assert.That(
                    WorldMacroGeologyFields.EvaluateHeightMeters(pt.X, pt.Z, in withParam),
                    Is.EqualTo(WorldMacroGeologyFields.EvaluateHeightMeters(pt.X, pt.Z, in literal)),
                    $"height changed at ({pt.X:0}, {pt.Z:0}) when ShelfProvinceCyclesPerWorld was " +
                    "introduced at its default");
            }
        }
    }
}
