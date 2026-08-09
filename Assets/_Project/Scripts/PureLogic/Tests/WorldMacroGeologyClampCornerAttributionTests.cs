using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Attributes the gradient kinks in the height field to the specific mask that is sitting on a
    /// clamp corner where each kink occurs.
    ///
    /// The chain that leads here, each step measured rather than assumed:
    ///   1. Docs/Reports/CleanRoom/CleanRoom_XRay_Slope.png shows sharp scalloped lobe outlines and
    ///      CleanRoom_XRay_Curvature.png shows hard boundaries. Both are derivative artefacts.
    ///   2. A first-difference scan over 180 km found NO step discontinuities, so the height field is
    ///      C0. The artefact is a kink in the gradient, not a jump in the value.
    ///   3. A second-difference scan over the same transects found 192 kinks, about one per kilometre,
    ///      the worst at 69x the local median curvature.
    ///   4. Those kinks are NOT on any lattice. Their fractional positions on the crater grid (2500 m),
    ///      the volcano grid (5555 m) and the plate Voronoi (12000 m) are spread evenly across 0..1,
    ///      which rules out a truncated cell neighbourhood - the hypothesis this scan was built to
    ///      test - and rules out the province blend along with it.
    ///
    /// What remains is the family of hard clamps. math.saturate, math.min, math.max, math.abs and
    /// math.clamp are all continuous with a corner, and the locus of that corner in a smooth field is
    /// a smooth CURVE - which is what the slope X-Ray shows. EvaluateHeightMeters and its helpers use
    /// dozens of them, and every mask in MacroMasks is wrapped in saturate before being exported.
    ///
    /// This matters beyond looks. Material class is slope-driven
    /// (WorldTerrainDetailContracts.cs:180-188), so a gradient kink draws a hard material boundary
    /// along the same curve, and the scatter eligibility flags key off the same masks.
    ///
    /// It also has history. The comment at WorldMacroGeologyFields.cs:533-537 describes "the thin
    /// curved lines the Director kept seeing", attributes them to the province Voronoi F2-F1 edge and
    /// states that the exp-weighted blend removed them. The lines are still here and the province
    /// lattice is now ruled out, so that fix addressed a real kink that was not this one.
    ///
    /// This fixture reports which masks are pinned at 0 or 1 at each kink. A mask that is pinned at
    /// nearly every kink is the one whose clamp is being ridden.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyClampCornerAttributionTests
    {
        private const uint Seed = 880031u;
        private const double StepMeters = 2.0;

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

        /// <summary>
        /// A mask is "pinned" when it is within epsilon of 0 or 1 AND its neighbour on one side is
        /// strictly inside the range. That second condition is what separates a mask riding its clamp
        /// corner from one that is simply inactive across the whole neighbourhood - Lake is 0
        /// everywhere in the deep ocean and that is not a kink, whereas a Lake that is 0 here and
        /// 0.03 two metres away has just left its clamp and bent the surface.
        /// </summary>
        [Test]
        public void ClampCornersAtKinks_AreAttributedToMasks()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Masks leaving a clamp corner at each of the worst gradient kinks.");
            sb.AppendLine("'pinned' = within 0.002 of 0 or 1 at the kink, and strictly inside 2 m away.");
            sb.AppendLine();

            (double X, double Z, double DX, double DZ, string Label)[] transects =
            {
                (0.0, 1500.0, 1.0, 0.0, "east from origin"),
                (1500.0, 0.0, 0.0, 1.0, "north from origin"),
                (280000.0, 90000.0, 1.0, 0.0, "east through P4_far")
            };

            var tally = new int[MaskNames.Length];
            int kinksExamined = 0;

            foreach (var t in transects)
            {
                int steps = 15000;   // 30 km at 2 m
                var heights = new float[steps];
                var maskRows = new float[steps][];

                for (int i = 0; i < steps; i++)
                {
                    double d = i * StepMeters;
                    heights[i] = WorldMacroGeologyFields.EvaluateHeightMeters(
                        t.X + t.DX * d, t.Z + t.DZ * d, in p,
                        out WorldMacroGeologyFields.MacroMasks m);
                    maskRows[i] = ToArray(in m);
                }

                // Rank by curvature, examine the strongest 12 on this transect.
                var ranked = new System.Collections.Generic.List<(int Index, float Curv)>();
                for (int i = 1; i < steps - 1; i++)
                {
                    float c = math.abs(heights[i + 1] - 2f * heights[i] + heights[i - 1]);
                    ranked.Add((i, c));
                }
                ranked.Sort((a, b) => b.Curv.CompareTo(a.Curv));

                sb.AppendLine($"  {t.Label}, strongest kinks:");
                for (int r = 0; r < 12 && r < ranked.Count; r++)
                {
                    int i = ranked[r].Index;
                    if (i < 2 || i >= steps - 2) continue;

                    var pinned = new System.Text.StringBuilder();
                    for (int c = 0; c < MaskNames.Length; c++)
                    {
                        float here = maskRows[i][c];
                        float before = maskRows[i - 1][c];
                        float after = maskRows[i + 1][c];

                        bool atCorner = here < 0.002f || here > 0.998f;
                        bool neighbourInside =
                            (before > 0.002f && before < 0.998f) || (after > 0.002f && after < 0.998f);

                        if (atCorner && neighbourInside)
                        {
                            pinned.Append($"{MaskNames[c]}={here:0.000}({before:0.000}/{after:0.000}) ");
                            tally[c]++;
                        }
                    }

                    kinksExamined++;
                    sb.AppendLine(
                        $"    x={t.X + t.DX * i * StepMeters,9:0} z={t.Z + t.DZ * i * StepMeters,8:0} " +
                        $"curv={ranked[r].Curv,7:0.000}m  {(pinned.Length == 0 ? "<no mask on a corner>" : pinned.ToString())}");
                }
                sb.AppendLine();
            }

            sb.AppendLine($"  Tally over {kinksExamined} examined kinks:");
            var order = new System.Collections.Generic.List<(string Name, int Count)>();
            for (int c = 0; c < MaskNames.Length; c++)
                if (tally[c] > 0) order.Add((MaskNames[c], tally[c]));
            order.Sort((a, b) => b.Count.CompareTo(a.Count));
            foreach (var o in order)
                sb.AppendLine($"    {o.Name,-16} on a corner at {o.Count,3} of {kinksExamined} kinks");
            if (order.Count == 0)
                sb.AppendLine("    NO mask was on a clamp corner at any examined kink - the kink is " +
                              "inside the height arithmetic, not in an exported mask.");

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }
    }
}
