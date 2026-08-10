using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Asks whether the clean-room X-Ray - the only picture of this terrain anyone actually looks at -
    /// contains the features being fixed, or whether it is a view of somewhere quiet.
    ///
    /// Why this fixture exists. On 2026-08-10 the clean room was re-run after 126 lines of change to
    /// EvaluateHeightMeters had landed (the WidthNormalisedGate extraction and the trench rewrite), and
    /// all four deterministic X-Rays came back BIT-IDENTICAL to the render taken before those changes -
    /// same SHA-1, not merely the same byte count. Only CleanRoom_Beauty.png differed, and a camera
    /// render differs run to run anyway, so it settles nothing.
    ///
    /// Bit-identical output has exactly two explanations: the render did not recompute, or the terrain
    /// under it genuinely did not move. CleanRoomTerrainTest builds a 3x3 grid of 1000 m chunks at
    /// GridRadius 1 and exports the X-Rays from the CENTRE chunk alone (CleanRoomTerrainTest.cs:94-96,
    /// :113); the centre chunk has origin (0,0) and spans 0..1000 m. So every X-Ray the owner has been
    /// shown covers one square kilometre at the corner of the world, and not one of the five atlas
    /// sites is inside it - P1_origin is at (5000, 5000), five tile-widths away.
    ///
    /// The seed is not a confound: CleanRoomTerrainTest passes
    /// WorldMacroGeologyFields.DefaultAuthoringSeed, which is 880031 (WorldMacroGeologyFields.cs:176),
    /// the same seed every atlas fixture uses. Checked rather than assumed.
    ///
    /// This fixture measures the tile directly and prints it next to the atlas sites. A mask that is
    /// flat across those 1000 m cannot draw anything there however its authored width is changed, so if
    /// the rewired masks are flat, the picture cannot show the work and does not disprove it. The
    /// instrument has to move before any visual claim is made from it.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyCleanRoomCoverageTests
    {
        private const uint Seed = (uint)WorldMacroGeologyFields.DefaultAuthoringSeed;

        private const double TileMinX = 0.0;
        private const double TileMinZ = 0.0;
        private const double TileSpanMeters = 1000.0;
        private const int SamplesPerAxis = 64;

        private static readonly string[] MaskNames =
        {
            "Shelf", "ShelfBreak", "Ridge", "Trench", "Basin", "Fault", "Crater", "Canyon",
            "HardRock", "PlateEdge", "Terrace", "Slump", "River", "Lake", "Strata", "Fold",
            "Volcano", "Mesa", "Dune", "Continentality", "Reef", "Ledge", "CaveEntrance", "BrinePool"
        };

        private static float[] ToArray(in WorldMacroGeologyFields.MacroMasks m)
        {
            return new[]
            {
                m.Shelf, m.ShelfBreak, m.Ridge, m.Trench, m.Basin, m.Fault, m.Crater, m.Canyon,
                m.HardRock, m.PlateEdge, m.Terrace, m.Slump, m.River, m.Lake, m.Strata, m.Fold,
                m.Volcano, m.Mesa, m.Dune, m.Continentality, m.Reef, m.Ledge, m.CaveEntrance, m.BrinePool
            };
        }

        private static double MeanSlopeDegrees(double minX, double minZ, in WorldMacroGeologyParams p)
        {
            const double probe = 12.0;
            double step = TileSpanMeters / (SamplesPerAxis - 1);
            double sum = 0.0;
            int count = 0;
            for (int iz = 0; iz < SamplesPerAxis; iz++)
            {
                double z = minZ + iz * step;
                for (int ix = 0; ix < SamplesPerAxis; ix++)
                {
                    double x = minX + ix * step;
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

        /// <summary>
        /// A mask's SWING over the tile is what decides whether a change to it can be seen there. A mask
        /// pinned at 1.0 across the whole square contributes a constant to the height and therefore
        /// draws nothing, however much its authored width is altered; a mask at 0 everywhere is simply
        /// absent. Only a mask that MOVES can put a feature in the picture.
        /// </summary>
        [Test]
        public void CleanRoomTile_MaskCoverage_IsReported()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);

            var min = new float[MaskNames.Length];
            var max = new float[MaskNames.Length];
            var sum = new double[MaskNames.Length];
            for (int c = 0; c < MaskNames.Length; c++)
            {
                min[c] = float.MaxValue;
                max[c] = float.MinValue;
            }

            double step = TileSpanMeters / (SamplesPerAxis - 1);
            float loH = float.MaxValue;
            float hiH = float.MinValue;

            for (int iz = 0; iz < SamplesPerAxis; iz++)
            {
                double z = TileMinZ + iz * step;
                for (int ix = 0; ix < SamplesPerAxis; ix++)
                {
                    double x = TileMinX + ix * step;
                    float h = WorldMacroGeologyFields.EvaluateHeightMeters(
                        x, z, in p, out WorldMacroGeologyFields.MacroMasks m);
                    loH = math.min(loH, h);
                    hiH = math.max(hiH, h);

                    float[] row = ToArray(in m);
                    for (int c = 0; c < row.Length; c++)
                    {
                        min[c] = math.min(min[c], row[c]);
                        max[c] = math.max(max[c], row[c]);
                        sum[c] += row[c];
                    }
                }
            }

            int cells = SamplesPerAxis * SamplesPerAxis;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("What the clean-room X-Rays actually cover, mask by mask.");
            sb.AppendLine("Exported from the CENTRE chunk alone: world (0,0)..(1000,1000), seed 880031.");
            sb.AppendLine($"Height over the tile: {loH:0.0} .. {hiH:0.0} m, relief {hiH - loH:0.0} m.");
            sb.AppendLine();
            sb.AppendLine($"    {"mask",-16}{"min",8}{"max",8}{"mean",8}{"swing",9}   verdict");

            int live = 0;
            for (int c = 0; c < MaskNames.Length; c++)
            {
                float swing = max[c] - min[c];
                string verdict;
                if (swing < 0.001f)
                    verdict = max[c] < 0.001f ? "DEAD - 0 everywhere"
                            : max[c] > 0.999f ? "PINNED AT 1 - constant" : "constant";
                else if (swing < 0.05f) verdict = "barely moves";
                else { verdict = "live"; live++; }

                sb.AppendLine(
                    $"    {MaskNames[c],-16}{min[c],8:0.000}{max[c],8:0.000}" +
                    $"{sum[c] / cells,8:0.000}{swing,9:0.000}   {verdict}");
            }
            sb.AppendLine();
            sb.AppendLine($"  {live} of {MaskNames.Length} masks are live (swing >= 0.05) inside the tile.");
            sb.AppendLine();

            (double X, double Z, string Label)[] sites =
            {
                (5000.0, 5000.0, "P1_origin"),
                (50000.0, 50000.0, "P2_near"),
                (-40000.0, 15000.0, "P3_west"),
                (300000.0, 90000.0, "P4_far"),
                (777000.0, -333000.0, "P5_deepfar")
            };

            sb.AppendLine("  Mean slope over a 1 km window, clean-room tile against the atlas sites:");
            sb.AppendLine($"    {"cleanroom",-12} {MeanSlopeDegrees(TileMinX, TileMinZ, in p),6:0.0} deg   <- what the pictures show");
            foreach (var s in sites)
                sb.AppendLine($"    {s.Label,-12} {MeanSlopeDegrees(s.X - 500.0, s.Z - 500.0, in p),6:0.0} deg");

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>
        /// Locks the conclusion so it cannot quietly rot: the tile the X-Rays are cut from must contain
        /// the shelf break, because the shelf break is the single largest vertical move in the
        /// generator and the only authored width measured to have real authority over slope
        /// (WorldMacroGeologyWidthParameterAuthorityTests: 4x widening moved P5_deepfar 46.9 -> 31.4).
        ///
        /// The criterion is a CROSSING, not a swing. The first version of this assertion asked for
        /// peak-to-trough swing above 0.05 and PASSED on 0.057 - while the mask's mean over the same
        /// tile was 0.001, i.e. the shelf touches one corner and is absent from the other 999 999 square
        /// metres. A peak is not coverage. To be in the picture the mask has to go from off to on
        /// somewhere inside the frame, so the test demands min &lt; 0.25 and max &gt; 0.75.
        ///
        /// If this fails, the picture is not evidence about the shelf work and the clean room needs to
        /// be re-aimed at a site that has one. Failing is the correct outcome to publish - it says the
        /// instrument is pointed at the wrong place, which is a defect in the proof, not in the terrain.
        /// </summary>
        [Test]
        public void CleanRoomTile_ContainsTheShelfBreak()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);

            float lo = float.MaxValue;
            float hi = float.MinValue;
            double step = TileSpanMeters / (SamplesPerAxis - 1);
            int inTransition = 0;
            int cells = 0;

            for (int iz = 0; iz < SamplesPerAxis; iz++)
            {
                double z = TileMinZ + iz * step;
                for (int ix = 0; ix < SamplesPerAxis; ix++)
                {
                    double x = TileMinX + ix * step;
                    WorldMacroGeologyFields.EvaluateHeightMeters(
                        x, z, in p, out WorldMacroGeologyFields.MacroMasks m);
                    lo = math.min(lo, m.Shelf);
                    hi = math.max(hi, m.Shelf);
                    if (m.Shelf > 0.05f && m.Shelf < 0.95f) inTransition++;
                    cells++;
                }
            }

            double transitionPercent = 100.0 * inTransition / cells;
            string detail =
                $"Shelf mask over the 1 km tile the X-Rays are exported from: min {lo:0.000}, " +
                $"max {hi:0.000}, {transitionPercent:0.0}% of samples inside the 0.05..0.95 transition " +
                "band. For the break to be visible the mask must cross the frame - go from off to on " +
                "inside it - not merely graze one corner. Every visual claim made from " +
                "CleanRoom_XRay_*.png about the shelf work is otherwise made from a view that cannot " +
                "contain it, which is how a bit-identical re-render after 126 lines of change came to " +
                "look like a null result on 2026-08-10.";

            Assert.That(lo, Is.LessThan(0.25f), detail);
            Assert.That(hi, Is.GreaterThan(0.75f), detail);
        }
    }
}
