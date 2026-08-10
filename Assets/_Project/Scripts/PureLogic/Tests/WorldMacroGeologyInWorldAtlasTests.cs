using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Finds probe sites INSIDE the 30 km world that are representative of it, and prints their
    /// coordinates so the atlas fixtures can be re-based on terrain a player can actually reach.
    ///
    /// Why the old atlas has to be replaced rather than trimmed. The five sites this suite has used
    /// since the geology work began - P1_origin (5000, 5000), P2_near (50000, 50000), P3_west
    /// (-40000, 15000), P4_far (300000, 90000), P5_deepfar (777000, -333000) - were chosen before
    /// anyone checked WorldExtentMeters, which is 30000 and is never overridden by any scene, prefab
    /// or asset. ResolveMinimumChunkRange bounds the chunk grid to +/-15000 m, so only P1_origin is
    /// inside the world. P5_deepfar is 52 half-extents outside it.
    ///
    /// Two currently-failing fixtures assert on P5_deepfar - EverySite_HasSomeTraversableGround and
    /// MaterialPalette_DoesNotCollapseToRock. They are failing about ground the game will never emit.
    /// A test that fails for a real reason in an unreachable place is worse than no test: it spends
    /// attention on a defect that cannot be observed, and it makes the suite look like it is covering
    /// the world when it is covering the noise field's behaviour past the end of its domain.
    ///
    /// Sites are chosen by percentile of the in-world slope distribution rather than by hand, so
    /// "flat", "typical" and "steep" mean something measured instead of something picked. The site
    /// nearest each target percentile is reported with its coordinates, mean slope and relief.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyInWorldAtlasTests
    {
        private const uint Seed = (uint)WorldMacroGeologyFields.DefaultAuthoringSeed;

        private static readonly float HalfExtent = WorldMacroGeologyFields.MinimumWorldExtentMeters * 0.5f;

        /// <summary>
        /// Candidate sites on a 24x24 lattice inside the world, inset by 600 m so a 1 km measurement
        /// window around any of them stays inside the chunk range.
        /// </summary>
        private const int LatticePerAxis = 24;
        private const double InsetMeters = 600.0;
        private const double WindowMeters = 1000.0;
        private const int WindowSamplesPerAxis = 24;
        private const double ProbeOffsetMeters = 12.0;

        private static (double Slope, double Relief) Window(double cx, double cz, in WorldMacroGeologyParams p)
        {
            double half = WindowMeters * 0.5;
            double step = WindowMeters / (WindowSamplesPerAxis - 1);
            double sum = 0.0;
            int count = 0;
            float lo = float.MaxValue, hi = float.MinValue;

            for (int iz = 0; iz < WindowSamplesPerAxis; iz++)
            {
                double z = cz - half + iz * step;
                for (int ix = 0; ix < WindowSamplesPerAxis; ix++)
                {
                    double x = cx - half + ix * step;
                    float h = WorldMacroGeologyFields.EvaluateHeightMeters(x, z, in p);
                    lo = math.min(lo, h);
                    hi = math.max(hi, h);

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

            return (sum / count, hi - lo);
        }

        [Test]
        public void RepresentativeInWorldSites_AreReported()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);

            double usable = HalfExtent - InsetMeters;
            double step = (usable * 2.0) / (LatticePerAxis - 1);

            var sites = new System.Collections.Generic.List<(double X, double Z, double Slope, double Relief)>();
            for (int iz = 0; iz < LatticePerAxis; iz++)
            {
                double z = -usable + iz * step;
                for (int ix = 0; ix < LatticePerAxis; ix++)
                {
                    double x = -usable + ix * step;
                    var wnd = Window(x, z, in p);
                    sites.Add((x, z, wnd.Slope, wnd.Relief));
                }
            }

            sites.Sort((a, b) => a.Slope.CompareTo(b.Slope));

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"Representative sites inside the {HalfExtent * 2 / 1000.0:0} km world, chosen by " +
                $"percentile of the 1 km mean slope over {sites.Count} candidates.");
            sb.AppendLine();
            sb.AppendLine($"    {"percentile",-12}{"x",10}{"z",10}{"slope",9}{"relief",10}   suggested name");

            (double Q, string Name)[] picks =
            {
                (0.02, "W1_flat"),
                (0.25, "W2_gentle"),
                (0.50, "W3_typical"),
                (0.75, "W4_steep"),
                (0.98, "W5_wall")
            };

            foreach (var pick in picks)
            {
                int idx = (int)math.clamp(math.round(pick.Q * (sites.Count - 1)), 0, sites.Count - 1);
                var s = sites[idx];
                sb.AppendLine(
                    $"    p{pick.Q * 100,-11:0}{s.X,10:0}{s.Z,10:0}{s.Slope,8:0.0}{s.Relief,9:0}m   {pick.Name}");
            }

            sb.AppendLine();
            sb.AppendLine($"    range across the world: {sites[0].Slope:0.0} deg at " +
                          $"({sites[0].X:0}, {sites[0].Z:0}) up to {sites[sites.Count - 1].Slope:0.0} deg at " +
                          $"({sites[sites.Count - 1].X:0}, {sites[sites.Count - 1].Z:0})");
            sb.AppendLine();
            sb.AppendLine("    For comparison, the sites this suite has been using and where they are:");
            (double X, double Z, string Label)[] old =
            {
                (5000.0, 5000.0, "P1_origin"),
                (50000.0, 50000.0, "P2_near"),
                (-40000.0, 15000.0, "P3_west"),
                (300000.0, 90000.0, "P4_far"),
                (777000.0, -333000.0, "P5_deepfar")
            };
            foreach (var o in old)
            {
                double outFactor = math.max(math.abs(o.X), math.abs(o.Z)) / HalfExtent;
                var wnd = Window(o.X, o.Z, in p);
                string where = outFactor <= 1.0 ? "INSIDE" : $"{outFactor:0.0}x outside";
                sb.AppendLine($"    {o.Label,-12}{o.X,10:0}{o.Z,10:0}{wnd.Slope,8:0.0}{wnd.Relief,9:0}m   {where}");
            }

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }
    }
}
